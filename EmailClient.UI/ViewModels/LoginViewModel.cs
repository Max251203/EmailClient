using EmailClient.Core.Models;
using EmailClient.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EmailClient.UI.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private EmailProvider _selectedProvider;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private EmailServerSettings _customSettings;
        private EmailServerSettings? _currentSettings;

        public LoginViewModel()
        {
            _customSettings = new EmailServerSettings();
            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => CanLogin());
            OpenGmailSettingsCommand = new RelayCommand(_ => OpenGmailSettings());

            _selectedProvider = EmailProvider.MailRu;
            UpdateCurrentSettings();
        }

        public ObservableCollection<EmailProvider> AvailableProviders =>
            new ObservableCollection<EmailProvider>
            {
                EmailProvider.MailRu,
                EmailProvider.GstuMail,
                EmailProvider.Custom
            };

        public EmailProvider SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider != value)
                {
                    _selectedProvider = value;
                    UpdateCurrentSettings();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCustomServer));
                    OnPropertyChanged(nameof(IsMailRu));
                    OnPropertyChanged(nameof(ProviderInstructions));
                    ErrorMessage = string.Empty;
                    (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private void UpdateCurrentSettings()
        {
            _currentSettings = EmailProviderSettings.GetSettings(SelectedProvider);
            if (IsCustomServer)
            {
                _currentSettings = _customSettings;
            }
            Debug.WriteLine($"Current settings updated: Server={_currentSettings.ImapServer}, Port={_currentSettings.ImapPort}");
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                (LoginCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool IsCustomServer => SelectedProvider == EmailProvider.Custom;
        public bool IsMailRu => SelectedProvider == EmailProvider.MailRu;

        public string ProviderInstructions
        {
            get
            {
                return SelectedProvider switch
                {
                    EmailProvider.MailRu => "Для подключения необходимо ввести пароль приложения, а не аккаунта!",
                    EmailProvider.Custom => "Настройте подключение вручную.",
                    _ => string.Empty
                };
            }
        }

        public EmailServerSettings CustomSettings
        {
            get => _customSettings;
            set
            {
                _customSettings = value;
                if (IsCustomServer)
                {
                    UpdateCurrentSettings();
                }
                OnPropertyChanged();
            }
        }

        public EmailAccount? EmailAccount { get; private set; }

        public ICommand LoginCommand { get; }
        public ICommand OpenGmailSettingsCommand { get; }

        public event EventHandler? LoginSuccessful;

        private bool CanLogin()
        {
            if (IsLoading) return false;
            if (string.IsNullOrWhiteSpace(Email)) return false;
            if (string.IsNullOrWhiteSpace(Password)) return false;

            if (IsCustomServer)
            {
                return !string.IsNullOrWhiteSpace(CustomSettings.ImapServer) &&
                       !string.IsNullOrWhiteSpace(CustomSettings.SmtpServer) &&
                       CustomSettings.ImapPort > 0 &&
                       CustomSettings.SmtpPort > 0;
            }

            return true;
        }

        private async Task LoginAsync()
        {
            try
            {
                

                IsLoading = true;
                ErrorMessage = string.Empty;

                if (_currentSettings == null)
                {
                    throw new InvalidOperationException("Server settings not initialized");
                }

                Debug.WriteLine($"[LOGIN] Attempting to connect using provider: {SelectedProvider}");
                Debug.WriteLine($"[LOGIN] Server settings: IMAP={_currentSettings.ImapServer}:{_currentSettings.ImapPort}");
                Debug.WriteLine($"[LOGIN] SSL: {_currentSettings.RequiresSsl}, Explicit: {_currentSettings.RequiresExplicitSsl}");

                // Определяем правильное имя пользователя для аутентификации
                string loginUsername = Email;
                if (SelectedProvider == EmailProvider.GstuMail && Email.Contains("@"))
                {
                    loginUsername = Email.Split('@')[0];
                }

                try
                {
                    var imapService = new ImapService();
                    await imapService.ConnectAsync(
                        _currentSettings.ImapServer,
                        _currentSettings.ImapPort,
                        _currentSettings.RequiresSsl,
                        _currentSettings.RequiresExplicitSsl);

                    await imapService.AuthenticateAsync(loginUsername, Password);
                    await imapService.DisconnectAsync();

                    // Если подключение успешно, создаем EmailAccount
                    EmailAccount = new EmailAccount
                    {
                        Email = Email,
                        Password = Password,
                        ImapServer = _currentSettings.ImapServer,  // Используем IMAP настройки
                        ImapPort = _currentSettings.ImapPort,
                        SmtpServer = _currentSettings.SmtpServer,
                        SmtpPort = _currentSettings.SmtpPort,
                        UseSsl = _currentSettings.RequiresSsl,
                        DisplayName = Email.Split('@')[0]
                    };

                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LOGIN] Connection error: {ex.Message}");
                    throw new Exception($"Connection failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                EmailAccount = null;
                ErrorMessage = GetFriendlyErrorMessage(ex);

                if (IsMailRu && ErrorMessage.Contains("authentication failed"))
                {
                    ErrorMessage += "\n\nFor Gmail, make sure you're using an App Password, not your regular password.";
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string GetFriendlyErrorMessage(Exception ex)
        {
            string message = ex.Message;

            // Логируем полное сообщение об ошибке для отладки
            Trace.WriteLine($"[LOGIN] Full error message: {message}");

            if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "Server is not responding. Please try again.";

            if (message.Contains("authentication failed", StringComparison.OrdinalIgnoreCase))
                return "Invalid email or password.";

            if (message.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
                return "Could not connect to the mail server. Please check your internet connection.";

            if (message.Contains("ssl/tls", StringComparison.OrdinalIgnoreCase))
                return "Secure connection failed. Please check your server settings.";

            // Возвращаем оригинальное сообщение, если не смогли определить тип ошибки
            return message;
        }

        private void OpenGmailSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://myaccount.google.com/apppasswords",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not open Gmail settings: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}