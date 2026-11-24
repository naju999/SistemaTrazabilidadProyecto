using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Domain.Common;
using SistemaTrazabilidad.Persistence.Context;

namespace SistemaTrazabilidad.Persistence.Repositories;

public interface IProveedorRepository : IGenericRepository<Proveedor>
{
    Task<IEnumerable<Proveedor>> GetActivosAsync();
    Task<Proveedor?> GetByCodigoAsync(string codigo);
}

public interface IInsumoRepository : IGenericRepository<Insumo>
{
    Task<IEnumerable<Insumo>> GetBajoStockAsync();
    Task<IEnumerable<Insumo>> GetActivosAsync();
    Task<Insumo?> GetByCodigoAsync(string codigo);
}

public interface ILoteRepository : IGenericRepository<Lote>
{
    Task<IEnumerable<Lote>> GetLotesPorInsumoAsync(int insumoId);
    Task<IEnumerable<Lote>> GetLotesVencidosAsync(int insumoId);
    Task<Lote?> GetByCodigoAsync(string codigoLote);
    Task<IEnumerable<Lote>> GetLotesPEPSAsync(int insumoId);
}

public interface IProduccionRepository : IGenericRepository<Produccion>
{
    Task<IEnumerable<Produccion>> GetPorInsumoAsync(int insumoId);
    Task<IEnumerable<Produccion>> GetEnProcesoAsync();
    Task<Produccion?> GetByOrdenAsync(string ordenProduccion);
}

public interface IAlertaRepository : IGenericRepository<Alerta>
{
    Task<IEnumerable<Alerta>> GetActivasAsync();
    Task<Alerta?> GetByCodigoAsync(string codigo);
}

public interface IAuditoriaRepository : IGenericRepository<Auditoria>
{
    Task<IEnumerable<Auditoria>> GetPorEntidadAsync(string entidad, int entidadId);
}

public class ProveedorRepository : GenericRepository<Proveedor>, IProveedorRepository
{
    public ProveedorRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Proveedor>> GetActivosAsync()
    {
        return FindAsync(p => p.Activo);
    }

    public async Task<Proveedor?> GetByCodigoAsync(string codigo)
    {
        return await _context.Set<Proveedor>().AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo);
    }

}

public class InsumoRepository : GenericRepository<Insumo>, IInsumoRepository
{
    public InsumoRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Insumo>> GetActivosAsync()
    {
        return FindAsync(i => i.Activo);
    }

    public async Task<IEnumerable<Insumo>> GetBajoStockAsync()
    {
        var insumos = await _context.Set<Insumo>()
            .Where(i => i.Activo)
            .ToListAsync();
        var lotes = await _context.Set<Lote>()
            .Where(l => !l.Bloqueado && l.CantidadDisponible > 0)
            .ToListAsync();

        foreach (var insumo in insumos)
        {
            insumo.StockDisponible = lotes
                .Where(l => l.InsumoId == insumo.Id && l.FechaVencimiento.Date >= DateTime.Today)
                .Sum(l => l.CantidadDisponible);
        }

        return insumos.Where(i => i.StockDisponible <= i.StockMinimo).ToList();
    }

    public async Task<Insumo?> GetByCodigoAsync(string codigo)
    {
        return await _context.Set<Insumo>().AsNoTracking().FirstOrDefaultAsync(i => i.Codigo == codigo);
    }
}

public class LoteRepository : GenericRepository<Lote>, ILoteRepository
{
    public LoteRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Lote>> GetLotesPorInsumoAsync(int insumoId)
    {
        return FindAsync(l => l.InsumoId == insumoId);
    }

    public Task<IEnumerable<Lote>> GetLotesVencidosAsync(int insumoId)
    {
        return FindAsync(l => l.InsumoId == insumoId && l.FechaVencimiento < DateTime.Today && l.CantidadDisponible > 0);
    }

    public async Task<Lote?> GetByCodigoAsync(string codigoLote)
    {
        return await _context.Set<Lote>().AsNoTracking().FirstOrDefaultAsync(l => l.CodigoLote == codigoLote);
    }

    public async Task<IEnumerable<Lote>> GetLotesPEPSAsync(int insumoId)
    {
        return await _context.Set<Lote>()
            .AsNoTracking()
            .Where(l => l.InsumoId == insumoId && !l.Bloqueado && l.CantidadDisponible > 0)
            .OrderBy(l => l.FechaIngreso)
            .ToListAsync();
    }
}

public class ProduccionRepository : GenericRepository<Produccion>, IProduccionRepository
{
    public ProduccionRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Produccion>> GetPorInsumoAsync(int insumoId)
    {
        return FindAsync(p => p.InsumoId == insumoId);
    }

    public Task<IEnumerable<Produccion>> GetEnProcesoAsync()
    {
        return FindAsync(p => p.Estado == ProductionStatus.EnProceso);
    }

    public async Task<Produccion?> GetByOrdenAsync(string ordenProduccion)
    {
        return await _context.Set<Produccion>().AsNoTracking().FirstOrDefaultAsync(p => p.OrdenProduccion == ordenProduccion);
    }
}

public class AlertaRepository : GenericRepository<Alerta>, IAlertaRepository
{
    public AlertaRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Alerta>> GetActivasAsync()
    {
        return FindAsync(a => a.Activa);
    }

    public async Task<Alerta?> GetByCodigoAsync(string codigo)
    {
        return await _context.Set<Alerta>().AsNoTracking().FirstOrDefaultAsync(a => a.Codigo == codigo);
    }
}

public class AuditoriaRepository : GenericRepository<Auditoria>, IAuditoriaRepository
{
    public AuditoriaRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Auditoria>> GetPorEntidadAsync(string entidad, int entidadId)
    {
        return FindAsync(a => a.Entidad == entidad && a.EntidadId == entidadId);
    }
}
