using SistemaTrazabilidad.Domain.Common;
using SistemaTrazabilidad.Persistence.Context;

namespace SistemaTrazabilidad.Persistence.Repositories;

public interface IUnitOfWork
{
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> CommitAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        if (_repositories.ContainsKey(type))
        {
            return (IGenericRepository<T>)_repositories[type]!;
        }

        var repository = new GenericRepository<T>(_context);
        _repositories[type] = repository;
        return repository;
    }

    public async Task<int> CommitAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
