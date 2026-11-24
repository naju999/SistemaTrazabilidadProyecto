using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public abstract class OperationalModuleViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Observe<T>(ObservableCollection<T> collection, params string[] properties)
    {
        collection.CollectionChanged += (_, _) =>
        {
            foreach (var property in properties)
            {
                OnPropertyChanged(property);
            }
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class SupplierModuleViewModel : OperationalModuleViewModel
{
    private readonly SupplierService _supplierService;

    public ObservableCollection<SupplierRow> Suppliers { get; } = [];
    public int ActiveCount => Suppliers.Count(s => s.Entity.Activo);
    public int InactiveCount => Suppliers.Count(s => !s.Entity.Activo);

    public SupplierModuleViewModel(SupplierService supplierService)
    {
        _supplierService = supplierService;
        Observe(Suppliers, nameof(ActiveCount), nameof(InactiveCount));
    }

    public async Task LoadAsync()
    {
        var suppliers = await _supplierService.GetAllProveedoresAsync();
        Replace(Suppliers, suppliers.Select(s => new SupplierRow(
            s.Id,
            s.Codigo,
            s.Nombre,
            s.Telefono,
            s.Email,
            s.Activo ? "Activo" : "Inactivo",
            s)));
    }

    public async Task SaveAsync(
        Proveedor? existing,
        string nombre,
        string telefono,
        string email,
        bool activo)
    {
        if (existing is null)
        {
            await _supplierService.CreateProveedorAsync(nombre, telefono, email);
        }
        else
        {
            await _supplierService.UpdateProveedorAsync(
                existing.Id, nombre, telefono, email, activo);
        }

        await LoadAsync();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed class ProductionModuleViewModel : OperationalModuleViewModel
{
    private readonly ProductionService _productionService;
    private readonly LotService _lotService;
    private readonly InventoryService _inventoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionService _sessionService;

    public ObservableCollection<ProductionRow> ProductionOrders { get; } = [];
    public IReadOnlyList<Insumo> AllInputs { get; private set; } = [];
    public IReadOnlyList<Insumo> Inputs { get; private set; } = [];
    public IReadOnlyList<Lote> AllLots { get; private set; } = [];
    public IReadOnlyList<Lote> Lots { get; private set; } = [];
    public int ProgrammedCount => ProductionOrders.Count(p => p.Lines.All(
        line => line.Estado == ProductionStatus.Programada));
    public int InProcessCount => ProductionOrders.Count(p => p.Lines.All(
        line => line.Estado == ProductionStatus.EnProceso));
    public int FinishedCount => ProductionOrders.Count(p => p.Lines.All(
        line => line.Estado == ProductionStatus.Finalizada));

    public ProductionModuleViewModel(
        ProductionService productionService,
        LotService lotService,
        InventoryService inventoryService,
        IUnitOfWork unitOfWork,
        ISessionService sessionService)
    {
        _productionService = productionService;
        _lotService = lotService;
        _inventoryService = inventoryService;
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        Observe(ProductionOrders, nameof(ProgrammedCount), nameof(InProcessCount),
            nameof(FinishedCount));
    }

    public async Task LoadAsync(string? search = null, ProductionStatus? status = null)
    {
        AllInputs = (await _inventoryService.GetStockActualAsync()).ToList();
        Inputs = AllInputs.Where(i => i.Activo).ToList();
        AllLots = (await _unitOfWork.Repository<Lote>().GetAllAsync()).ToList();
        Lots = AllLots
            .Where(l => !l.Bloqueado && !l.EstaVencido && l.CantidadDisponible > 0)
            .OrderBy(l => l.FechaIngreso).ToList();

        var inputNames = AllInputs.ToDictionary(i => i.Id, i => i.Nombre);
        var lotCodes = AllLots.ToDictionary(l => l.Id, l => l.CodigoInterno);
        var productions = await _unitOfWork.Repository<Produccion>().GetAllAsync();

        IEnumerable<ProductionRow> rows = productions
            .GroupBy(production => production.OrdenProduccion)
            .Select(group =>
            {
                var lines = group.OrderBy(production => production.Id).ToList();
                var representative = lines[0];
                var states = lines.Select(line => line.Estado).Distinct().ToList();
                var state = states.Count == 1 ? states[0].ToString() : "Inconsistente";
                var inputDescriptions = lines.Select(line =>
                {
                    var inputName = line.InsumoId is int inputId &&
                                    inputNames.TryGetValue(inputId, out var name)
                        ? name
                        : "Insumo eliminado";
                    return $"{inputName} ({line.CantidadProgramada})";
                });
                var lotDescriptions = lines.Select(line =>
                    line.LoteId is int lotId && lotCodes.TryGetValue(lotId, out var code)
                        ? code
                        : "Lote eliminado");

                return new ProductionRow(
                    representative.Id,
                    representative.OrdenProduccion,
                    representative.FechaInicio.ToString("d"),
                    string.Join(", ", inputDescriptions),
                    string.Join(", ", lotDescriptions),
                    lines.Sum(line => line.CantidadProgramada),
                    lines.Sum(line => line.CantidadConsumida),
                    state,
                    lines);
            })
            .OrderByDescending(row => row.Lines[0].FechaInicio)
            .ThenByDescending(row => row.Id);
        if (status.HasValue)
        {
            rows = rows.Where(row => row.Lines.All(line => line.Estado == status.Value));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(p =>
                p.Orden.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Producto.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.LotePeps.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Replace(ProductionOrders, rows);
    }

    public async Task CreateAsync(
        DateTime startDate,
        IReadOnlyList<ProductionOrderLine> lines)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.CreateOrdenProduccionAsync(
            startDate, lines, userId);
        await LoadAsync();
    }

    public async Task StartAsync(Produccion production)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.IniciarProduccionAsync(production.Id, userId);
        await LoadAsync();
    }

    public async Task FinishAsync(
        Produccion production,
        IReadOnlyDictionary<int, decimal> consumedQuantities)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.FinalizarProduccionAsync(
            production.Id, consumedQuantities, userId);
        await LoadAsync();
    }

    public async Task CancelAsync(Produccion production)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.CancelarProduccionAsync(production.Id, userId);
        await LoadAsync();
    }

    public Task<Lote> SelectPepsLotAsync(int inputId, decimal quantity)
    {
        return _lotService.SeleccionarLotePEPSAsync(inputId, quantity);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed class AlertModuleViewModel : OperationalModuleViewModel
{
    private readonly AlertService _alertService;
    private readonly IUnitOfWork _unitOfWork;

    public ObservableCollection<AlertRow> Alerts { get; } = [];
    public int ActiveCount => Alerts.Count(a => a.Entity.Activa);
    public int UnreadCount => Alerts.Count(a => !a.Entity.Leida);
    public int ResolvedCount => Alerts.Count(a => !a.Entity.Activa);
    public int TotalCount => Alerts.Count;

    public AlertModuleViewModel(AlertService alertService, IUnitOfWork unitOfWork)
    {
        _alertService = alertService;
        _unitOfWork = unitOfWork;
        Observe(Alerts, nameof(ActiveCount), nameof(UnreadCount),
            nameof(ResolvedCount), nameof(TotalCount));
    }

    public async Task LoadAsync(string? type = null, string? status = null, bool unreadOnly = false)
    {
        var alerts = await _unitOfWork.Repository<Alerta>().GetAllAsync();
        var filtered = alerts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            filtered = filtered.Where(a => a.Tipo.ToString() == type);
        }
        if (string.Equals(status, "Activas", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(a => a.Activa);
        }
        else if (string.Equals(status, "Resueltas", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(a => !a.Activa);
        }
        if (unreadOnly)
        {
            filtered = filtered.Where(a => !a.Leida);
        }

        Replace(Alerts, filtered.OrderByDescending(a => a.FechaGeneracion)
            .Select(a => new AlertRow(
                a.Id,
                a.FechaGeneracion.ToString("g"),
                "—",
                a.Tipo.ToString(),
                a.Codigo,
                a.Mensaje,
                a.Activa ? "Activa" : "Resuelta",
                a)));
    }

    public async Task GenerateCurrentAlertsAsync()
    {
        await _alertService.GenerateStockAlertsAsync();
        await _alertService.GenerateExpiryAlertsAsync();
        await LoadAsync();
    }

    public async Task ResolveAsync(Alerta alert)
    {
        await _alertService.ResolveAlertAsync(alert.Id);
        await LoadAsync();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed class AuditModuleViewModel : OperationalModuleViewModel
{
    private readonly IUnitOfWork _unitOfWork;

    public ObservableCollection<AuditRow> AuditEvents { get; } = [];

    public AuditModuleViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LoadAsync(
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
        string? action = null)
    {
        var records = await _unitOfWork.Repository<Auditoria>().GetAllAsync();
        IEnumerable<Auditoria> filtered = records;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(a =>
                a.Entidad.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Accion.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Detalle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (a.UsuarioId?.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (from.HasValue)
        {
            filtered = filtered.Where(a => a.FechaHora.Date >= from.Value.Date);
        }
        if (to.HasValue)
        {
            filtered = filtered.Where(a => a.FechaHora.Date <= to.Value.Date);
        }
        if (!string.IsNullOrWhiteSpace(action) &&
            !string.Equals(action, "Todas", StringComparison.OrdinalIgnoreCase))
        {
            filtered = action switch
            {
                "Creación" => filtered.Where(a =>
                    a.Accion.Contains("crear", StringComparison.OrdinalIgnoreCase) ||
                    a.Accion.Contains("registrar", StringComparison.OrdinalIgnoreCase)),
                "Actualización" => filtered.Where(a =>
                    a.Accion.Contains("actualizar", StringComparison.OrdinalIgnoreCase)),
                "Eliminación" => filtered.Where(a =>
                    a.Accion.Contains("eliminar", StringComparison.OrdinalIgnoreCase) ||
                    a.Accion.Contains("delete", StringComparison.OrdinalIgnoreCase)),
                "Bloqueo de lote" => filtered.Where(a =>
                    a.Accion.Contains("bloqueo", StringComparison.OrdinalIgnoreCase)),
                _ => filtered
            };
        }

        Replace(AuditEvents, filtered.OrderByDescending(a => a.FechaHora)
            .Select(a => new AuditRow(
                a.FechaHora.ToString("g"),
                a.UsuarioId?.ToString() ?? "Sistema",
                a.Entidad,
                a.Entidad,
                a.EntidadId.ToString(),
                a.Accion,
                a.Detalle)));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed record SupplierRow(
    int Id,
    string Codigo,
    string Proveedor,
    string Telefono,
    string Correo,
    string Estado,
    Proveedor Entity);

public sealed record ProductionRow(
    int Id,
    string Orden,
    string FechaInicio,
    string Producto,
    string LotePeps,
    decimal Programado,
    decimal Consumido,
    string Estado,
    IReadOnlyList<Produccion> Lines)
{
    public Produccion Entity => Lines[0];

    public bool CanStart => Lines.All(line =>
        line.Estado == ProductionStatus.Programada &&
        line.InsumoId.HasValue &&
        line.LoteId.HasValue);

    public bool CanFinish => Lines.All(line =>
        line.Estado == ProductionStatus.EnProceso &&
        line.InsumoId.HasValue &&
        line.LoteId.HasValue);

    public bool CanCancel => Lines.All(line =>
        (line.Estado is ProductionStatus.Programada or ProductionStatus.EnProceso) &&
        line.InsumoId.HasValue &&
        line.LoteId.HasValue);
}

public sealed record AlertRow(
    int Id,
    string Fecha,
    string Prioridad,
    string Tipo,
    string Codigo,
    string Mensaje,
    string Estado,
    Alerta Entity);

public sealed record AuditRow(
    string FechaHora,
    string Usuario,
    string Modulo,
    string Entidad,
    string Registro,
    string Accion,
    string Detalle);
