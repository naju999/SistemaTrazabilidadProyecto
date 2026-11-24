using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class InventoryDetailViewModel : INotifyPropertyChanged
{
    private static readonly string[] AllowedUnitsOfMeasure =
        ["kg", "gramos", "litros", "ml", "unidad"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;
    private readonly ISessionService _sessionService;

    private Insumo? _insumo;
    private string _codigo = "Se asigna automáticamente al guardar";
    private string _nombre = "";
    private string _unidadMedida = "kg";
    private string _stockMinimoTexto = string.Empty;
    private bool _activo = true;
    private string _mensajeError = "";
    private bool _cargando = false;
    private bool _esNuevo = true;

    public string Codigo
    {
        get => _codigo;
        private set { if (_codigo != value) { _codigo = value; OnPropertyChanged(); } }
    }

    public string Nombre
    {
        get => _nombre;
        set { if (_nombre != value) { _nombre = value; OnPropertyChanged(); } }
    }

    public string UnidadMedida
    {
        get => _unidadMedida;
        set { if (_unidadMedida != value) { _unidadMedida = value; OnPropertyChanged(); } }
    }

    public string StockMinimoTexto
    {
        get => _stockMinimoTexto;
        set
        {
            if (_stockMinimoTexto != value)
            {
                _stockMinimoTexto = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Activo
    {
        get => _activo;
        set
        {
            if (_activo != value)
            {
                _activo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Estado));
            }
        }
    }

    public string Estado
    {
        get => Activo ? "Activo" : "Inactivo";
        set
        {
            Activo = value switch
            {
                "Activo" => true,
                "Inactivo" => false,
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "El estado no es válido.")
            };
        }
    }

    public string MensajeError
    {
        get => _mensajeError;
        set { if (_mensajeError != value) { _mensajeError = value; OnPropertyChanged(); } }
    }

    public bool Cargando
    {
        get => _cargando;
        set { if (_cargando != value) { _cargando = value; OnPropertyChanged(); } }
    }

    public string TituloVentana => _esNuevo ? "Nuevo Insumo" : $"Editar Insumo: {Nombre}";
    public ICommand GuardarCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? GuardoExitoso;

    public InventoryDetailViewModel(
        IUnitOfWork unitOfWork,
        AuditService auditService,
        ISessionService sessionService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _sessionService = sessionService;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
    }

    public void CargarInsumo(Insumo insumo)
    {
        _insumo = insumo;
        _esNuevo = false;

        Codigo = insumo.Codigo;
        Nombre = insumo.Nombre;
        UnidadMedida = NormalizarUnidadMedida(insumo.UnidadMedida);
        StockMinimoTexto = insumo.StockMinimo.ToString("0.##", CultureInfo.CurrentCulture);
        Activo = insumo.Activo;

        OnPropertyChanged(nameof(TituloVentana));
    }

    private bool ValidarCampos(out decimal stockMinimo)
    {
        MensajeError = "";
        stockMinimo = 0;
        Nombre = Nombre.Trim();
        UnidadMedida = UnidadMedida.Trim();

        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MensajeError = "El nombre es obligatorio";
            return false;
        }
        if (Nombre.Length > 200)
        {
            MensajeError = "El nombre no puede superar 200 caracteres";
            return false;
        }

        if (!AllowedUnitsOfMeasure.Contains(UnidadMedida, StringComparer.OrdinalIgnoreCase))
        {
            MensajeError = "Selecciona una unidad de medida válida";
            return false;
        }

        if (!decimal.TryParse(
                StockMinimoTexto,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var valorStockMinimo))
        {
            MensajeError = string.IsNullOrWhiteSpace(StockMinimoTexto)
                ? "El stock mínimo es obligatorio"
                : "El stock mínimo debe ser un número válido";
            return false;
        }

        if (valorStockMinimo < 0)
        {
            MensajeError = "El stock mínimo no puede ser negativo";
            return false;
        }

        stockMinimo = valorStockMinimo;
        return true;
    }

    private async Task GuardarAsync()
    {
        if (!ValidarCampos(out var stockMinimo))
            return;

        Cargando = true;
        try
        {
            if (_esNuevo)
            {
                var codigoGenerado = await GenerarCodigoAsync();
                var nuevoInsumo = new Insumo
                {
                    Codigo = codigoGenerado,
                    Nombre = Nombre,
                    UnidadMedida = UnidadMedida,
                    StockActual = 0,
                    StockMinimo = stockMinimo,
                    Activo = Activo
                };

                await _unitOfWork.Repository<Insumo>().AddAsync(nuevoInsumo);
                await _unitOfWork.CommitAsync();

                _insumo = nuevoInsumo;
                _esNuevo = false;
                OnPropertyChanged(nameof(TituloVentana));

                await _auditService.LogAsync("Insumo", nuevoInsumo.Id, "Create",
                    $"Se creó el insumo {nuevoInsumo.Nombre}.", _sessionService.UsuarioActual?.Id);

                MensajeError = "";
            }
            else
            {
                var insumo = await _unitOfWork.Repository<Insumo>()
                    .GetByIdAsync(_insumo!.Id)
                    ?? throw new InvalidOperationException("El insumo ya no existe.");

                insumo.Codigo = Codigo;
                insumo.Nombre = Nombre;
                insumo.UnidadMedida = UnidadMedida;
                insumo.StockMinimo = stockMinimo;
                insumo.Activo = Activo;
                await _unitOfWork.CommitAsync();

                _insumo = insumo;
                await _auditService.LogAsync("Insumo", insumo.Id, "Update",
                    $"Se actualizó el insumo {insumo.Nombre}.", _sessionService.UsuarioActual?.Id);
            }

            GuardoExitoso?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al guardar: {ex.GetBaseException().Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    private async Task<string> GenerarCodigoAsync()
    {
        var insumos = (await _unitOfWork.Repository<Insumo>().GetAllAsync()).ToList();
        var ultimoNumero = insumos
            .Select(i => i.Codigo)
            .Where(c => c.StartsWith("INS", StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c.AsSpan(3), out var numero) ? numero : 0)
            .DefaultIfEmpty(0)
            .Max();

        var siguienteNumero = ultimoNumero + 1;
        string codigo;
        do
        {
            codigo = $"INS{siguienteNumero++:D3}";
        }
        while (insumos.Any(i => string.Equals(i.Codigo, codigo, StringComparison.OrdinalIgnoreCase)));

        Codigo = codigo;
        return codigo;
    }

    internal static string NormalizarUnidadMedida(string unidadMedida)
    {
        const string itemPrefix = "System.Windows.Controls.ComboBoxItem: ";
        var unidad = unidadMedida.StartsWith(itemPrefix, StringComparison.Ordinal)
            ? unidadMedida[itemPrefix.Length..]
            : unidadMedida;

        return string.Equals(unidad, "unidades", StringComparison.OrdinalIgnoreCase)
            ? "unidad"
            : unidad;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
