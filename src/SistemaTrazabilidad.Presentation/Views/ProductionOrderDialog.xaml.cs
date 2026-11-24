using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Domain.Entities;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class ProductionOrderDialog : Window
{
    private IReadOnlyList<ProductionOrderLine> _selectedLines = [];

    public IReadOnlyList<Insumo> Insumos { get; }
    public ObservableCollection<ProductionOrderLineDraft> Lineas { get; } = [];

    private ProductionOrderDialog(
        Window owner,
        IReadOnlyList<Insumo> inputs,
        IReadOnlyList<Lote> lots)
    {
        InitializeComponent();
        Owner = owner;
        Insumos = inputs;
        DataContext = this;
        FechaInicioPicker.SelectedDate = DateTime.Today;
        Lineas.Add(new ProductionOrderLineDraft(inputs, lots));
    }

    public static bool TryShow(
        Window owner,
        IReadOnlyList<Insumo> inputs,
        IReadOnlyList<Lote> lots,
        out DateTime startDate,
        out IReadOnlyList<ProductionOrderLine> lines)
    {
        var dialog = new ProductionOrderDialog(owner, inputs, lots);
        if (dialog.ShowDialog() != true)
        {
            startDate = default;
            lines = [];
            return false;
        }

        startDate = dialog.FechaInicioPicker.SelectedDate!.Value;
        lines = dialog._selectedLines;
        return true;
    }

    private void AgregarInsumo_Click(object sender, RoutedEventArgs e)
    {
        Lineas.Add(new ProductionOrderLineDraft(Insumos, Lineas[0].TodosLosLotes));
    }

    private void QuitarInsumo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is ProductionOrderLineDraft line &&
            Lineas.Count > 1)
        {
            Lineas.Remove(line);
        }
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        LineasGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LineasGrid.CommitEdit(DataGridEditingUnit.Row, true);
        MensajeErrorTextBlock.Visibility = Visibility.Collapsed;

        if (FechaInicioPicker.SelectedDate is not DateTime startDate)
        {
            ShowValidationError("Selecciona la fecha de inicio.");
            return;
        }

        var result = new List<ProductionOrderLine>(Lineas.Count);
        for (var index = 0; index < Lineas.Count; index++)
        {
            var line = Lineas[index];
            if (line.InsumoSeleccionado is null)
            {
                ShowValidationError($"Selecciona un insumo para la línea {index + 1}.");
                return;
            }

            if (line.LoteSeleccionado is null ||
                line.LoteSeleccionado.InsumoId != line.InsumoSeleccionado.Id)
            {
                ShowValidationError($"Selecciona un lote válido para la línea {index + 1}.");
                return;
            }

            if (!decimal.TryParse(
                    line.CantidadTexto,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out var quantity) ||
                quantity <= 0)
            {
                ShowValidationError($"La cantidad de la línea {index + 1} debe ser mayor que cero.");
                return;
            }

            result.Add(new ProductionOrderLine(
                line.InsumoSeleccionado.Id,
                line.LoteSeleccionado.Id,
                quantity));
        }

        var requestedByLot = result
            .GroupBy(line => line.LoteId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.CantidadProgramada));
        foreach (var request in requestedByLot)
        {
            var lot = Lineas.Select(line => line.LoteSeleccionado)
                .First(lot => lot?.Id == request.Key)!;
            if (request.Value > lot.CantidadDisponible)
            {
                ShowValidationError(
                    $"El lote {lot.CodigoInterno} solo tiene {lot.CantidadDisponible} disponibles.");
                return;
            }
        }

        _selectedLines = result;
        DialogResult = true;
    }

    private void ShowValidationError(string message)
    {
        MensajeErrorTextBlock.Text = message;
        MensajeErrorTextBlock.Visibility = Visibility.Visible;
    }
}

public sealed class ProductionOrderLineDraft : INotifyPropertyChanged
{
    private Insumo? _insumoSeleccionado;
    private Lote? _loteSeleccionado;
    private string _cantidadTexto = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<Insumo> Insumos { get; }
    public IReadOnlyList<Lote> TodosLosLotes { get; }
    public IReadOnlyList<Lote> LotesDisponibles { get; private set; } = [];

    public Insumo? InsumoSeleccionado
    {
        get => _insumoSeleccionado;
        set
        {
            if (_insumoSeleccionado?.Id == value?.Id)
            {
                return;
            }

            _insumoSeleccionado = value;
            _loteSeleccionado = null;
            LotesDisponibles = value is null
                ? []
                : TodosLosLotes.Where(lot => lot.InsumoId == value.Id).ToList();
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoteSeleccionado));
            OnPropertyChanged(nameof(TieneLoteSeleccionado));
            OnPropertyChanged(nameof(TieneInsumoSeleccionado));
            OnPropertyChanged(nameof(LotesDisponibles));
            OnPropertyChanged(nameof(LotePlaceholder));
        }
    }

    public bool TieneInsumoSeleccionado => InsumoSeleccionado is not null;
    public string LotePlaceholder => !TieneInsumoSeleccionado
        ? "Selecciona primero un insumo"
        : LotesDisponibles.Count == 0
            ? "No hay lotes disponibles"
            : "Selecciona un lote";

    public Lote? LoteSeleccionado
    {
        get => _loteSeleccionado;
        set
        {
            if (_loteSeleccionado?.Id == value?.Id)
            {
                return;
            }

            _loteSeleccionado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TieneLoteSeleccionado));
        }
    }

    public bool TieneLoteSeleccionado => LoteSeleccionado is not null;

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

    public ProductionOrderLineDraft(IReadOnlyList<Insumo> inputs, IReadOnlyList<Lote> lots)
    {
        Insumos = inputs;
        TodosLosLotes = lots;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
