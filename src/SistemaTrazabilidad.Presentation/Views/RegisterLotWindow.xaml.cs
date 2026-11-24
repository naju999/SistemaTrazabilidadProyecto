using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class RegisterLotWindow : Window
{
    public RegisterLotWindow(Insumo insumo)
    {
        InitializeComponent();

        var inventoryService = App.Services.GetRequiredService<InventoryService>();
        var sessionService = App.Services.GetRequiredService<ISessionService>();
        var unitOfWork = App.Services.GetRequiredService<IUnitOfWork>();
        var viewModel = new RegisterLotViewModel(insumo, inventoryService, sessionService, unitOfWork);
        viewModel.GuardadoExitoso += (_, _) =>
        {
            DialogResult = true;
        };
        DataContext = viewModel;
        Title = $"Registrar lote para {insumo.Nombre}";
        Loaded += async (_, _) => await viewModel.CargarProveedoresAsync();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
