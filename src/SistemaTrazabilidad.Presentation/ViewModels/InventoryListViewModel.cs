using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly AuditService _auditService;
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
        AuditService auditService, ISessionService sessionService)
    {
        _unitOfWork = unitOfWork;
        _inventoryService = inventoryService;
        _auditService = auditService;
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
            var insumosData = await _unitOfWork.Repository<Insumo>().GetAllAsync();
            var lista = new System.Collections.Generic.List<Insumo>(insumosData);
            
            Insumos.Clear();
            foreach (var insumo in lista)
            {
                Insumos.Add(insumo);
            }

            MensajeEstado = $"{Insumos.Count} insumos cargados";
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
            var criterio = Busqueda.ToLower();
            var insumosData = await _unitOfWork.Repository<Insumo>().FindAsync(i => 
                i.Codigo.ToLower().Contains(criterio) || 
                i.Nombre.ToLower().Contains(criterio));
            
            var lista = new System.Collections.Generic.List<Insumo>(insumosData);
            
            Insumos.Clear();
            foreach (var insumo in lista)
            {
                Insumos.Add(insumo);
            }

            MensajeEstado = $"{Insumos.Count} resultado(s) encontrado(s)";
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
            _unitOfWork.Repository<Insumo>().Remove(insumo);
            await _unitOfWork.CommitAsync();

            await _auditService.LogAsync("Insumo", insumo.Id, "Delete", $"Se eliminó el insumo {insumo.Nombre}.",
                _sessionService.UsuarioActual?.Id);

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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
