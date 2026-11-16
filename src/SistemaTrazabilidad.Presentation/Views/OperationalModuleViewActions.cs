using System.Windows;
using System.Windows.Controls;
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
    private async void BuscarProveedor_Click(object sender, RoutedEventArgs e)
    {
        var state = (EstadoProveedorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        bool? active = state switch
        {
            "Activos" => true,
            "Inactivos" => false,
            _ => null
        };

        await ModuleViewActions.RunAsync(this, () =>
            ((SupplierModuleViewModel)DataContext).LoadAsync(BusquedaProveedor.Text, active));
    }

    private async void NuevoProveedor_Click(object sender, RoutedEventArgs e)
    {
        var fields = new[]
        {
            new ModuleInputField("codigo", "Código", ModuleInputKind.Text),
            new ModuleInputField("nombre", "Nombre", ModuleInputKind.Text),
            new ModuleInputField("telefono", "Teléfono", ModuleInputKind.Text, Required: false),
            new ModuleInputField("email", "Correo", ModuleInputKind.Text, Required: false),
            new ModuleInputField("entrega", "Tiempo de entrega (días)", ModuleInputKind.Integer, 1)
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Nuevo proveedor", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((SupplierModuleViewModel)DataContext).SaveAsync(
                null,
                ModuleViewActions.GetText(values, "codigo"),
                ModuleViewActions.GetText(values, "nombre"),
                ModuleViewActions.GetText(values, "telefono"),
                ModuleViewActions.GetText(values, "email"),
                ModuleViewActions.GetInt(values, "entrega"),
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
            new ModuleInputField("nombre", "Nombre", ModuleInputKind.Text, row.Entity.Nombre),
            new ModuleInputField("telefono", "Teléfono", ModuleInputKind.Text, row.Entity.Telefono, Required: false),
            new ModuleInputField("email", "Correo", ModuleInputKind.Text, row.Entity.Email, Required: false),
            new ModuleInputField("entrega", "Tiempo de entrega (días)", ModuleInputKind.Integer, row.Entity.TiempoEntregaDias),
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
                row.Entity.Codigo,
                ModuleViewActions.GetText(values, "nombre"),
                ModuleViewActions.GetText(values, "telefono"),
                ModuleViewActions.GetText(values, "email"),
                ModuleViewActions.GetInt(values, "entrega"),
                ModuleViewActions.GetChoice<bool>(values, "activo")));
    }
}

public partial class PurchasesView
{
    private async void BuscarCompras_Click(object sender, RoutedEventArgs e)
    {
        PurchaseStatus? status = null;
        var selectedState = (EstadoCompraCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrWhiteSpace(selectedState) &&
            !string.Equals(selectedState, "Todos", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<PurchaseStatus>(selectedState, out var parsedStatus))
        {
            status = parsedStatus;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((PurchaseModuleViewModel)DataContext).LoadAsync(
                BusquedaCompra.Text,
                status,
                DesdeCompraPicker.SelectedDate,
                HastaCompraPicker.SelectedDate));
    }

    private async void NuevaCompra_Click(object sender, RoutedEventArgs e)
    {
        var vm = (PurchaseModuleViewModel)DataContext;
        var fields = new[]
        {
            new ModuleInputField(
                "proveedor",
                "Proveedor",
                ModuleInputKind.Selection,
                Choices: ModuleViewActions.ToChoices(vm.Suppliers, s => s.Nombre, s => s)),
            new ModuleInputField(
                "insumo",
                "Insumo",
                ModuleInputKind.Selection,
                Choices: ModuleViewActions.ToChoices(vm.Inputs, i => $"{i.Codigo} — {i.Nombre}", i => i)),
            new ModuleInputField("fecha", "Fecha de compra", ModuleInputKind.Date, DateTime.Today),
            new ModuleInputField("cantidad", "Cantidad", ModuleInputKind.Decimal, 1m),
            new ModuleInputField("precio", "Precio unitario", ModuleInputKind.Decimal, 0m),
            new ModuleInputField("entrega", "Entrega estimada", ModuleInputKind.Date, DateTime.Today),
            new ModuleInputField("observaciones", "Observaciones", ModuleInputKind.Text, Required: false)
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Nueva orden de compra", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            vm.CreateAsync(
                ModuleViewActions.GetChoice<Proveedor>(values, "proveedor"),
                ModuleViewActions.GetChoice<Insumo>(values, "insumo"),
                ModuleViewActions.GetDate(values, "fecha"),
                ModuleViewActions.GetDecimal(values, "cantidad"),
                ModuleViewActions.GetDecimal(values, "precio"),
                ModuleViewActions.GetDate(values, "entrega"),
                ModuleViewActions.GetText(values, "observaciones")));
    }

    private async void RecibirCompra_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is PurchaseRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((PurchaseModuleViewModel)DataContext).ReceiveAsync(row.Entity));
        }
    }

    private async void CancelarCompra_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is PurchaseRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((PurchaseModuleViewModel)DataContext).CancelAsync(row.Entity));
        }
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

        await ModuleViewActions.RunAsync(this, () =>
            ((ProductionModuleViewModel)DataContext).LoadAsync(BusquedaProduccion.Text, status));
    }

    private async void NuevaOrdenProduccion_Click(object sender, RoutedEventArgs e)
    {
        var vm = (ProductionModuleViewModel)DataContext;
        var eligibleLots = vm.Lots
            .Where(l => vm.Inputs.Any(i => i.Id == l.InsumoId))
            .ToList();
        var fields = new[]
        {
            new ModuleInputField(
                "lote",
                "Lote e insumo",
                ModuleInputKind.Selection,
                Choices: ModuleViewActions.ToChoices(
                    eligibleLots,
                    l => $"{l.CodigoLote} — {vm.Inputs.First(i => i.Id == l.InsumoId).Nombre} ({l.CantidadDisponible} disponibles)",
                    l => l)),
            new ModuleInputField("orden", "Número de orden", ModuleInputKind.Text),
            new ModuleInputField("fecha", "Fecha de inicio", ModuleInputKind.Date, DateTime.Today),
            new ModuleInputField("cantidad", "Cantidad programada", ModuleInputKind.Decimal, 1m)
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Nueva orden de producción", fields, out var values))
        {
            return;
        }

        var lot = ModuleViewActions.GetChoice<Lote>(values, "lote");
        var input = vm.Inputs.First(i => i.Id == lot.InsumoId);
        await ModuleViewActions.RunAsync(this, () =>
            vm.CreateAsync(
                input,
                lot,
                ModuleViewActions.GetText(values, "orden"),
                ModuleViewActions.GetDate(values, "fecha"),
                ModuleViewActions.GetDecimal(values, "cantidad")));
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

        var fields = new[]
        {
            new ModuleInputField("cantidad", "Cantidad consumida", ModuleInputKind.Decimal, row.Entity.CantidadProgramada)
        };
        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Finalizar producción", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((ProductionModuleViewModel)DataContext).FinishAsync(
                row.Entity, ModuleViewActions.GetDecimal(values, "cantidad")));
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
            new ModuleInputField("cantidad", "Cantidad requerida", ModuleInputKind.Decimal, 1m)
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
                $"Lote seleccionado: {lot.CodigoLote}\nDisponible: {lot.CantidadDisponible}",
                "Selección PEPS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }
}

public partial class AlertsView
{
    private async void ActualizarAlertas_Click(object sender, RoutedEventArgs e)
    {
        await ModuleViewActions.RunAsync(this, () =>
            ((AlertModuleViewModel)DataContext).GenerateCurrentAlertsAsync());
    }

    private async void ResolverAlerta_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is AlertRow row)
        {
            await ModuleViewActions.RunAsync(this, () =>
                ((AlertModuleViewModel)DataContext).ResolveAsync(row.Entity));
        }
    }

    private async void SoloActualizarAlertas_Click(object sender, RoutedEventArgs e)
    {
        await ModuleViewActions.RunAsync(this, () =>
            ((AlertModuleViewModel)DataContext).LoadAsync());
    }

    private async void FiltrarAlertas_Click(object sender, RoutedEventArgs e)
    {
        var selectedType = (TipoAlertaCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var type = selectedType switch
        {
            "Stock mínimo" => AlertType.StockMinimo.ToString(),
            "Vencimiento" => AlertType.Vencimiento.ToString(),
            "Sanidad" => AlertType.Sanidad.ToString(),
            "Producción" => AlertType.Produccion.ToString(),
            _ => null
        };
        var status = (EstadoAlertaCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

        await ModuleViewActions.RunAsync(this, () =>
            ((AlertModuleViewModel)DataContext).LoadAsync(
                type, status, SoloNoLeidasCheckBox.IsChecked == true));
    }
}

public partial class AuditView
{
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
            MessageBox.Show(
                ModuleViewActions.GetOwner(this),
                row.Detalle,
                $"{row.Accion} — {row.Entidad} #{row.Registro}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}

public partial class SanitaryView
{
    private async void FiltrarSanitario_Click(object sender, RoutedEventArgs e)
    {
        var status = (ResultadoSanitarioCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.Equals(status, "Todos", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }

        await ModuleViewActions.RunAsync(this, () =>
            ((SanitaryModuleViewModel)DataContext).LoadAsync(
                BusquedaSanitaria.Text, status, FechaSanitariaPicker.SelectedDate));
    }

    private async void NuevoRegistroSanitario_Click(object sender, RoutedEventArgs e)
    {
        var vm = (SanitaryModuleViewModel)DataContext;
        var fields = new[]
        {
            new ModuleInputField(
                "insumo",
                "Insumo",
                ModuleInputKind.Selection,
                Choices: ModuleViewActions.ToChoices(vm.Inputs, i => $"{i.Codigo} — {i.Nombre}", i => i)),
            new ModuleInputField("fecha", "Fecha del control", ModuleInputKind.Date, DateTime.Today),
            new ModuleInputField("temperatura", "Temperatura (°C)", ModuleInputKind.Decimal, 20m),
            new ModuleInputField("cloro", "Cloro (mg/L)", ModuleInputKind.Decimal, 0m),
            new ModuleInputField("ph", "pH", ModuleInputKind.Decimal, 7m),
            new ModuleInputField("madurez", "Madurez", ModuleInputKind.Decimal, 0m),
            new ModuleInputField("observaciones", "Observaciones", ModuleInputKind.Text, Required: false)
        };

        if (!ModuleInputDialog.Show(ModuleViewActions.GetOwner(this), "Nuevo control sanitario", fields, out var values))
        {
            return;
        }

        await ModuleViewActions.RunAsync(this, () =>
            vm.CreateAsync(
                ModuleViewActions.GetChoice<Insumo>(values, "insumo"),
                ModuleViewActions.GetDate(values, "fecha"),
                ModuleViewActions.GetDecimal(values, "temperatura"),
                ModuleViewActions.GetDecimal(values, "cloro"),
                ModuleViewActions.GetDecimal(values, "ph"),
                ModuleViewActions.GetDecimal(values, "madurez"),
                ModuleViewActions.GetText(values, "observaciones")));
    }

    private void VerDetalleSanitario_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is SanitaryRow row)
        {
            MessageBox.Show(
                ModuleViewActions.GetOwner(this),
                string.IsNullOrWhiteSpace(row.Observaciones) ? "Sin observaciones." : row.Observaciones,
                $"Detalle del registro #{row.Id}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
