using EmailClient.Core.Models;
using EmailClient.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EmailClient.UI.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        private bool _isPasswordVisible;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            _viewModel.LoginSuccessful += (s, e) => DialogResult = true;
            DataContext = _viewModel;

            PasswordBox.PasswordChanged += (s, e) => _viewModel.Password = PasswordBox.Password;
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            if (_isPasswordVisible)
            {
                PasswordText.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordText.Visibility = Visibility.Visible;
                ShowPasswordIcon.Text = "🔒";
            }
            else
            {
                PasswordBox.Password = PasswordText.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordText.Visibility = Visibility.Collapsed;
                ShowPasswordIcon.Text = "👁";
            }
        }

        public EmailAccount? EmailAccount => _viewModel.EmailAccount;
    }
}