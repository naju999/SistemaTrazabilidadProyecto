using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SistemaTrazabilidad.Presentation.ViewModels;

namespace SistemaTrazabilidad.Presentation.Views;

public partial class SuppliersView : UserControl
{
    private readonly IServiceScope _scope;

    public SuppliersView()
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        DataContext = _scope.ServiceProvider.GetRequiredService<SupplierModuleViewModel>();
        Loaded += async (_, _) => await ModuleViewActions.RunAsync(this,
            () => ((SupplierModuleViewModel)DataContext).LoadAsync());
        Unloaded += (_, _) => _scope.Dispose();
    }
}
public partial class ProductionView : UserControl
{
    private readonly IServiceScope _scope;

    public ProductionView()
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        DataContext = _scope.ServiceProvider.GetRequiredService<ProductionModuleViewModel>();
        Loaded += async (_, _) => await ModuleViewActions.RunAsync(this,
            () => ((ProductionModuleViewModel)DataContext).LoadAsync());
        Unloaded += (_, _) => _scope.Dispose();
    }
}

public partial class AlertsView : UserControl
{
    private readonly IServiceScope _scope;

    public AlertsView()
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        DataContext = _scope.ServiceProvider.GetRequiredService<AlertModuleViewModel>();
        Loaded += async (_, _) => await ModuleViewActions.RunAsync(this,
            () => ((AlertModuleViewModel)DataContext).RefreshCurrentAlertsAsync());
        Unloaded += (_, _) => _scope.Dispose();
    }
}

public partial class AuditView : UserControl
{
    private readonly IServiceScope _scope;

    public AuditView()
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        DataContext = _scope.ServiceProvider.GetRequiredService<AuditModuleViewModel>();
        Loaded += async (_, _) => await ModuleViewActions.RunAsync(this,
            () => ((AuditModuleViewModel)DataContext).LoadAsync());
        Unloaded += (_, _) => _scope.Dispose();
    }
}
