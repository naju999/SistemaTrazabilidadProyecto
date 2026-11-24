using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class LotService
{
    private readonly IUnitOfWork _unitOfWork;

    public LotService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Lote> CreateLoteAsync(
        int insumoId,
        int proveedorId,
        decimal cantidad,
        DateTime fechaIngreso,
        DateTime fechaVencimiento,
        int? usuarioId = null)
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad del lote debe ser mayor que cero.");
        }

        if (fechaVencimiento <= fechaIngreso)
        {
            throw new BusinessRuleException("La fecha de vencimiento debe ser posterior a la fecha de ingreso.");
        }

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException($"Insumo {insumoId} no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("No se puede generar un lote para un insumo inactivo.");
        }

        var proveedor = await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId)
            ?? throw new BusinessRuleException("Proveedor no encontrado.");

        var codigoLote = LotCodeGenerator.Generate(fechaIngreso);
        var lote = new Lote
        {
            CodigoInterno = codigoLote,
            InsumoId = insumoId,
            ProveedorId = proveedor.Id,
            CodigoLote = codigoLote,
            FechaIngreso = fechaIngreso,
            FechaVencimiento = fechaVencimiento,
            CantidadInicial = cantidad,
            CantidadDisponible = cantidad,
            Bloqueado = false,
            PEPS = true
        };

        var lotesActuales = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == insumoId &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date >= DateTime.Today);
        var stockAnterior = lotesActuales.Sum(l => l.CantidadDisponible);
        await _unitOfWork.Repository<Lote>().AddAsync(lote);
        insumo.StockActual = stockAnterior + cantidad;
        insumo.StockDisponible = insumo.StockActual;
        _unitOfWork.Repository<Insumo>().Update(insumo);
        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Lote = lote,
            Tipo = InventoryMovementType.Entrada,
            Cantidad = cantidad,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = codigoLote,
            Comentario = "Entrada registrada como un nuevo lote."
        });
        await _unitOfWork.CommitAsync();

        return lote;
    }

    public async Task<Lote> SeleccionarLotePEPSAsync(int insumoId, decimal cantidad)
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad solicitada debe ser mayor que cero.");
        }

        var lotes = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == insumoId &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date >= DateTime.Today &&
            l.CantidadDisponible > 0);

        var loteSeleccionado = lotes
            .OrderBy(l => l.FechaIngreso)
            .FirstOrDefault(l => l.CantidadDisponible >= cantidad)
            ?? lotes.OrderBy(l => l.FechaIngreso).FirstOrDefault();

        if (loteSeleccionado == null)
        {
            throw new BusinessRuleException("No hay lotes válidos disponibles para el insumo solicitado.");
        }

        if (loteSeleccionado.CantidadDisponible < cantidad)
        {
            throw new BusinessRuleException("El lote disponible no cubre la cantidad solicitada. Seleccione un lote adicional o reduzca la cantidad.");
        }

        return loteSeleccionado;
    }

    public async Task<Lote> ConsumeLoteAsync(int loteId, decimal cantidad, int usuarioId)
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad a consumir debe ser mayor que cero.");
        }

        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException($"Lote {loteId} no encontrado.");

        if (lote.Bloqueado || lote.EstaVencido)
        {
            throw new BusinessRuleException("No se puede consumir un lote bloqueado o vencido.");
        }

        if (lote.CantidadDisponible - cantidad < 0)
        {
            throw new BusinessRuleException("La cantidad de consumo excede la cantidad disponible del lote.");
        }

        var insumoId = lote.InsumoId
            ?? throw new BusinessRuleException("El lote está desvinculado de un insumo eliminado.");
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("El insumo asociado al lote no existe.");

        var lotesDisponibles = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == insumo.Id &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date >= DateTime.Today);
        var stockAnterior = lotesDisponibles.Sum(l => l.CantidadDisponible);
        if (stockAnterior - cantidad < 0)
        {
            throw new BusinessRuleException("No hay suficiente stock general para consumir de este lote.");
        }

        lote.CantidadDisponible -= cantidad;
        _unitOfWork.Repository<Lote>().Update(lote);

        insumo.StockActual = stockAnterior - cantidad;
        insumo.StockDisponible = insumo.StockActual;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            Tipo = InventoryMovementType.Produccion,
            Cantidad = cantidad,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = "Consumo de lote PEPS para producción",
            Comentario = $"Consumo automático del lote {lote.CodigoInterno}"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();

        return lote;
    }

    public async Task<IEnumerable<Lote>> GetLotesVencidosAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == insumoId &&
            l.FechaVencimiento.Date < DateTime.Today);
    }

    public async Task<IEnumerable<Lote>> GetTrazaHistoricaAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Lote>().FindAsync(l => l.InsumoId == insumoId);
    }

    public async Task<Lote> BlockInvalidLotAsync(int loteId, string razon)
    {
        if (string.IsNullOrWhiteSpace(razon))
        {
            throw new BusinessRuleException("La razón del bloqueo es obligatoria.");
        }

        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException($"Lote {loteId} no encontrado.");

        var eraDisponible = !lote.Bloqueado && !lote.EstaVencido;
        var insumoId = lote.InsumoId
            ?? throw new BusinessRuleException("El lote está desvinculado de un insumo eliminado.");
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("El insumo asociado al lote no existe.");
        var lotesDisponibles = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId == lote.InsumoId &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date >= DateTime.Today);
        var stockAnterior = lotesDisponibles.Sum(l => l.CantidadDisponible);

        lote.Bloqueado = true;
        _unitOfWork.Repository<Lote>().Update(lote);
        var stockPosterior = eraDisponible
            ? stockAnterior - lote.CantidadDisponible
            : stockAnterior;
        insumo.StockActual = stockPosterior;
        insumo.StockDisponible = stockPosterior;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        if (eraDisponible && lote.CantidadDisponible > 0)
        {
            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
            {
                InsumoId = insumo.Id,
                LoteId = lote.Id,
                Tipo = InventoryMovementType.BloqueoLote,
                Cantidad = lote.CantidadDisponible,
                StockAnterior = stockAnterior,
                StockPosterior = stockPosterior,
                FechaMovimiento = DateTime.UtcNow,
                Referencia = lote.CodigoInterno,
                Comentario = $"Stock excluido por bloqueo del lote: {razon}"
            });
        }

        var auditoria = new Auditoria
        {
            Entidad = nameof(Lote),
            EntidadId = lote.Id,
            Accion = "Bloqueo",
            Detalle = razon,
            FechaHora = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Auditoria>().AddAsync(auditoria);
        await _unitOfWork.CommitAsync();

        return lote;
    }
}
