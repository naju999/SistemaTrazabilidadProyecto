using System;
using System.Windows;
using System.Windows.Controls;
using SistemaTrazabilidad.Application.Services;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly ISessionService _sessionService;

    public MainWindow(ISessionService sessionService)
    {
        _sessionService = sessionService;
        InitializeComponent();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (!_sessionService.EstaAutenticado)
        {
            LoginWindow loginWindow = new(App.Services.GetService(typeof(ViewModels.LoginViewModel)) as ViewModels.LoginViewModel
                ?? throw new InvalidOperationException("LoginViewModel no configurada en DI"));
            loginWindow.Show();
            this.Close();
            return;
        }

        var usuario = _sessionService.UsuarioActual;
        UsuarioTextBlock.Text = $"Bienvenido: {usuario?.NombreCompleto}";

        MostrarDashboard();
    }

    private void NavMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            switch (tag)
            {
                case "Dashboard":
                    MostrarDashboard();
                    break;
                case "Inventory":
                    MostrarInventory();
                    break;
                case "Suppliers":
                    MainContent.Content = new SuppliersView();
                    break;
                case "Production":
                    MainContent.Content = new ProductionView();
                    break;
                case "Alerts":
                    MainContent.Content = new AlertsView();
                    break;
                case "Audit":
                    MainContent.Content = new AuditView();
                    break;
            }
        }
    }

    private void MostrarDashboard()
    {
        var dashboard = new Dashboard();
        var dashboardVM = App.Services.GetService(typeof(ViewModels.DashboardViewModel)) as ViewModels.DashboardViewModel;
        dashboard.DataContext = dashboardVM;
        MainContent.Content = dashboard;
    }

    private void MostrarInventory()
    {
        var inventoryView = new InventoryListView();
        var inventoryVM = App.Services.GetService(typeof(ViewModels.InventoryListViewModel)) as ViewModels.InventoryListViewModel;
        inventoryView.DataContext = inventoryVM;
        MainContent.Content = inventoryView;
    }

    private void MostrarVista(string titulo, string descripcion)
    {
        var stackPanel = new StackPanel { Margin = new Thickness(30) };
        
        var tituloBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 102, 204)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(15, 10, 15, 10)
        };

        var tituloText = new TextBlock
        {
            Text = titulo,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White
        };
        tituloBorder.Child = tituloText;
        stackPanel.Children.Add(tituloBorder);

        var descripcionText = new TextBlock
        {
            Text = descripcion,
            FontSize = 12,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 15, 0, 0)
        };
        stackPanel.Children.Add(descripcionText);

        var vistaEnConstText = new TextBlock
        {
            Text = "Esta vista está en construcción. Próximamente disponible.",
            FontSize = 14,
            Margin = new Thickness(0, 30, 0, 0),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 100, 0))
        };
        stackPanel.Children.Add(vistaEnConstText);

        var scroll = new ScrollViewer
        {
            Content = stackPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        MainContent.Content = scroll;
    }

    private void CerrarSesion_Click(object sender, RoutedEventArgs e)
    {
        _sessionService.CerrarSesion();
        LoginWindow loginWindow = new(App.Services.GetService(typeof(ViewModels.LoginViewModel)) as ViewModels.LoginViewModel
            ?? throw new InvalidOperationException("LoginViewModel no configurada en DI"));
        loginWindow.Show();
        this.Close();
    }
}

