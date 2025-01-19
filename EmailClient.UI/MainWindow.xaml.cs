using EmailClient.Core.Models;
using EmailClient.UI.ViewModels;
using EmailClient.UI.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace EmailClient.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded;
        }


        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.IsLoading = true;
                _viewModel.StatusMessage = "Initializing email client...";

                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize email client: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                _viewModel.IsLoading = false;
            }
        }

        private void Folder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is EmailFolder folder)
            {
                _viewModel.SelectedFolder = folder;
                e.Handled = true;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            var result = MessageBox.Show(
                "Are you sure you want to exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }
    }
}