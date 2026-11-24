using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class InventoryService
{
    private readonly IUnitOfWork _unitOfWork;

    public InventoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MovimientoInventario> RegisterEntradaAsync(
        int insumoId,
        int proveedorId,
        decimal cantidad,
        DateTime fechaVencimiento,
        int usuarioId,
        string referencia = "Entrada de inventario")
    {
        var movimiento = await PrepareEntradaAsync(
            insumoId, proveedorId, cantidad, fechaVencimiento, usuarioId, referencia);
        await _unitOfWork.CommitAsync();
        return movimiento;
    }

    internal async Task<MovimientoInventario> PrepareEntradaAsync(
        int insumoId,
        int proveedorId,
        decimal cantidad,
        DateTime fechaVencimiento,
        int usuarioId,
        string referencia)
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad de entrada debe ser mayor que cero.");
        }

        var fechaIngreso = DateTime.Today;
        if (fechaVencimiento.Date <= fechaIngreso)
        {
            throw new BusinessRuleException("La fecha de vencimiento debe ser posterior a la fecha de ingreso.");
        }

        var insumo = await GetActiveInputAsync(insumoId);
        var proveedor = await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId)
            ?? throw new BusinessRuleException("El proveedor seleccionado no existe.");
        if (!proveedor.Activo)
        {
            throw new BusinessRuleException("El proveedor seleccionado está inactivo.");
        }

        var stockAnterior = await GetAvailableQuantityAsync(insumoId);
        var codigoLote = LotCodeGenerator.Generate(fechaIngreso);
        var lote = new Lote
        {
            CodigoInterno = codigoLote,
            InsumoId = insumoId,
            ProveedorId = proveedor.Id,
            CodigoLote = codigoLote,
            FechaIngreso = fechaIngreso,
            FechaVencimiento = fechaVencimiento.Date,
            CantidadInicial = cantidad,
            CantidadDisponible = cantidad,
            Bloqueado = false,
            PEPS = true
        };

        await _unitOfWork.Repository<Lote>().AddAsync(lote);
        insumo.StockActual = stockAnterior + cantidad;
        insumo.StockDisponible = insumo.StockActual;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = CreateMovement(
            insumo,
            lote,
            InventoryMovementType.Entrada,
            cantidad,
            stockAnterior,
            insumo.StockActual,
            usuarioId,
            referencia,
            "Entrada registrada como un nuevo lote.");
        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);

        return movimiento;
    }

    public async Task<IEnumerable<MovimientoInventario>> RegisterSalidaAsync(
        int insumoId,
        decimal cantidad,
        int usuarioId,
        string referencia = "Salida de inventario")
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad de salida debe ser mayor que cero.");
        }

        var insumo = await GetActiveInputAsync(insumoId);
        var lotesDisponibles = (await _unitOfWork.Repository<Lote>().FindAsync(l =>
                l.InsumoId == insumoId &&
                !l.Bloqueado &&
                l.FechaVencimiento.Date >= DateTime.Today &&
                l.CantidadDisponible > 0))
            .OrderBy(l => l.FechaIngreso)
            .ToList();
        var stockAnterior = lotesDisponibles.Sum(l => l.CantidadDisponible);

        if (stockAnterior < cantidad)
        {
            throw new BusinessRuleException(
                $"Stock disponible insuficiente. Disponible: {stockAnterior}; solicitado: {cantidad}.");
        }

        var restante = cantidad;
        var stockActual = stockAnterior;
        var movimientos = new List<MovimientoInventario>();

        foreach (var loteSnapshot in lotesDisponibles)
        {
            if (restante == 0)
            {
                break;
            }

            var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteSnapshot.Id)
                ?? throw new BusinessRuleException($"El lote {loteSnapshot.CodigoLote} ya no existe.");
            var cantidadLote = Math.Min(lote.CantidadDisponible, restante);
            lote.CantidadDisponible -= cantidadLote;
            _unitOfWork.Repository<Lote>().Update(lote);

            var stockPosterior = stockActual - cantidadLote;
            movimientos.Add(CreateMovement(
                insumo,
                lote,
                InventoryMovementType.Salida,
                cantidadLote,
                stockActual,
                stockPosterior,
                usuarioId,
                referencia,
                $"Salida PEPS del lote {lote.CodigoInterno}."));

            restante -= cantidadLote;
            stockActual = stockPosterior;
        }

        insumo.StockActual = stockActual;
        insumo.StockDisponible = stockActual;
        _unitOfWork.Repository<Insumo>().Update(insumo);
        foreach (var movimiento in movimientos)
        {
            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        }
        await _unitOfWork.CommitAsync();

        return movimientos;
    }

    public async Task<IEnumerable<MovimientoInventario>> GetHistorialAsync(int insumoId)
    {
        return await _unitOfWork.Repository<MovimientoInventario>()
            .FindAsync(m => m.InsumoId == insumoId);
    }

    public async Task<IEnumerable<Insumo>> GetStockActualAsync()
    {
        var insumos = (await _unitOfWork.Repository<Insumo>().GetAllAsync()).ToList();
        var lotes = await _unitOfWork.Repository<Lote>().GetAllAsync();

        foreach (var insumo in insumos)
        {
            insumo.StockDisponible = lotes
                .Where(l => l.InsumoId == insumo.Id && !l.Bloqueado && !l.EstaVencido)
                .Sum(l => l.CantidadDisponible);
        }

        return insumos;
    }

    public async Task<IEnumerable<StockReconciliation>> GetStockDiscrepanciesAsync()
    {
        var insumos = await GetStockActualAsync();
        return insumos
            .Where(i => i.StockActual != i.StockDisponible)
            .Select(i => new StockReconciliation(
                i.Id, i.Codigo, i.Nombre, i.StockActual, i.StockDisponible))
            .ToList();
    }

    public async Task<Insumo> AdjustStockAsync(
        int insumoId,
        decimal nuevoStock,
        int usuarioId,
        string referencia = "Ajuste de stock")
    {
        if (nuevoStock < 0)
        {
            throw new BusinessRuleException("El stock ajustado no puede ser negativo.");
        }

        var insumo = await GetActiveInputAsync(insumoId);
        var lotes = (await _unitOfWork.Repository<Lote>().FindAsync(l =>
                l.InsumoId == insumoId &&
                !l.Bloqueado &&
                l.FechaVencimiento.Date >= DateTime.Today &&
                l.CantidadDisponible > 0))
            .OrderBy(l => l.FechaIngreso)
            .ToList();
        var stockAnterior = lotes.Sum(l => l.CantidadDisponible);

        if (nuevoStock > stockAnterior)
        {
            throw new BusinessRuleException(
                "Un ajuste no puede aumentar el stock sin identificar su lote. Registre una entrada con código y vencimiento.");
        }

        var restante = stockAnterior - nuevoStock;
        if (restante == 0)
        {
            insumo.StockDisponible = stockAnterior;
            return insumo;
        }

        var stockActual = stockAnterior;
        foreach (var loteSnapshot in lotes)
        {
            if (restante == 0)
            {
                break;
            }

            var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteSnapshot.Id)
                ?? throw new BusinessRuleException($"El lote {loteSnapshot.CodigoLote} ya no existe.");
            var cantidadAjuste = Math.Min(lote.CantidadDisponible, restante);
            lote.CantidadDisponible -= cantidadAjuste;
            _unitOfWork.Repository<Lote>().Update(lote);

            var stockPosterior = stockActual - cantidadAjuste;
            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(CreateMovement(
                insumo,
                lote,
                InventoryMovementType.Ajuste,
                cantidadAjuste,
                stockActual,
                stockPosterior,
                usuarioId,
                referencia,
                $"Ajuste de disponibilidad del lote {lote.CodigoInterno}."));

            restante -= cantidadAjuste;
            stockActual = stockPosterior;
        }

        insumo.StockActual = nuevoStock;
        insumo.StockDisponible = nuevoStock;
        _unitOfWork.Repository<Insumo>().Update(insumo);
        await _unitOfWork.CommitAsync();

        return insumo;
    }

    public async Task<decimal> GetAvailableQuantityAsync(int insumoId)
    {
        var lotes = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == insumoId &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date >= DateTime.Today &&
            l.CantidadDisponible > 0);
        return lotes.Sum(l => l.CantidadDisponible);
    }

    private async Task<Insumo> GetActiveInputAsync(int insumoId)
    {
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException($"Insumo {insumoId} no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("El insumo está inactivo.");
        }

        return insumo;
    }

    private static MovimientoInventario CreateMovement(
        Insumo insumo,
        Lote lote,
        InventoryMovementType type,
        decimal quantity,
        decimal stockBefore,
        decimal stockAfter,
        int userId,
        string reference,
        string comment)
    {
        return new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Lote = lote,
            Tipo = type,
            Cantidad = quantity,
            StockAnterior = stockBefore,
            StockPosterior = stockAfter,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = userId,
            Referencia = reference,
            Comentario = comment
        };
    }
}

internal static class LotCodeGenerator
{
    public static string Generate(DateTime date)
    {
        var suffix = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        return $"LOT-{date:yyyyMMdd}-{suffix}";
    }
}

public sealed record StockReconciliation(
    int InsumoId,
    string Codigo,
    string Nombre,
    decimal StockRegistrado,
    decimal StockDisponibleCalculado)
{
    public decimal Diferencia => StockRegistrado - StockDisponibleCalculado;
}
