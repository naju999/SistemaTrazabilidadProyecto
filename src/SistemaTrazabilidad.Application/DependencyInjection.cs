using Microsoft.Extensions.DependencyInjection;
using SistemaTrazabilidad.Application.Services;

namespace SistemaTrazabilidad.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AlertService>();
        services.AddScoped<AuditService>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<ProductionService>();
        services.AddScoped<SanitaryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<LotService>();
        services.AddScoped<InventoryService>();
        services.AddSingleton<ISessionService, SessionService>();
        
        return services;
    }
}
