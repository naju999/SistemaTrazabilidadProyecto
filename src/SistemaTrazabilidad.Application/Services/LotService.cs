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

    public async Task<Lote> CreateLoteAsync(int insumoId, string codigoLote, decimal cantidad, DateTime fechaIngreso, DateTime fechaVencimiento)
    {
        if (string.IsNullOrWhiteSpace(codigoLote))
        {
            throw new BusinessRuleException("El código de lote es obligatorio.");
        }

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

        var existingLot = await _unitOfWork.Repository<Lote>().FindAsync(l => l.CodigoLote == codigoLote);
        if (existingLot.Any())
        {
            throw new BusinessRuleException("Ya existe un lote con el mismo código.");
        }

        var lote = new Lote
        {
            InsumoId = insumoId,
            CodigoLote = codigoLote,
            FechaIngreso = fechaIngreso,
            FechaVencimiento = fechaVencimiento,
            CantidadInicial = cantidad,
            CantidadDisponible = cantidad,
            Bloqueado = false,
            PEPS = true
        };

        await _unitOfWork.Repository<Lote>().AddAsync(lote);
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
            !l.EstaVencido &&
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

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(lote.InsumoId)
            ?? throw new BusinessRuleException("El insumo asociado al lote no existe.");

        if (insumo.StockActual - cantidad < 0)
        {
            throw new BusinessRuleException("No hay suficiente stock general para consumir de este lote.");
        }

        lote.CantidadDisponible -= cantidad;
        _unitOfWork.Repository<Lote>().Update(lote);

        insumo.StockActual -= cantidad;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            Tipo = InventoryMovementType.Produccion,
            Cantidad = cantidad,
            StockAnterior = insumo.StockActual + cantidad,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = "Consumo de lote PEPS para producción",
            Comentario = $"Consumo automático del lote {lote.CodigoLote}"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();

        return lote;
    }

    public async Task<IEnumerable<Lote>> GetLotesVencidosAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Lote>().FindAsync(l => l.InsumoId == insumoId && l.EstaVencido);
    }

    public async Task<IEnumerable<Lote>> GetTrazaHistoricaAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Lote>().FindAsync(l => l.InsumoId == insumoId);
    }

    public async Task<Lote> BlockInvalidLotAsync(int loteId, string razon)
    {
        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException($"Lote {loteId} no encontrado.");

        lote.Bloqueado = true;
        _unitOfWork.Repository<Lote>().Update(lote);

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
