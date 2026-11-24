using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public sealed class RegisterLotViewModel : INotifyPropertyChanged
{
    private readonly Insumo _insumo;
    private readonly InventoryService _inventoryService;
    private readonly ISessionService _sessionService;
    private readonly IUnitOfWork _unitOfWork;
    private string _cantidadTexto = string.Empty;
    private DateTime? _fechaVencimiento;
    private Proveedor? _proveedorSeleccionado;
    private string _mensajeError = string.Empty;
    private bool _cargando;
    private bool _proveedoresCargados;

    public string ProductoDescripcion => $"{_insumo.Codigo} · {_insumo.Nombre} · Unidad: {_insumo.UnidadMedida}";
    public ObservableCollection<Proveedor> Proveedores { get; } = new();

    public string CantidadTexto
    {
        get => _cantidadTexto;
        set
        {
            if (_cantidadTexto == value)
            {
                return;
            }

            _cantidadTexto = value;
            OnPropertyChanged();
        }
    }

    public DateTime? FechaVencimiento
    {
        get => _fechaVencimiento;
        set
        {
            if (_fechaVencimiento == value)
            {
                return;
            }

            _fechaVencimiento = value;
            OnPropertyChanged();
        }
    }

    public Proveedor? ProveedorSeleccionado
    {
        get => _proveedorSeleccionado;
        set
        {
            if (_proveedorSeleccionado == value)
            {
                return;
            }

            _proveedorSeleccionado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PuedeGuardar));
        }
    }

    public string MensajeError
    {
        get => _mensajeError;
        private set
        {
            if (_mensajeError == value)
            {
                return;
            }

            _mensajeError = value;
            OnPropertyChanged();
        }
    }

    public bool PuedeGuardar =>
        !_cargando && _proveedoresCargados && ProveedorSeleccionado is not null && _insumo.Activo;
    public ICommand GuardarCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? GuardadoExitoso;

    public RegisterLotViewModel(
        Insumo insumo,
        InventoryService inventoryService,
        ISessionService sessionService,
        IUnitOfWork unitOfWork)
    {
        _insumo = insumo;
        _inventoryService = inventoryService;
        _sessionService = sessionService;
        _unitOfWork = unitOfWork;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
    }

    public async Task CargarProveedoresAsync()
    {
        _cargando = true;
        OnPropertyChanged(nameof(PuedeGuardar));

        try
        {
            var proveedores = (await _unitOfWork.Repository<Proveedor>()
                    .FindAsync(proveedor => proveedor.Activo))
                .OrderBy(proveedor => proveedor.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Proveedores.Clear();
            foreach (var proveedor in proveedores)
            {
                Proveedores.Add(proveedor);
            }

            _proveedoresCargados = true;
            MensajeError = Proveedores.Count == 0
                ? "No hay proveedores activos. Registra o activa un proveedor antes de ingresar el lote."
                : string.Empty;
        }
        catch (Exception ex)
        {
            MensajeError = $"No se pudieron cargar los proveedores: {ex.GetBaseException().Message}";
        }
        finally
        {
            _cargando = false;
            OnPropertyChanged(nameof(PuedeGuardar));
        }
    }

    private async Task GuardarAsync()
    {
        MensajeError = string.Empty;

        if (!TryParseCantidad(CantidadTexto, out var cantidad) || cantidad <= 0)
        {
            MensajeError = "Ingresa una cantidad numérica mayor que cero.";
            return;
        }

        if (FechaVencimiento is null || FechaVencimiento.Value.Date <= DateTime.Today)
        {
            MensajeError = "La fecha de vencimiento debe ser posterior a la fecha de ingreso.";
            return;
        }

        if (ProveedorSeleccionado is null)
        {
            MensajeError = "Selecciona el proveedor de esta entrada.";
            return;
        }

        var usuarioId = _sessionService.UsuarioActual?.Id;
        if (usuarioId is null)
        {
            MensajeError = "No se pudo identificar al usuario de la sesión.";
            return;
        }

        _cargando = true;
        OnPropertyChanged(nameof(PuedeGuardar));

        try
        {
            await _inventoryService.RegisterEntradaAsync(
                _insumo.Id,
                ProveedorSeleccionado.Id,
                cantidad,
                FechaVencimiento.Value.Date,
                usuarioId.Value);

            GuardadoExitoso?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensajeError = ex.GetBaseException().Message;
        }
        finally
        {
            _cargando = false;
            OnPropertyChanged(nameof(PuedeGuardar));
        }
    }

    private static bool TryParseCantidad(string text, out decimal cantidad)
    {
        const NumberStyles styles = NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite;

        return decimal.TryParse(
            text,
            styles | NumberStyles.AllowThousands,
            CultureInfo.CurrentCulture,
            out cantidad);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
