using System.ComponentModel;
using System.Runtime.CompilerServices;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;
using System.Windows.Media;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AlertService _alertService;
    private readonly InventoryService _inventoryService;
    private bool _cargando;

    public IReadOnlyList<DashboardChartItem> StockChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> AlertsChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> ProductionChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> ExpiryChart { get; private set; } = [];

    public bool Cargando
    {
        get => _cargando;
        set
        {
            if (_cargando != value)
            {
                _cargando = value;
                OnPropertyChanged();
            }
        }
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
            await _alertService.RefreshCurrentAlertsAsync();

            var insumosActivos = (await _inventoryService.GetStockActualAsync())
                .Where(insumo => insumo.Activo)
                .ToList();
            var stockCritico = insumosActivos.Count(insumo =>
                insumo.StockDisponible <= insumo.StockMinimo);
            StockChart = CreateChartItems(
            [
                ("Bajo o al mínimo", stockCritico, Brushes.IndianRed),
                ("Sobre el mínimo", insumosActivos.Count - stockCritico, Brushes.SeaGreen)
            ], "Sin insumos activos");
            OnPropertyChanged(nameof(StockChart));

            var alertas = (await _alertService.GetActiveAlertsAsync()).ToList();
            AlertsChart = CreateChartItems(
                alertas.GroupBy(alerta => alerta.Tipo)
                    .Select(group => (
                        GetAlertTypeName(group.Key),
                        group.Count(),
                        GetAlertTypeColor(group.Key))),
                "Sin alertas activas");
            OnPropertyChanged(nameof(AlertsChart));

            var fechaInicio = DateTime.Today.AddDays(-6);
            var producciones = await _unitOfWork.Repository<Produccion>()
                .FindAsync(produccion => produccion.FechaInicio >= fechaInicio);
            var ordenesPorDia = producciones
                .GroupBy(produccion => produccion.OrdenProduccion, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Min(produccion => produccion.FechaInicio).Date)
                .GroupBy(fecha => fecha)
                .ToDictionary(group => group.Key, group => group.Count());
            ProductionChart = CreateChartItems(
                Enumerable.Range(0, 7).Select(offset =>
                {
                    var fecha = fechaInicio.AddDays(offset);
                    return (
                        fecha.ToString("ddd dd/MM"),
                        ordenesPorDia.GetValueOrDefault(fecha, 0),
                        (Brush)Brushes.MediumPurple);
                }),
                "Sin órdenes recientes");
            OnPropertyChanged(nameof(ProductionChart));

            var lotesDisponibles = await _unitOfWork.Repository<Lote>()
                .FindAsync(lote => lote.CantidadDisponible > 0 && !lote.Bloqueado);
            var vencimientos = lotesDisponibles
                .GroupBy(lote =>
                {
                    var diasRestantes = (lote.FechaVencimiento.Date - DateTime.Today).Days;
                    return diasRestantes switch
                    {
                        < 0 => "Vencidos",
                        <= 7 => "0-7 días",
                        <= 14 => "8-14 días",
                        _ => "Más de 14 días"
                    };
                })
                .ToDictionary(group => group.Key, group => group.Count());
            ExpiryChart = CreateChartItems(
            [
                ("Vencidos", vencimientos.GetValueOrDefault("Vencidos"), Brushes.IndianRed),
                ("0-7 días", vencimientos.GetValueOrDefault("0-7 días"), Brushes.DarkOrange),
                ("8-14 días", vencimientos.GetValueOrDefault("8-14 días"), Brushes.SteelBlue),
                ("Más de 14 días", vencimientos.GetValueOrDefault("Más de 14 días"), Brushes.SeaGreen)
            ], "Sin lotes disponibles");
            OnPropertyChanged(nameof(ExpiryChart));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error cargando indicadores: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Cargando = false;
        }
    }

    private static IReadOnlyList<DashboardChartItem> CreateChartItems(
        IEnumerable<(string Label, int Count, Brush Color)> values,
        string emptyLabel)
    {
        var populated = values.Where(value => value.Count > 0).ToList();
        if (populated.Count == 0)
        {
            return [new DashboardChartItem(emptyLabel, 0, 0, Brushes.LightGray)];
        }

        var maxCount = populated.Max(value => value.Count);
        return populated.Select(value => new DashboardChartItem(
            value.Label,
            value.Count,
            value.Count * 100d / maxCount,
            value.Color)).ToList();
    }

    private static string GetAlertTypeName(AlertType type)
    {
        return type switch
        {
            AlertType.StockMinimo => "Stock mínimo",
            AlertType.Vencimiento => "Vencimiento",
            AlertType.Anomalia => "Anomalía",
            AlertType.Sanidad => "Sanidad",
            AlertType.Produccion => "Producción",
            _ => type.ToString()
        };
    }

    private static Brush GetAlertTypeColor(AlertType type)
    {
        return type switch
        {
            AlertType.StockMinimo => Brushes.DarkOrange,
            AlertType.Vencimiento => Brushes.SteelBlue,
            AlertType.Anomalia => Brushes.MediumPurple,
            AlertType.Sanidad => Brushes.Teal,
            AlertType.Produccion => Brushes.DarkSlateBlue,
            _ => Brushes.Gray
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record DashboardChartItem(string Label, int Count, double Percentage, Brush Color);
