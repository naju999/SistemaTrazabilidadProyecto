using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public sealed class LotManagementViewModel : INotifyPropertyChanged
{
    private readonly Insumo _insumo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InventoryService _inventoryService;
    private bool _cargando;
    private decimal _disponibleTotal;
    private decimal _reservadoTotal;
    private string _mensajeEstado = string.Empty;

    public ObservableCollection<LotRow> Lotes { get; } = new();
    public string ProductoDescripcion => $"{_insumo.Codigo} · {_insumo.Nombre} · Unidad: {_insumo.UnidadMedida}";
    public string DisponibleResumen => $"{_disponibleTotal.ToString("0.##", CultureInfo.CurrentCulture)} {_insumo.UnidadMedida}";
    public string ReservadoResumen => $"{_reservadoTotal.ToString("0.##", CultureInfo.CurrentCulture)} {_insumo.UnidadMedida}";
    public string CantidadLotes => Lotes.Count.ToString(CultureInfo.CurrentCulture);
    public bool TieneLotes => Lotes.Count > 0;
    public bool PuedeRegistrarEntrada => _insumo.Activo && !_cargando;

    public bool Cargando
    {
        get => _cargando;
        private set
        {
            if (_cargando == value)
            {
                return;
            }

            _cargando = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PuedeRegistrarEntrada));
        }
    }

    public string MensajeEstado
    {
        get => _mensajeEstado;
        private set
        {
            if (_mensajeEstado == value)
            {
                return;
            }

            _mensajeEstado = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LotManagementViewModel(Insumo insumo, IUnitOfWork unitOfWork, InventoryService inventoryService)
    {
        _insumo = insumo;
        _unitOfWork = unitOfWork;
        _inventoryService = inventoryService;
    }

    public async Task CargarLotesAsync()
    {
        Cargando = true;
        MensajeEstado = string.Empty;

        try
        {
            var lotes = (await _unitOfWork.Repository<Lote>()
                .FindAsync(lote => lote.InsumoId == _insumo.Id))
                .OrderBy(lote => lote.FechaVencimiento)
                .ThenBy(lote => lote.FechaIngreso)
                .ToList();
            var proveedoresPorId = (await _unitOfWork.Repository<Proveedor>().GetAllAsync())
                .ToDictionary(proveedor => proveedor.Id, proveedor => proveedor.Nombre);

            var produccionesActivas = (await _unitOfWork.Repository<Produccion>().FindAsync(produccion =>
                produccion.InsumoId == _insumo.Id &&
                (produccion.Estado == ProductionStatus.Programada ||
                 produccion.Estado == ProductionStatus.EnProceso))).ToList();

            var filas = lotes.Select(lote =>
            {
                var reservado = produccionesActivas
                    .Where(produccion => produccion.LoteId == lote.Id)
                    .Sum(produccion => produccion.CantidadProgramada);
                var disponible = lote.Bloqueado || lote.EstaVencido ? 0 : lote.CantidadDisponible;
                var estado = ObtenerEstado(lote, reservado);
                var proveedorNombre = lote.ProveedorId is int proveedorId &&
                                      proveedoresPorId.TryGetValue(proveedorId, out var nombre)
                    ? nombre
                    : "No registrado";

                return new LotRow(
                    lote.CodigoInterno,
                    lote.CodigoLote == lote.CodigoInterno ? string.Empty : lote.CodigoLote,
                    proveedorNombre,
                    lote.FechaIngreso,
                    lote.CantidadInicial,
                    lote.CantidadDisponible,
                    disponible,
                    reservado,
                    lote.FechaVencimiento,
                    estado);
            }).ToList();

            Lotes.Clear();
            foreach (var fila in filas)
            {
                Lotes.Add(fila);
            }

            _disponibleTotal = await _inventoryService.GetAvailableQuantityAsync(_insumo.Id);
            _reservadoTotal = filas.Sum(fila => fila.Reservado);
            OnPropertyChanged(nameof(DisponibleResumen));
            OnPropertyChanged(nameof(ReservadoResumen));
            OnPropertyChanged(nameof(CantidadLotes));
            OnPropertyChanged(nameof(TieneLotes));

            MensajeEstado = _insumo.Activo
                ? (Lotes.Count == 0 ? "No hay entradas registradas para este insumo." : string.Empty)
                : "El insumo está inactivo; no se pueden registrar nuevas entradas.";
        }
        catch (Exception ex)
        {
            MensajeEstado = $"Error al cargar los lotes: {ex.GetBaseException().Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    private static string ObtenerEstado(Lote lote, decimal reservado)
    {
        if (lote.Bloqueado)
        {
            return "Bloqueado";
        }

        if (lote.EstaVencido)
        {
            return "Vencido";
        }

        if (lote.CantidadDisponible <= 0)
        {
            return reservado > 0 ? "Reservado" : "Agotado";
        }

        return reservado > 0 ? "Parcialmente reservado" : "Disponible";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record LotRow(
    string CodigoInterno,
    string CodigoLoteAnterior,
    string Proveedor,
    DateTime FechaIngreso,
    decimal CantidadInicial,
    decimal Saldo,
    decimal Disponible,
    decimal Reservado,
    DateTime FechaVencimiento,
    string Estado);
