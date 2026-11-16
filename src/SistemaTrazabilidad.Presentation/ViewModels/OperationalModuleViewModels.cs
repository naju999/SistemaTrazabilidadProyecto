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
    public string AverageDeliveryDays => Suppliers.Count == 0
        ? "—"
        : $"{Suppliers.Average(s => s.Entity.TiempoEntregaDias):0.#} días";

    public SupplierModuleViewModel(SupplierService supplierService)
    {
        _supplierService = supplierService;
        Observe(Suppliers, nameof(ActiveCount), nameof(InactiveCount), nameof(AverageDeliveryDays));
    }

    public async Task LoadAsync(string? search = null, bool? active = null)
    {
        var suppliers = await _supplierService.GetAllProveedoresAsync();
        IEnumerable<Proveedor> filtered = suppliers;
        if (active.HasValue)
        {
            filtered = filtered.Where(s => s.Activo == active.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(s =>
                s.Codigo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Telefono.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Replace(Suppliers, filtered.Select(s => new SupplierRow(
            s.Id,
            s.Codigo,
            s.Nombre,
            s.Telefono,
            s.Email,
            $"{s.TiempoEntregaDias} días",
            s.Activo ? "Activo" : "Inactivo",
            s)));
    }

    public async Task SaveAsync(
        Proveedor? existing,
        string codigo,
        string nombre,
        string telefono,
        string email,
        int tiempoEntregaDias,
        bool activo)
    {
        if (existing is null)
        {
            await _supplierService.CreateProveedorAsync(codigo, nombre, telefono, email, tiempoEntregaDias);
        }
        else
        {
            await _supplierService.UpdateProveedorAsync(
                existing.Id, nombre, telefono, email, tiempoEntregaDias, activo);
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

public sealed class PurchaseModuleViewModel : OperationalModuleViewModel
{
    private readonly PurchaseService _purchaseService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionService _sessionService;

    public ObservableCollection<PurchaseRow> Purchases { get; } = [];
    public IReadOnlyList<Proveedor> Suppliers { get; private set; } = [];
    public IReadOnlyList<Insumo> Inputs { get; private set; } = [];
    public int OrdersCount => Purchases.Count;
    public int PendingCount => Purchases.Count(p => p.Entity.Estado == PurchaseStatus.Pendiente);
    public int ReceivedCount => Purchases.Count(p => p.Entity.Estado == PurchaseStatus.Recibida);
    public int PurchasesWithLotsCount => Purchases.Count(p => p.LotePeps != "—");

    public PurchaseModuleViewModel(
        PurchaseService purchaseService,
        IUnitOfWork unitOfWork,
        ISessionService sessionService)
    {
        _purchaseService = purchaseService;
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        Observe(Purchases, nameof(OrdersCount), nameof(PendingCount),
            nameof(ReceivedCount), nameof(PurchasesWithLotsCount));
    }

    public async Task LoadAsync(
        string? search = null,
        PurchaseStatus? status = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        Suppliers = (await _unitOfWork.Repository<Proveedor>().GetAllAsync())
            .Where(s => s.Activo).ToList();
        Inputs = (await _unitOfWork.Repository<Insumo>().GetAllAsync())
            .Where(i => i.Activo).ToList();

        var supplierNames = Suppliers.ToDictionary(s => s.Id, s => s.Nombre);
        var inputNames = Inputs.ToDictionary(i => i.Id, i => i.Nombre);
        var purchases = await _unitOfWork.Repository<Compra>().GetAllAsync();

        IEnumerable<PurchaseRow> rows = purchases
            .OrderByDescending(p => p.FechaCompra)
            .Select(p => new PurchaseRow(
                p.Id,
                $"OC-{p.Id:D4}",
                p.FechaCompra.ToString("d"),
                supplierNames.GetValueOrDefault(p.ProveedorId, $"Proveedor #{p.ProveedorId}"),
                inputNames.GetValueOrDefault(p.InsumoId, $"Insumo #{p.InsumoId}"),
                "—",
                $"{p.Cantidad} {Inputs.FirstOrDefault(i => i.Id == p.InsumoId)?.UnidadMedida}",
                p.Total.ToString("C"),
                p.Estado.ToString(),
                p));
        if (status.HasValue)
        {
            rows = rows.Where(p => p.Entity.Estado == status.Value);
        }
        if (from.HasValue)
        {
            rows = rows.Where(p => p.Entity.FechaCompra.Date >= from.Value.Date);
        }
        if (to.HasValue)
        {
            rows = rows.Where(p => p.Entity.FechaCompra.Date <= to.Value.Date);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(p =>
                p.Orden.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Proveedor.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Insumo.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Replace(Purchases, rows);
    }

    public async Task CreateAsync(
        Proveedor supplier,
        Insumo input,
        DateTime purchaseDate,
        decimal quantity,
        decimal unitPrice,
        DateTime estimatedDelivery,
        string notes)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _purchaseService.CreateOrdenCompraAsync(
            supplier.Id, input.Id, purchaseDate, quantity, unitPrice,
            estimatedDelivery, notes, userId);
        await LoadAsync();
    }

    public async Task ReceiveAsync(Compra purchase)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _purchaseService.RecibirOrdenCompraAsync(purchase.Id, userId);
        await LoadAsync();
    }

    public async Task CancelAsync(Compra purchase)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _purchaseService.CancelarOrdenCompraAsync(purchase.Id, userId);
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionService _sessionService;

    public ObservableCollection<ProductionRow> ProductionOrders { get; } = [];
    public IReadOnlyList<Insumo> Inputs { get; private set; } = [];
    public IReadOnlyList<Lote> Lots { get; private set; } = [];
    public int ProgrammedCount => ProductionOrders.Count(p => p.Entity.Estado == ProductionStatus.Programada);
    public int InProcessCount => ProductionOrders.Count(p => p.Entity.Estado == ProductionStatus.EnProceso);
    public int FinishedCount => ProductionOrders.Count(p => p.Entity.Estado == ProductionStatus.Finalizada);
    public int CriticalInputsCount => Inputs.Count(i => i.StockActual <= i.StockMinimo);

    public ProductionModuleViewModel(
        ProductionService productionService,
        LotService lotService,
        IUnitOfWork unitOfWork,
        ISessionService sessionService)
    {
        _productionService = productionService;
        _lotService = lotService;
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        Observe(ProductionOrders, nameof(ProgrammedCount), nameof(InProcessCount),
            nameof(FinishedCount));
    }

    public async Task LoadAsync(string? search = null, ProductionStatus? status = null)
    {
        Inputs = (await _unitOfWork.Repository<Insumo>().GetAllAsync())
            .Where(i => i.Activo).ToList();
        var allLots = await _unitOfWork.Repository<Lote>().GetAllAsync();
        Lots = allLots
            .Where(l => !l.Bloqueado && !l.EstaVencido && l.CantidadDisponible > 0)
            .OrderBy(l => l.FechaIngreso).ToList();

        var inputNames = Inputs.ToDictionary(i => i.Id, i => i.Nombre);
        var lotCodes = allLots.ToDictionary(l => l.Id, l => l.CodigoLote);
        var productions = await _unitOfWork.Repository<Produccion>().GetAllAsync();

        IEnumerable<ProductionRow> rows = productions
            .OrderByDescending(p => p.FechaInicio)
            .Select(p => new ProductionRow(
                p.Id,
                p.OrdenProduccion,
                p.FechaInicio.ToString("d"),
                inputNames.GetValueOrDefault(p.InsumoId, $"Insumo #{p.InsumoId}"),
                lotCodes.GetValueOrDefault(p.LoteId, $"Lote #{p.LoteId}"),
                p.CantidadProgramada,
                p.CantidadConsumida,
                p.Estado.ToString(),
                p));
        if (status.HasValue)
        {
            rows = rows.Where(p => p.Entity.Estado == status.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            rows = rows.Where(p =>
                p.Orden.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Producto.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.LotePeps.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Replace(ProductionOrders, rows);
        OnPropertyChanged(nameof(CriticalInputsCount));
    }

    public async Task CreateAsync(
        Insumo input,
        Lote lot,
        string orderNumber,
        DateTime startDate,
        decimal plannedQuantity)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.CreateOrdenProduccionAsync(
            input.Id, lot.Id, orderNumber, startDate, plannedQuantity, userId);
        await LoadAsync();
    }

    public async Task StartAsync(Produccion production)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.IniciarProduccionAsync(production.Id, userId);
        await LoadAsync();
    }

    public async Task FinishAsync(Produccion production, decimal consumedQuantity)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _productionService.FinalizarProduccionAsync(
            production.Id, consumedQuantity, userId);
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

public sealed class SanitaryModuleViewModel : OperationalModuleViewModel
{
    private readonly SanitaryService _sanitaryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionService _sessionService;

    public ObservableCollection<SanitaryRow> SanitaryRecords { get; } = [];
    public IReadOnlyList<Insumo> Inputs { get; private set; } = [];
    public int ApprovedCount => SanitaryRecords.Count(r => r.Resultado == "Aprobado");
    public int ReviewCount => SanitaryRecords.Count(r => r.Resultado != "Aprobado");
    public int RecentCount => SanitaryRecords.Count(r =>
        r.RecordedAt.Date >= DateTime.Today.AddDays(-7));

    public SanitaryModuleViewModel(
        SanitaryService sanitaryService,
        IUnitOfWork unitOfWork,
        ISessionService sessionService)
    {
        _sanitaryService = sanitaryService;
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        Observe(SanitaryRecords, nameof(ApprovedCount), nameof(ReviewCount),
            nameof(RecentCount));
    }

    public async Task LoadAsync(string? search = null, string? status = null, DateTime? date = null)
    {
        Inputs = (await _unitOfWork.Repository<Insumo>().GetAllAsync())
            .Where(i => i.Activo).ToList();
        var inputNames = Inputs.ToDictionary(i => i.Id, i => i.Nombre);
        var records = await _unitOfWork.Repository<RegistroSanitario>().GetAllAsync();

        var filtered = records.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(r =>
                inputNames.GetValueOrDefault(r.InsumoId, string.Empty)
                    .Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Observaciones.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (string.Equals(status, "Aprobado", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r => r.Aprobado);
        }
        else if (string.Equals(status, "Revisión", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(r => !r.Aprobado);
        }
        if (date.HasValue)
        {
            filtered = filtered.Where(r => r.FechaRegistro.Date == date.Value.Date);
        }

        Replace(SanitaryRecords, filtered.OrderByDescending(r => r.FechaRegistro)
            .Select(r => new SanitaryRow(
                r.Id,
                r.FechaRegistro,
                r.FechaRegistro.ToString("d"),
                inputNames.GetValueOrDefault(r.InsumoId, $"Insumo #{r.InsumoId}"),
                r.Temperatura.ToString("0.##"),
                r.Cloro.ToString("0.##"),
                r.PH.ToString("0.##"),
                r.Madurez.ToString("0.##"),
                r.Aprobado ? "Aprobado" : "Revisión",
                r.Observaciones)));
    }

    public async Task CreateAsync(
        Insumo input,
        DateTime date,
        decimal temperature,
        decimal chlorine,
        decimal ph,
        decimal maturity,
        string notes)
    {
        var userId = _sessionService.UsuarioActual?.Id
            ?? throw new InvalidOperationException("No hay una sesión de usuario activa.");

        await _sanitaryService.RegistrarRegistroSanitarioAsync(
            input.Id, date, temperature, chlorine, ph, maturity, notes, userId);
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

public sealed record SupplierRow(
    int Id,
    string Codigo,
    string Proveedor,
    string Telefono,
    string Correo,
    string EntregaEstimada,
    string Estado,
    Proveedor Entity);

public sealed record PurchaseRow(
    int Id,
    string Orden,
    string Fecha,
    string Proveedor,
    string Insumo,
    string LotePeps,
    string Cantidad,
    string Total,
    string Estado,
    Compra Entity);

public sealed record ProductionRow(
    int Id,
    string Orden,
    string FechaInicio,
    string Producto,
    string LotePeps,
    decimal Programado,
    decimal Consumido,
    string Estado,
    Produccion Entity);

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

public sealed record SanitaryRow(
    int Id,
    DateTime RecordedAt,
    string Fecha,
    string InsumoLote,
    string Temperatura,
    string Cloro,
    string Ph,
    string Madurez,
    string Resultado,
    string Observaciones);
