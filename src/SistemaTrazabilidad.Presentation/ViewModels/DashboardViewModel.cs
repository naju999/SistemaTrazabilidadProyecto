using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AlertService _alertService;
    private readonly InventoryService _inventoryService;

    private int _stockCriticoCount;
    private int _alertasActivasCount;
    private int _produccionRecienteCount;
    private int _vencimientosProximosCount;
    private bool _cargando;

    public int StockCriticoCount
    {
        get => _stockCriticoCount;
        set { if (_stockCriticoCount != value) { _stockCriticoCount = value; OnPropertyChanged(); } }
    }

    public int AlertasActivasCount
    {
        get => _alertasActivasCount;
        set { if (_alertasActivasCount != value) { _alertasActivasCount = value; OnPropertyChanged(); } }
    }

    public int ProduccionRecienteCount
    {
        get => _produccionRecienteCount;
        set { if (_produccionRecienteCount != value) { _produccionRecienteCount = value; OnPropertyChanged(); } }
    }

    public int VencimientosProximosCount
    {
        get => _vencimientosProximosCount;
        set { if (_vencimientosProximosCount != value) { _vencimientosProximosCount = value; OnPropertyChanged(); } }
    }

    public bool Cargando
    {
        get => _cargando;
        set { if (_cargando != value) { _cargando = value; OnPropertyChanged(); } }
    }

    public DashboardViewModel(IUnitOfWork unitOfWork, AlertService alertService, InventoryService inventoryService)
    {
        _unitOfWork = unitOfWork;
        _alertService = alertService;
        _inventoryService = inventoryService;
    }

    public async Task CargarIndicadoresAsync()
    {
        Cargando = true;
        try
        {
            // Stock crítico
            var insumos = await _inventoryService.GetStockActualAsync();
            StockCriticoCount = insumos.Count(i => i.Activo && i.StockDisponible <= i.StockMinimo);

            // Alertas activas
            var alertas = await _alertService.GetActiveAlertsAsync();
            AlertasActivasCount = ((System.Collections.Generic.IEnumerable<Alerta>)alertas).Count();

            // Producción reciente (últimos 7 días)
            var producciones = await _unitOfWork.Repository<Produccion>().FindAsync(p => p.FechaInicio >= DateTime.Now.AddDays(-7));
            ProduccionRecienteCount = ((System.Collections.Generic.IEnumerable<Produccion>)producciones).Count();

            // Vencimientos próximos (próximos 14 días)
            var lotes = await _unitOfWork.Repository<Lote>().FindAsync(l => 
                l.FechaVencimiento <= DateTime.Now.AddDays(14) && 
                l.FechaVencimiento > DateTime.Now && 
                l.CantidadDisponible > 0);
            VencimientosProximosCount = ((System.Collections.Generic.IEnumerable<Lote>)lotes).Count();

        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error cargando indicadores: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Cargando = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
