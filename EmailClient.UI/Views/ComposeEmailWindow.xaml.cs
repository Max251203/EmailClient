using EmailClient.Core.Models;
using EmailClient.UI.ViewModels;
using System.Windows;

namespace EmailClient.UI.Views
{
    public partial class ComposeEmailWindow : Window
    {
        private readonly ComposeEmailViewModel _viewModel;

        public ComposeEmailWindow(EmailAccount account)
        {
            InitializeComponent();
            _viewModel = new ComposeEmailViewModel(account);
            _viewModel.EmailSent += (s, e) => DialogResult = true;
            DataContext = _viewModel;
        }

        public Email Email => _viewModel.GetEmail();
    }
}