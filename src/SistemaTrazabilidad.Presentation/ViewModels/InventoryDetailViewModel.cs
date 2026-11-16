using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class InventoryDetailViewModel : INotifyPropertyChanged
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;
    private readonly ISessionService _sessionService;

    private Insumo? _insumo;
    private string _codigo = "";
    private string _nombre = "";
    private string _unidadMedida = "kg";
    private decimal _stockActual = 0;
    private decimal _stockMinimo = 0;
    private bool _activo = true;
    private string _mensajeError = "";
    private bool _cargando = false;
    private bool _esNuevo = true;

    public string Codigo
    {
        get => _codigo;
        set { if (_codigo != value) { _codigo = value; OnPropertyChanged(); } }
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

    public decimal StockActual
    {
        get => _stockActual;
        set { if (_stockActual != value) { _stockActual = value; OnPropertyChanged(); } }
    }

    public decimal StockMinimo
    {
        get => _stockMinimo;
        set { if (_stockMinimo != value) { _stockMinimo = value; OnPropertyChanged(); } }
    }

    public bool Activo
    {
        get => _activo;
        set { if (_activo != value) { _activo = value; OnPropertyChanged(); } }
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

    public bool EsNuevo => _esNuevo;

    public string TituloVentana => _esNuevo ? "Nuevo Insumo" : $"Editar Insumo: {Nombre}";

    public ICommand GuardarCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? GuardoExitoso;

    public InventoryDetailViewModel(IUnitOfWork unitOfWork, AuditService auditService, ISessionService sessionService)
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
        UnidadMedida = insumo.UnidadMedida;
        StockActual = insumo.StockActual;
        StockMinimo = insumo.StockMinimo;
        Activo = insumo.Activo;

        OnPropertyChanged(nameof(TituloVentana));
        OnPropertyChanged(nameof(EsNuevo));
    }

    private bool ValidarCampos()
    {
        MensajeError = "";

        if (string.IsNullOrWhiteSpace(Codigo))
        {
            MensajeError = "El código es obligatorio";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MensajeError = "El nombre es obligatorio";
            return false;
        }

        if (StockActual < 0)
        {
            MensajeError = "El stock actual no puede ser negativo";
            return false;
        }

        if (StockMinimo < 0)
        {
            MensajeError = "El stock mínimo no puede ser negativo";
            return false;
        }

        return true;
    }

    private async Task GuardarAsync()
    {
        if (!ValidarCampos())
            return;

        Cargando = true;
        try
        {
            if (_esNuevo)
            {
                var nuevoInsumo = new Insumo
                {
                    Codigo = Codigo,
                    Nombre = Nombre,
                    UnidadMedida = UnidadMedida,
                    StockActual = StockActual,
                    StockMinimo = StockMinimo,
                    Activo = Activo
                };

                await _unitOfWork.Repository<Insumo>().AddAsync(nuevoInsumo);
                await _unitOfWork.CommitAsync();
                
                await _auditService.LogAsync("Insumo", nuevoInsumo.Id, "Create",
                    $"Se creó el insumo {nuevoInsumo.Nombre}.", _sessionService.UsuarioActual?.Id);

                MensajeError = "";
            }
            else
            {
                _insumo!.Codigo = Codigo;
                _insumo.Nombre = Nombre;
                _insumo.UnidadMedida = UnidadMedida;
                _insumo.StockActual = StockActual;
                _insumo.StockMinimo = StockMinimo;
                _insumo.Activo = Activo;

                _unitOfWork.Repository<Insumo>().Update(_insumo);
                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync("Insumo", _insumo.Id, "Update",
                    $"Se actualizó el insumo {_insumo.Nombre}.", _sessionService.UsuarioActual?.Id);
            }

            GuardoExitoso?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
