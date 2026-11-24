using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SistemaTrazabilidad.Domain.Common;
using SistemaTrazabilidad.Persistence.Context;

namespace SistemaTrazabilidad.Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> FindTrackedAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public void Remove(T entity)
    {
        var trackedEntity = _dbSet.Local.FirstOrDefault(e => e.Id == entity.Id);
        _dbSet.Remove(trackedEntity ?? entity);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }
}
