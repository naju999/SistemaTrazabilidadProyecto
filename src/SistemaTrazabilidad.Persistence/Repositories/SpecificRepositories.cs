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
    Task<IEnumerable<Proveedor>> GetSugerenciasDeCompraAsync(int top = 5);
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

public interface ICompraRepository : IGenericRepository<Compra>
{
    Task<IEnumerable<Compra>> GetComprasPorProveedorAsync(int proveedorId);
    Task<IEnumerable<Compra>> GetHistoricoPreciosAsync(int insumoId);
    Task<IEnumerable<Compra>> GetPendientesAsync();
}

public interface IProduccionRepository : IGenericRepository<Produccion>
{
    Task<IEnumerable<Produccion>> GetPorInsumoAsync(int insumoId);
    Task<IEnumerable<Produccion>> GetEnProcesoAsync();
    Task<Produccion?> GetByOrdenAsync(string ordenProduccion);
}

public interface IRegistroSanitarioRepository : IGenericRepository<RegistroSanitario>
{
    Task<IEnumerable<RegistroSanitario>> GetPorInsumoAsync(int insumoId);
    Task<IEnumerable<RegistroSanitario>> GetDefectuososAsync();
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

    public async Task<IEnumerable<Proveedor>> GetSugerenciasDeCompraAsync(int top = 5)
    {
        return await _context.Set<Proveedor>()
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.TiempoEntregaDias)
            .Take(top)
            .ToListAsync().ConfigureAwait(false);
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

    public Task<IEnumerable<Insumo>> GetBajoStockAsync()
    {
        return FindAsync(i => i.Activo && i.StockActual <= i.StockMinimo);
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

public class CompraRepository : GenericRepository<Compra>, ICompraRepository
{
    public CompraRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Compra>> GetComprasPorProveedorAsync(int proveedorId)
    {
        return FindAsync(c => c.ProveedorId == proveedorId);
    }

    public Task<IEnumerable<Compra>> GetHistoricoPreciosAsync(int insumoId)
    {
        return FindAsync(c => c.InsumoId == insumoId);
    }

    public Task<IEnumerable<Compra>> GetPendientesAsync()
    {
        return FindAsync(c => c.Estado == PurchaseStatus.Pendiente);
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

public class RegistroSanitarioRepository : GenericRepository<RegistroSanitario>, IRegistroSanitarioRepository
{
    public RegistroSanitarioRepository(AppDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<RegistroSanitario>> GetPorInsumoAsync(int insumoId)
    {
        return FindAsync(r => r.InsumoId == insumoId);
    }

    public Task<IEnumerable<RegistroSanitario>> GetDefectuososAsync()
    {
        return FindAsync(r => !r.Aprobado);
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
