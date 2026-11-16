using System.Windows.Controls;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class InventoryListView : UserControl
{
    public InventoryListView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is InventoryListViewModel vm)
        {
            await vm.CargarInsumosAsync();
        }
    }

    private void NuevoInsumo_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var window = new InventoryDetailWindow();
        window.ShowDialog();
        
        if (DataContext is InventoryListViewModel vm)
        {
            vm.CargarCommand.Execute(null);
        }
    }

    private void Editar_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.DataContext is Insumo insumo)
        {
            var window = new InventoryDetailWindow(insumo);
            window.ShowDialog();

            if (DataContext is InventoryListViewModel vm)
            {
                vm.CargarCommand.Execute(null);
            }
        }
    }
}
