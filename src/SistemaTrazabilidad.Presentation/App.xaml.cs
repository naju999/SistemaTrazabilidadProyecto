using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SistemaTrazabilidad.Application;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Persistence;
using SistemaTrazabilidad.Persistence.Context;

namespace SistemaTrazabilidad.Presentation;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            var configuration = BuildConfiguration();
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            Services = serviceCollection.BuildServiceProvider();

            InitializeDatabase();
            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al inicializar la aplicación:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "Error Fatal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            this.Shutdown(1);
        }

        base.OnStartup(e);
    }

    private static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });

        // Persistence y Repositorios
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No se configuró la conexión DefaultConnection.");
        services.AddPersistence(connectionString);

        // Application Services
        services.AddApplicationServices();
        
        // Session
        services.AddSingleton<ISessionService, SessionService>();

        // Presentation ViewModels y Views
        services.AddTransient<Views.LoginWindow>();
        services.AddTransient<Views.MainWindow>();
        services.AddSingleton<ViewModels.LoginViewModel>();
        services.AddSingleton<ViewModels.DashboardViewModel>();
        services.AddSingleton<ViewModels.InventoryListViewModel>();
        services.AddTransient<ViewModels.SupplierModuleViewModel>();
        services.AddTransient<ViewModels.PurchaseModuleViewModel>();
        services.AddTransient<ViewModels.ProductionModuleViewModel>();
        services.AddTransient<ViewModels.AlertModuleViewModel>();
        services.AddTransient<ViewModels.AuditModuleViewModel>();
        services.AddTransient<ViewModels.SanitaryModuleViewModel>();
    }

    private static void InitializeDatabase()
    {
        try
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seedInitializer = new SeedDataInitializer(context);
            seedInitializer.InitializeAsync().Wait();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error inicializando base de datos:\n\n{ex.Message}",
                "Error de BD",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }
    }

    private static void ShowLoginWindow()
    {
        var loginWindow = Services.GetRequiredService<Views.LoginWindow>();
        loginWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
