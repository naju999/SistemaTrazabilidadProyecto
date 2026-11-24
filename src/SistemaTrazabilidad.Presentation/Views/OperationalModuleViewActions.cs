using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PdfSharp;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

internal static class ModuleViewActions
{
    public static async Task RunAsync(FrameworkElement view, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Window.GetWindow(view),
                ex.Message,
                "No se pudo completar la operación",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    public static Window GetOwner(FrameworkElement view)
    {
        return Window.GetWindow(view)
            ?? throw new InvalidOperationException("La ventana de la vista no está disponible.");
    }

    public static object? GetValue(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    public static string GetText(IReadOnlyDictionary<string, object?> values, string key)
    {
        return GetValue(values, key) as string ?? string.Empty;
    }

    public static int GetInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        return GetValue(values, key) is int value
            ? value
            : throw new InvalidOperationException($"El campo {key} no contiene un entero.");
    }

    public static decimal GetDecimal(IReadOnlyDictionary<string, object?> values, string key)
    {
        return GetValue(values, key) is decimal value
            ? value
            : throw new InvalidOperationException($"El campo {key} no contiene un decimal.");
    }

    public static DateTime GetDate(IReadOnlyDictionary<string, object?> values, string key)
    {
        return GetValue(values, key) is DateTime value
            ? value
            : throw new InvalidOperationException($"El campo {key} no contiene una fecha.");
    }

    public static T GetChoice<T>(IReadOnlyDictionary<string, object?> values, string key)
    {
        return GetValue(values, key) is T value
            ? value
            : throw new InvalidOperationException($"El campo {key} requiere una selección.");
    }

    public static IReadOnlyList<ModuleInputChoice> ToChoices<T>(
        IEnumerable<T> items,
        Func<T, string> label,
        Func<T, object> value)
    {
        return items.Select(item => new ModuleInputChoice(label(item), value(item))).ToList();
    }
}

public partial class SuppliersView
{
    private async void NuevoProveedor_Click(object sender, RoutedEventArgs e)
    {
        var fields = new[]
        {
            new ModuleInputField("nombre", "Nombre", ModuleInputKind.Text, MaxLength: 200),
            new ModuleInputField(
                "telefono",
                "Teléfono",
                ModuleInputKind.Digits,
                Required: false,
                MaxLength: 15),
            new ModuleInputField(
                "email",
                "Correo",
                ModuleInputKind.Text,
                Required: false,
                MaxLength: 254)
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Nuevo proveedor", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((SupplierModuleViewModel)DataContext).SaveAsync(
                null,
                ModuleViewActions.GetText(values, "nombre"),
                ModuleViewActions.GetText(values, "telefono"),
                ModuleViewActions.GetText(values, "email"),
                true));
    }

    private async void EditarProveedor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not SupplierRow row)
        {
            return;
        }

        var fields = new[]
        {
            new ModuleInputField(
                "nombre",
                "Nombre",
                ModuleInputKind.Text,
                row.Entity.Nombre,
                MaxLength: 200),
            new ModuleInputField(
                "telefono",
                "Teléfono",
                ModuleInputKind.Digits,
                row.Entity.Telefono,
                Required: false,
                MaxLength: 15),
            new ModuleInputField(
                "email",
                "Correo",
                ModuleInputKind.Text,
                row.Entity.Email,
                Required: false,
                MaxLength: 254),
            new ModuleInputField(
                "activo",
                "Estado",
                ModuleInputKind.Selection,
                row.Entity.Activo,
                [
                    new ModuleInputChoice("Activo", true),
                    new ModuleInputChoice("Inactivo", false)
                ])
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Editar proveedor", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((SupplierModuleViewModel)DataContext).SaveAsync(
                row.Entity,
                ModuleViewActions.GetText(values, "nombre"),
                ModuleViewActions.GetText(values, "telefono"),
                ModuleViewActions.GetText(values, "email"),
                ModuleViewActions.GetChoice<bool>(values, "activo")));
    }

    private async void EliminarProveedor_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not SupplierRow row)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            ModuleViewActions.GetOwner(this),
            $"¿Deseas eliminar al proveedor {row.Proveedor} ({row.Codigo})? " +
            "Solo se podrá eliminar si no tiene compras ni lotes asociados.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((SupplierModuleViewModel)DataContext).DeleteAsync(row.Entity));
    }
}

public partial class ProductionView
{
    private async void BuscarProduccion_Click(object sender, RoutedEventArgs e)
    {
        ProductionStatus? status = null;
        var selectedState = (EstadoProduccionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.Equals(selectedState, "Programada", StringComparison.OrdinalIgnoreCase))
        {
            status = ProductionStatus.Programada;
        }
        else if (string.Equals(selectedState, "En proceso", StringComparison.OrdinalIgnoreCase))
        {
            status = ProductionStatus.EnProceso;
        }
        else if (string.Equals(selectedState, "Finalizada", StringComparison.OrdinalIgnoreCase))
        {
            status = ProductionStatus.Finalizada;
        }
        else if (string.Equals(selectedState, "Cancelada", StringComparison.OrdinalIgnoreCase))
        {
            status = ProductionStatus.Cancelada;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((ProductionModuleViewModel)DataContext).LoadAsync(BusquedaProduccion.Text, status));
    }

    private async void NuevaOrdenProduccion_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ProductionModuleViewModel)DataContext;
        if (!ProductionOrderDialog.TryShow(
                ModuleViewActions.GetOwner(this),
                vm.Inputs,
                vm.Lots,
                out var startDate,
                out var lines))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            vm.CreateAsync(startDate, lines));
    }

    private async void IniciarProduccion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is ProductionRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((ProductionModuleViewModel)DataContext).StartAsync(row.Entity));
        }
    }

    private async void FinalizarProduccion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not ProductionRow row)
        {
            return;
        }

        var vm = (ProductionModuleViewModel)DataContext;
        var fields = row.Lines.Select(line =>
        {
            var inputName = vm.AllInputs.FirstOrDefault(input => input.Id == line.InsumoId)?.Nombre
                ?? $"Insumo {line.InsumoId}";
            var lotCode = vm.AllLots.FirstOrDefault(lot => lot.Id == line.LoteId)?.CodigoInterno
                ?? $"Lote {line.LoteId}";
            return new ModuleInputField(
                $"cantidad-{line.Id}",
                $"{inputName} / {lotCode} (programado: {line.CantidadProgramada})",
                ModuleInputKind.Decimal,
                line.CantidadProgramada);
        }).ToList();
        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Finalizar producción", fields, out var values))
        {
            return;
        }

        var consumedQuantities = row.Lines.ToDictionary(
            line => line.Id,
            line => ModuleViewActions.GetDecimal(values, $"cantidad-{line.Id}"));
        await ModuleViewActions.RunAsync(this, () =>
            vm.FinishAsync(row.Entity, consumedQuantities));
    }

    private async void VerPeps_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ProductionModuleViewModel)DataContext;
        var fields = new[]
        {
            new ModuleInputField(
                "insumo",
                "Insumo",
                ModuleInputKind.Selection,
                Choices: ModuleViewActions.ToChoices(vm.Inputs, i => $"{i.Codigo} — {i.Nombre}", i => i)),
            new ModuleInputField("cantidad", "Cantidad requerida", ModuleInputKind.Decimal)
        };
        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Consultar selección PEPS", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, async () =>
        {
            var input = ModuleViewActions.GetChoice<Insumo>(values, "insumo");
            var lot = await vm.SelectPepsLotAsync(
                input.Id, ModuleViewActions.GetDecimal(values, "cantidad"));
            MessageBox.Show(
                ModuleViewActions.GetOwner(this),
                $"Lote seleccionado: {lot.CodigoInterno}\nDisponible: {lot.CantidadDisponible}",
                "Selección PEPS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private async void CancelarProduccion_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is ProductionRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((ProductionModuleViewModel)DataContext).CancelAsync(row.Entity));
        }
    }
}

public partial class AlertsView
{
    private async void ActualizarAlertas_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (AlertModuleViewModel)DataContext;
        await ModuleViewActions.RunAsync(this, () =>
            viewModel.RefreshCurrentAlertsAsync(
                GetSelectedAlertType(),
                GetSelectedAlertStatus()));
    }

    private async void ResolverAlerta_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is AlertRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((AlertModuleViewModel)DataContext).ResolveAsync(row.Entity));
        }
    }

    private async void FiltrarAlertas_Click(object sender, RoutedEventArgs e)
    {
        await ModuleViewActions.RunAsync(this, () =>
            ((AlertModuleViewModel)DataContext).LoadAsync(
                GetSelectedAlertType(),
                GetSelectedAlertStatus()));
    }

    private string? GetSelectedAlertType()
    {
        var selectedType = (TipoAlertaCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return selectedType switch
        {
            "Stock mínimo" => AlertType.StockMinimo.ToString(),
            "Vencimiento" => AlertType.Vencimiento.ToString(),
            _ => null
        };
    }

    private string? GetSelectedAlertStatus()
    {
        return (EstadoAlertaCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
    }
}

public partial class AuditView
{
    private void ExportarReporte_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AuditModuleViewModel viewModel)
        {
            throw new InvalidOperationException("No se pudo acceder a los registros de auditoría.");
        }

        var records = viewModel.AuditEvents.ToList();
        if (records.Count == 0)
        {
            MessageBox.Show(
                ModuleViewActions.GetOwner(this),
                "No hay registros para exportar con los filtros actuales.",
                "Sin registros",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar auditoría a PDF",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(ModuleViewActions.GetOwner(this)) != true)
        {
            return;
        }

        try
        {
            AuditPdfExporter.Export(dialog.FileName, records);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException or
            PdfSharpException)
        {
            MessageBox.Show(
                ModuleViewActions.GetOwner(this),
                $"No se pudo generar el PDF: {ex.Message}",
                "Error al exportar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            ModuleViewActions.GetOwner(this),
            $"Se exportaron {records.Count} registros a PDF.",
            "Exportación completada",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void BuscarAuditoria_Click(object sender, RoutedEventArgs e)
    {
        await ModuleViewActions.RunAsync(this, () =>
            ((AuditModuleViewModel)DataContext).LoadAsync(
                BusquedaAuditoria.Text,
                DesdeAuditoriaPicker.SelectedDate,
                HastaAuditoriaPicker.SelectedDate,
                (AccionAuditoriaCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()));
    }

    private void VerDetalleAuditoria_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is AuditRow row)
        {
            var window = new AuditDetailWindow(row)
            {
                Owner = ModuleViewActions.GetOwner(this)
            };
            window.ShowDialog();
        }
    }
}
