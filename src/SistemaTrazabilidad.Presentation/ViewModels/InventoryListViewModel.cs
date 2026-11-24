using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class InventoryListViewModel : INotifyPropertyChanged
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly InventoryService _inventoryService;
    private readonly ISessionService _sessionService;

    private ObservableCollection<Insumo> _insumos = new();
    private string _busqueda = "";
    private bool _cargando = false;
    private string _mensajeEstado = "";

    public ObservableCollection<Insumo> Insumos
    {
        get => _insumos;
        set { if (_insumos != value) { _insumos = value; OnPropertyChanged(); } }
    }

    public string Busqueda
    {
        get => _busqueda;
        set { if (_busqueda != value) { _busqueda = value; OnPropertyChanged(); } }
    }

    public bool Cargando
    {
        get => _cargando;
        set { if (_cargando != value) { _cargando = value; OnPropertyChanged(); } }
    }

    public string MensajeEstado
    {
        get => _mensajeEstado;
        set { if (_mensajeEstado != value) { _mensajeEstado = value; OnPropertyChanged(); } }
    }

    public ICommand CargarCommand { get; }
    public ICommand BuscarCommand { get; }
    public ICommand EliminarCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InventoryListViewModel(IUnitOfWork unitOfWork, InventoryService inventoryService, 
        ISessionService sessionService)
    {
        _unitOfWork = unitOfWork;
        _inventoryService = inventoryService;
        _sessionService = sessionService;

        CargarCommand = new AsyncRelayCommand(CargarInsumosAsync);
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        EliminarCommand = new AsyncRelayCommand<Insumo>(EliminarInsumoAsync);
    }

    public async Task CargarInsumosAsync()
    {
        Cargando = true;
        try
        {
            var lista = (await _inventoryService.GetStockActualAsync()).ToList();
            foreach (var insumo in lista)
            {
                insumo.UnidadMedida = InventoryDetailViewModel.NormalizarUnidadMedida(insumo.UnidadMedida);
            }
            
            Insumos.Clear();
            foreach (var insumo in lista)
            {
                Insumos.Add(insumo);
            }

            var discrepancies = lista.Count(i => i.TieneDiferenciaStock);
            MensajeEstado = discrepancies == 0
                ? $"{Insumos.Count} insumos cargados; disponibilidad calculada desde lotes."
                : $"{Insumos.Count} insumos cargados; {discrepancies} con diferencias entre stock registrado y lotes.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    public async Task BuscarAsync()
    {
        if (string.IsNullOrWhiteSpace(Busqueda))
        {
            await CargarInsumosAsync();
            return;
        }

        Cargando = true;
        try
        {
            var criterio = Busqueda.Trim();
            var lista = (await _inventoryService.GetStockActualAsync())
                .Where(i =>
                    i.Codigo.Contains(criterio, StringComparison.OrdinalIgnoreCase) ||
                    i.Nombre.Contains(criterio, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var insumo in lista)
            {
                insumo.UnidadMedida = InventoryDetailViewModel.NormalizarUnidadMedida(insumo.UnidadMedida);
            }
            
            Insumos.Clear();
            foreach (var insumo in lista)
            {
                Insumos.Add(insumo);
            }

            var discrepancies = lista.Count(i => i.TieneDiferenciaStock);
            MensajeEstado = discrepancies == 0
                ? $"{Insumos.Count} resultado(s); disponibilidad calculada desde lotes."
                : $"{Insumos.Count} resultado(s); {discrepancies} con diferencias de stock que requieren revisión.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error en búsqueda: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    public async Task EliminarInsumoAsync(Insumo? insumo)
    {
        if (insumo == null)
            return;

        var resultado = System.Windows.MessageBox.Show(
            $"¿Está seguro de eliminar el insumo '{insumo.Nombre}'?",
            "Confirmar eliminación",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (resultado != System.Windows.MessageBoxResult.Yes)
            return;

        Cargando = true;
        try
        {
            var lotes = (await _unitOfWork.Repository<Lote>()
                .FindTrackedAsync(lote => lote.InsumoId == insumo.Id)).ToList();
            var loteIds = lotes.Select(lote => lote.Id).ToHashSet();
            var producciones = (await _unitOfWork.Repository<Produccion>()
                .FindTrackedAsync(produccion =>
                    produccion.InsumoId == insumo.Id ||
                    (produccion.LoteId.HasValue && loteIds.Contains(produccion.LoteId.Value))))
                .ToList();
            var ordenesActivas = producciones
                .Where(produccion => produccion.Estado is ProductionStatus.Programada or ProductionStatus.EnProceso)
                .GroupBy(produccion => new { produccion.OrdenProduccion, produccion.Estado })
                .Select(group => group.Key)
                .ToList();

            if (ordenesActivas.Count > 0)
            {
                var ordenes = string.Join(", ", ordenesActivas.Select(orden =>
                    $"{orden.OrdenProduccion} ({orden.Estado})"));
                var mensaje = $"No se puede eliminar el insumo porque tiene órdenes programadas o en proceso: {ordenes}.";
                MensajeEstado = mensaje;
                System.Windows.MessageBox.Show(
                    mensaje,
                    "No se puede eliminar el insumo",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var compras = (await _unitOfWork.Repository<Compra>()
                .FindTrackedAsync(compra => compra.InsumoId == insumo.Id)).ToList();
            var movimientos = (await _unitOfWork.Repository<MovimientoInventario>()
                .FindTrackedAsync(movimiento =>
                    movimiento.InsumoId == insumo.Id ||
                    (movimiento.LoteId.HasValue && loteIds.Contains(movimiento.LoteId.Value))))
                .ToList();
            var registrosSanitarios = (await _unitOfWork.Repository<RegistroSanitario>()
                .FindTrackedAsync(registro => registro.InsumoId == insumo.Id)).ToList();
            var alertas = (await _unitOfWork.Repository<Alerta>()
                .FindTrackedAsync(alerta =>
                    alerta.InsumoId == insumo.Id ||
                    (alerta.LoteId.HasValue && loteIds.Contains(alerta.LoteId.Value))))
                .ToList();

            var lotesPorId = lotes.ToDictionary(lote => lote.Id);
            var detalle = new StringBuilder()
                .AppendLine($"Insumo eliminado: {insumo.Codigo} - {insumo.Nombre}")
                .AppendLine($"Nombre: {insumo.Nombre}")
                .AppendLine($"Unidad de medida: {insumo.UnidadMedida}")
                .AppendLine($"Estado: {(insumo.Activo ? "Activo" : "Inactivo")}")
                .AppendLine($"Stock mínimo: {insumo.StockMinimo:#,##0.##}")
                .AppendLine($"Stock registrado: {insumo.StockActual:#,##0.##}")
                .AppendLine($"Stock disponible: {insumo.StockDisponible:#,##0.##}");

            void AppendSection(string title, IEnumerable<string> entries)
            {
                var items = entries.ToList();
                if (items.Count == 0)
                {
                    return;
                }

                detalle.AppendLine();
                detalle.AppendLine($"{title} ({items.Count})");
                foreach (var item in items)
                {
                    detalle.AppendLine($"- {item}");
                }
            }

            AppendSection("Lotes", lotes.Select(lote =>
                $"{lote.CodigoInterno} | código anterior: {lote.CodigoLote} | proveedor: #{lote.ProveedorId} | cantidad inicial: {lote.CantidadInicial:#,##0.##} | disponible: {lote.CantidadDisponible:#,##0.##} | vence: {lote.FechaVencimiento:dd/MM/yyyy} | bloqueado: {(lote.Bloqueado ? "Sí" : "No")}"));
            AppendSection("Compras", compras.Select(compra =>
                $"OC-{compra.Id:D4} | estado: {compra.Estado} | proveedor: #{compra.ProveedorId} | cantidad: {compra.Cantidad:#,##0.##} | precio unitario: {compra.PrecioUnitario:#,##0.##} | compra: {compra.FechaCompra:dd/MM/yyyy} | entrega estimada: {compra.FechaEntregaEstimada:dd/MM/yyyy} | observaciones: {compra.Observaciones}"));
            AppendSection("Órdenes de producción", producciones.Select(produccion =>
                $"{produccion.OrdenProduccion} | estado: {produccion.Estado} | inicio: {produccion.FechaInicio:dd/MM/yyyy} | fin: {produccion.FechaFin:dd/MM/yyyy} | programado: {produccion.CantidadProgramada:#,##0.##} | consumido: {produccion.CantidadConsumida:#,##0.##} | lote: {GetLotCode(produccion.LoteId, lotesPorId)}"));
            AppendSection("Movimientos de inventario", movimientos.Select(movimiento =>
                $"{movimiento.FechaMovimiento:dd/MM/yyyy} | {movimiento.Tipo} | cantidad: {movimiento.Cantidad:#,##0.##} | stock: {movimiento.StockAnterior:#,##0.##} → {movimiento.StockPosterior:#,##0.##} | usuario: #{movimiento.UsuarioId} | referencia: {movimiento.Referencia} | comentario: {movimiento.Comentario}"));
            AppendSection("Registros sanitarios", registrosSanitarios.Select(registro =>
                $"{registro.FechaRegistro:dd/MM/yyyy} | {(registro.Aprobado ? "Aprobado" : "En revisión")} | temperatura: {registro.Temperatura} | cloro: {registro.Cloro} | pH: {registro.PH} | madurez: {registro.Madurez} | observaciones: {registro.Observaciones}"));
            AppendSection("Alertas relacionadas", alertas.Select(alerta =>
                $"{alerta.Codigo} | tipo: {alerta.Tipo} | fecha: {alerta.FechaGeneracion:dd/MM/yyyy} | estado: {(alerta.Activa ? "Activa" : "Resuelta")} | {(alerta.Leida ? "Leída" : "No leída")} | {alerta.Mensaje}"));

            var detalleAuditoria = detalle.ToString().TrimEnd();

            foreach (var lote in lotes)
            {
                lote.InsumoId = null;
                lote.Insumo = null;
                _unitOfWork.Repository<Lote>().Update(lote);
            }

            foreach (var compra in compras)
            {
                compra.InsumoId = null;
                compra.Insumo = null;
                _unitOfWork.Repository<Compra>().Update(compra);
            }

            foreach (var produccion in producciones)
            {
                produccion.InsumoId = null;
                produccion.Insumo = null;
                produccion.LoteId = null;
                produccion.Lote = null;
                _unitOfWork.Repository<Produccion>().Update(produccion);
            }

            foreach (var movimiento in movimientos)
            {
                movimiento.InsumoId = null;
                movimiento.Insumo = null;
                if (movimiento.LoteId is int loteId && loteIds.Contains(loteId))
                {
                    movimiento.LoteId = null;
                    movimiento.Lote = null;
                }
                _unitOfWork.Repository<MovimientoInventario>().Update(movimiento);
            }

            foreach (var registro in registrosSanitarios)
            {
                registro.InsumoId = null;
                registro.Insumo = null;
                _unitOfWork.Repository<RegistroSanitario>().Update(registro);
            }

            foreach (var alerta in alertas)
            {
                alerta.InsumoId = null;
                alerta.Insumo = null;
                if (alerta.LoteId is int loteId && loteIds.Contains(loteId))
                {
                    alerta.LoteId = null;
                    alerta.Lote = null;
                }
                _unitOfWork.Repository<Alerta>().Update(alerta);
            }

            await _unitOfWork.Repository<Auditoria>().AddAsync(new Auditoria
            {
                Entidad = nameof(Insumo),
                EntidadId = insumo.Id,
                Accion = "Delete",
                Detalle = detalleAuditoria,
                FechaHora = DateTime.UtcNow,
                UsuarioId = _sessionService.UsuarioActual?.Id
            });

            _unitOfWork.Repository<Insumo>().Remove(insumo);
            await _unitOfWork.CommitAsync();

            Insumos.Remove(insumo);
            MensajeEstado = "Insumo eliminado correctamente";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error al eliminar: {ex.Message}";
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Cargando = false;
        }
    }

    private static string GetLotCode(int? loteId, IReadOnlyDictionary<int, Lote> lotesPorId)
    {
        return loteId is int id && lotesPorId.TryGetValue(id, out var lote)
            ? lote.CodigoInterno
            : "sin lote asociado";
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
