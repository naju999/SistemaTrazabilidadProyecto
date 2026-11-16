using System.Windows;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class InventoryDetailWindow : Window
{
    private readonly InventoryDetailViewModel _viewModel;

    public InventoryDetailWindow(Insumo? insumo = null)
    {
        InitializeComponent();
        
        _viewModel = new InventoryDetailViewModel(
            App.Services.GetService(typeof(Persistence.Repositories.IUnitOfWork)) as Persistence.Repositories.IUnitOfWork 
                ?? throw new InvalidOperationException("IUnitOfWork no encontrado"),
            App.Services.GetService(typeof(Application.Services.AuditService)) as Application.Services.AuditService 
                ?? throw new InvalidOperationException("AuditService no encontrado"),
            App.Services.GetService(typeof(Application.Services.ISessionService)) as Application.Services.ISessionService 
                ?? throw new InvalidOperationException("ISessionService no encontrado")
        );

        DataContext = _viewModel;

        if (insumo != null)
        {
            _viewModel.CargarInsumo(insumo);
        }

        _viewModel.GuardoExitoso += (s, e) =>
        {
            MessageBox.Show("Insumo guardado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        };
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
