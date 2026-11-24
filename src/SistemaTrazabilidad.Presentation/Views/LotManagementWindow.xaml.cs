using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class LotManagementWindow : Window
{
    private readonly Insumo _insumo;
    private readonly LotManagementViewModel _viewModel;

    public LotManagementWindow(Insumo insumo)
    {
        InitializeComponent();
        _insumo = insumo;

        var unitOfWork = App.Services.GetRequiredService<IUnitOfWork>();
        var inventoryService = App.Services.GetRequiredService<InventoryService>();
        _viewModel = new LotManagementViewModel(insumo, unitOfWork, inventoryService);
        DataContext = _viewModel;

        Title = $"Lotes de {insumo.Nombre}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.CargarLotesAsync();
    }

    private async void RegistrarEntrada_Click(object sender, RoutedEventArgs e)
    {
        var window = new RegisterLotWindow(_insumo)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            await _viewModel.CargarLotesAsync();
        }
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();
}
