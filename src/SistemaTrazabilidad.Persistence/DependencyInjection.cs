using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaTrazabilidad.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<Context.AppDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddScoped<Repositories.IUnitOfWork, Repositories.UnitOfWork>();
        services.AddScoped<Repositories.IProveedorRepository, Repositories.ProveedorRepository>();
        services.AddScoped<Repositories.IInsumoRepository, Repositories.InsumoRepository>();
        services.AddScoped<Repositories.ILoteRepository, Repositories.LoteRepository>();
        services.AddScoped<Repositories.IProduccionRepository, Repositories.ProduccionRepository>();
        services.AddScoped<Repositories.IAlertaRepository, Repositories.AlertaRepository>();
        services.AddScoped<Repositories.IAuditoriaRepository, Repositories.AuditoriaRepository>();

        return services;
    }
}
