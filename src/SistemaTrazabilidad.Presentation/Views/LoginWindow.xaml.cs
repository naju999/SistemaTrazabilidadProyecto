using System.Windows;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginExitoso += (s, e) =>
        {
            MainWindow mainWindow = App.Services.GetService(typeof(Views.MainWindow)) as Views.MainWindow
                ?? throw new InvalidOperationException("MainWindow no configurada en DI");
            mainWindow.Show();
            this.Close();
        };

        Loaded += (s, e) =>
        {
            PasswordBox.Focus();
        };

        PasswordBox.PasswordChanged += (s, e) =>
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        };
    }

    private void CerrarPrograma_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}
