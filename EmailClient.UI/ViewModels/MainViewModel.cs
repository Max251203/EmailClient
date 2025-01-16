using EmailClient.Core.Models;
using EmailClient.Core.Services;
using EmailClient.UI.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace EmailClient.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SmtpService _smtpService;
        private readonly ImapService _imapService;
        private readonly SettingsService _settingsService;
        private ObservableCollection<EmailBox> _emailBoxes;
        private EmailBox? _selectedEmailBox;
        private ObservableCollection<Email> _emails;
        private ObservableCollection<EmailFolder> _folders;
        private Email? _selectedEmail;
        private EmailFolder? _selectedFolder;
        private bool _isLoading;
        private bool _isContentLoading;
        private string _statusMessage = string.Empty;
        private bool _isConnected;
        private CancellationTokenSource? _loadingCts;

        public MainViewModel()
        {
            _smtpService = new SmtpService();
            _imapService = new ImapService();
            _settingsService = new SettingsService();
            _emails = new ObservableCollection<Email>();
            _folders = new ObservableCollection<EmailFolder>();
            _emailBoxes = new ObservableCollection<EmailBox>();

            RefreshCommand = new RelayCommand(_ => LoadEmailsAsync(), _ => IsConnected);
            ComposeCommand = new RelayCommand(_ => ComposeNewEmailAsync(), _ => IsConnected);
            DeleteCommand = new RelayCommand(_ => DeleteSelectedEmailAsync(), _ => IsConnected && SelectedEmail != null);
            AddEmailBoxCommand = new RelayCommand(_ => AddNewEmailBox());
            RemoveEmailBoxCommand = new RelayCommand(box => RemoveEmailBox(box as EmailBox), _ => SelectedEmailBox != null);

            // Загружаем сохраненные аккаунты
            var accounts = _settingsService.LoadAccounts();
            if (accounts.Any())
            {
                foreach (var account in accounts)
                {
                    var emailBox = new EmailBox(account);
                    EmailBoxes.Add(emailBox);
                }
                SelectedEmailBox = EmailBoxes.First();
            }
        }

        private async Task LoadEmailsAsync()
        {
            if (SelectedFolder == null || SelectedEmailBox == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Refreshing emails...";

                await Task.Run(async () =>
                {
                    await _imapService.ConnectAsync(
                        SelectedEmailBox.Account.ImapServer,
                        SelectedEmailBox.Account.ImapPort,
                        SelectedEmailBox.Account.UseSsl);

                    await _imapService.AuthenticateAsync(
                        SelectedEmailBox.Account.Email,
                        SelectedEmailBox.Account.Password);

                    var emails = await _imapService.GetEmailHeadersAsync(SelectedFolder.Path);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Обновляем кэш
                        SelectedEmailBox.UpdateCache(SelectedFolder.Path, emails.ToList());
                        Emails = new ObservableCollection<Email>(emails);
                        StatusMessage = $"Loaded {emails.Count} emails";
                    });

                    await _imapService.DisconnectAsync();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error refreshing emails: {ex.Message}";
                    MessageBox.Show($"Failed to refresh emails: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ComposeCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddEmailBoxCommand { get; }
        public ICommand RemoveEmailBoxCommand { get; }

        public ObservableCollection<EmailBox> EmailBoxes
        {
            get => _emailBoxes;
            set
            {
                _emailBoxes = value;
                OnPropertyChanged();
            }
        }

        public EmailBox? SelectedEmailBox
        {
            get => _selectedEmailBox;
            set
            {
                if (_selectedEmailBox != value)
                {
                    _selectedEmailBox = value;
                    OnPropertyChanged();
                    LoadEmailBoxContent();
                    (RemoveEmailBoxCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<Email> Emails
        {
            get => _emails;
            set
            {
                _emails = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<EmailFolder> Folders
        {
            get => _folders;
            set
            {
                _folders = value;
                OnPropertyChanged();
            }
        }

        public EmailFolder? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (_selectedFolder != value)
                {
                    _selectedFolder = value;
                    OnPropertyChanged();
                    LoadEmailsForCurrentFolderAsync().ConfigureAwait(false);
                }
            }
        }

        public Email? SelectedEmail
        {
            get => _selectedEmail;
            set
            {
                if (_selectedEmail != value)
                {
                    _selectedEmail = value;
                    OnPropertyChanged();
                    LoadEmailContentAsync(value).ConfigureAwait(false);
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsContentLoading
        {
            get => _isContentLoading;
            set
            {
                _isContentLoading = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ComposeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        private async void AddNewEmailBox()
        {
            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true && loginWindow.EmailAccount != null)
            {
                try
                {
                    // Проверяем, не существует ли уже такой ящик
                    if (EmailBoxes.Any(box => box.Account.Email == loginWindow.EmailAccount.Email))
                    {
                        MessageBox.Show("This email account is already added.",
                            "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Заменяем .Wait() на await
                    await InitializeWithAccount(loginWindow.EmailAccount);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add email box: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RemoveEmailBox(EmailBox? box)
        {
            if (box == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove {box.DisplayName}?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Отключаемся от текущего ящика
                    await _imapService.DisconnectAsync();

                    // Очищаем все связанные данные
                    Emails.Clear();
                    Folders.Clear();

                    // Удаляем ящик из коллекции
                    EmailBoxes.Remove(box);

                    // Сохраняем обновленный список аккаунтов
                    _settingsService.SaveAccounts(EmailBoxes.Select(eb => eb.Account).ToList());

                    if (SelectedEmailBox == box)
                    {
                        SelectedEmailBox = EmailBoxes.FirstOrDefault();
                        if (SelectedEmailBox != null)
                        {
                            // Заменяем ConfigureAwait(false) на await
                            await InitializeWithAccount(SelectedEmailBox.Account);
                        }
                        else
                        {
                            // Если ящиков больше нет, сбрасываем состояние
                            IsConnected = false;
                            StatusMessage = "No email accounts configured";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing email box: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async Task InitializeWithAccount(EmailAccount account)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Initializing email box...";

                // Проверяем, существует ли уже такой ящик
                var existingBox = EmailBoxes.FirstOrDefault(box =>
                    box.Account.Email == account.Email);

                EmailBox emailBox;
                if (existingBox != null)
                {
                    emailBox = existingBox;
                }
                else
                {
                    emailBox = new EmailBox(account);
                    EmailBoxes.Add(emailBox);
                    _settingsService.SaveAccounts(EmailBoxes.Select(eb => eb.Account).ToList());
                }

                SelectedEmailBox = emailBox;
                await InitializeEmailBox(emailBox);
                IsConnected = true;
            }
            catch (Exception ex)
            {
                // Если инициализация не удалась, удаляем ящик
                if (!EmailBoxes.Any(box => box.Account.Email == account.Email))
                {
                    var failedBox = EmailBoxes.FirstOrDefault(box => box.Account.Email == account.Email);
                    if (failedBox != null)
                    {
                        EmailBoxes.Remove(failedBox);
                        _settingsService.SaveAccounts(EmailBoxes.Select(eb => eb.Account).ToList());
                    }
                }

                StatusMessage = $"Initialization failed: {ex.Message}";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InitializeEmailBox(EmailBox emailBox)
        {
            try
            {
                StatusMessage = "Connecting to server...";
                await _imapService.ConnectAsync(
                    emailBox.Account.ImapServer,
                    emailBox.Account.ImapPort,
                    emailBox.Account.UseSsl);

                StatusMessage = "Authenticating...";
                await _imapService.AuthenticateAsync(emailBox.Account.Email, emailBox.Account.Password);

                StatusMessage = "Loading folders...";
                var folders = await _imapService.GetFoldersAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    emailBox.Folders.Clear(); // Очищаем существующие папки
                    foreach (var folder in folders)
                    {
                        emailBox.Folders.Add(folder);
                    }
                    Folders = new ObservableCollection<EmailFolder>(emailBox.Folders);
                });

                // Загружаем письма для INBOX
                var inboxFolder = folders.FirstOrDefault(f => f.Name == "Входящие");
                if (inboxFolder != null)
                {
                    StatusMessage = "Loading messages...";
                    var emails = await _imapService.GetEmailHeadersAsync(inboxFolder.Path);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        emailBox.UpdateCache(inboxFolder.Path, emails.ToList());
                        Emails = new ObservableCollection<Email>(emails);
                        SelectedFolder = inboxFolder;
                    });
                }

                await _imapService.DisconnectAsync();
                StatusMessage = "Initialization complete";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Initialization failed: {ex.Message}";
                throw;
            }
        }

        private void LoadEmailBoxContent()
        {
            if (SelectedEmailBox == null) return;

            Folders = new ObservableCollection<EmailFolder>(SelectedEmailBox.Folders);
            SelectedFolder = Folders.FirstOrDefault(f => f.Name == "Входящие");

            if (SelectedFolder != null && SelectedEmailBox.TryGetCachedEmails(SelectedFolder.Path, out var cachedEmails))
            {
                Emails = new ObservableCollection<Email>(cachedEmails);
            }
            else
            {
                Emails.Clear();
            }
        }

        private async Task LoadEmailsForCurrentFolderAsync(bool forceRefresh = false)
        {
            if (SelectedFolder == null || SelectedEmailBox == null) return;

            try
            {
                // Отменяем предыдущую загрузку, если она есть
                _loadingCts?.Cancel();
                _loadingCts = new CancellationTokenSource();

                IsLoading = true;
                StatusMessage = $"Loading {SelectedFolder.Name}...";

                // Проверяем кэш
                if (!forceRefresh && SelectedEmailBox.TryGetCachedEmails(SelectedFolder.Path, out var cachedEmails))
                {
                    await Task.Run(() =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Emails = new ObservableCollection<Email>(cachedEmails);
                            StatusMessage = $"Loaded {Emails.Count} emails (from cache)";
                        });
                    });
                    return;
                }

                await Task.Run(async () =>
                {
                    await _imapService.ConnectAsync(
                        SelectedEmailBox.Account.ImapServer,
                        SelectedEmailBox.Account.ImapPort,
                        SelectedEmailBox.Account.UseSsl);

                    await _imapService.AuthenticateAsync(
                        SelectedEmailBox.Account.Email,
                        SelectedEmailBox.Account.Password);

                    var emails = await _imapService.GetEmailHeadersAsync(SelectedFolder.Path);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedEmailBox.UpdateCache(SelectedFolder.Path, emails.ToList());
                        Emails = new ObservableCollection<Email>(emails);
                        StatusMessage = $"Loaded {emails.Count} emails";
                    });

                    await _imapService.DisconnectAsync();
                }, _loadingCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Загрузка была отменена
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error loading emails: {ex.Message}";
                    MessageBox.Show($"Failed to load emails: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadEmailContentAsync(Email? email)
        {
            if (email == null || SelectedEmailBox == null) return;

            try
            {
                IsContentLoading = true;

                await Task.Run(async () =>
                {
                    await _imapService.ConnectAsync(
                        SelectedEmailBox.Account.ImapServer,
                        SelectedEmailBox.Account.ImapPort,
                        SelectedEmailBox.Account.UseSsl);

                    await _imapService.AuthenticateAsync(
                        SelectedEmailBox.Account.Email,
                        SelectedEmailBox.Account.Password);

                    var fullEmail = await _imapService.GetEmailContentAsync(email.MessageId);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        email.Body = fullEmail.Body;
                        email.HtmlBody = fullEmail.HtmlBody;
                        email.Attachments = fullEmail.Attachments;
                        OnPropertyChanged(nameof(SelectedEmail));
                    });

                    await _imapService.DisconnectAsync();
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error loading email content: {ex.Message}";
                    MessageBox.Show($"Failed to load email content: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsContentLoading = false;
            }
        }

        private async Task DeleteSelectedEmailAsync()
        {
            if (SelectedEmail == null || SelectedEmailBox == null || SelectedFolder == null) return;

            try
            {
                StatusMessage = "Deleting email...";
                IsLoading = true;

                await Task.Run(async () =>
                {
                    await _imapService.ConnectAsync(
                        SelectedEmailBox.Account.ImapServer,
                        SelectedEmailBox.Account.ImapPort,
                        SelectedEmailBox.Account.UseSsl);

                    await _imapService.AuthenticateAsync(
                        SelectedEmailBox.Account.Email,
                        SelectedEmailBox.Account.Password);

                    await _imapService.DeleteEmailAsync(SelectedEmail.MessageId);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedEmailBox.RemoveFromCache(SelectedFolder.Path, SelectedEmail);
                        Emails.Remove(SelectedEmail);
                        StatusMessage = "Email deleted successfully";
                    });

                    await _imapService.DisconnectAsync();
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting email: {ex.Message}";
                MessageBox.Show($"Failed to delete email: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ComposeNewEmailAsync()
        {
            if (SelectedEmailBox == null) return;

            try
            {
                var composeWindow = new ComposeEmailWindow(SelectedEmailBox.Account);
                if (composeWindow.ShowDialog() == true)
                {
                    StatusMessage = "Sending email...";
                    IsLoading = true;

                    await Task.Run(async () =>
                    {
                        await _smtpService.ConnectAsync(
                            SelectedEmailBox.Account.SmtpServer,
                            SelectedEmailBox.Account.SmtpPort,
                            SelectedEmailBox.Account.UseSsl);

                        await _smtpService.AuthenticateAsync(
                            SelectedEmailBox.Account.Email,
                            SelectedEmailBox.Account.Password);

                        await _smtpService.SendEmailAsync(composeWindow.Email);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = "Email sent successfully";
                        });

                        await _smtpService.DisconnectAsync();

                        // Обновляем папку "Отправленные"
                        var sentFolder = Folders.FirstOrDefault(f => f.Name == "Отправленные");
                        if (sentFolder != null)
                        {
                            SelectedEmailBox.CachedEmails.Remove(sentFolder.Path);
                            if (SelectedFolder == sentFolder)
                            {
                                await LoadEmailsForCurrentFolderAsync(true);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending email: {ex.Message}";
                MessageBox.Show($"Failed to send email: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}