using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class ProductionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public ProductionService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Produccion> CreateOrdenProduccionAsync(int insumoId, int loteId, string ordenProduccion, DateTime fechaInicio, decimal cantidadProgramada, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(ordenProduccion))
        {
            throw new BusinessRuleException("El número de orden de producción es obligatorio.");
        }

        if (cantidadProgramada <= 0)
        {
            throw new BusinessRuleException("La cantidad programada debe ser mayor que cero.");
        }

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo no encontrado.");

        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException("Lote no encontrado.");

        if (lote.Bloqueado || lote.EstaVencido)
        {
            throw new BusinessRuleException("No se puede usar un lote bloqueado o vencido para producción.");
        }

        if (lote.InsumoId != insumoId)
        {
            throw new BusinessRuleException("El lote no corresponde al insumo solicitado.");
        }

        var produccion = new Produccion
        {
            OrdenProduccion = ordenProduccion,
            InsumoId = insumoId,
            LoteId = loteId,
            FechaInicio = fechaInicio,
            CantidadProgramada = cantidadProgramada,
            CantidadConsumida = 0,
            Estado = ProductionStatus.Programada
        };

        await _unitOfWork.Repository<Produccion>().AddAsync(produccion);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Produccion), produccion.Id, "Crear orden de producción", $"Orden de producción {ordenProduccion} creada.", usuarioId);

        return produccion;
    }

    public async Task<Produccion> IniciarProduccionAsync(int produccionId, int usuarioId)
    {
        var produccion = await _unitOfWork.Repository<Produccion>().GetByIdAsync(produccionId)
            ?? throw new BusinessRuleException("Orden de producción no encontrada.");

        if (produccion.Estado != ProductionStatus.Programada)
        {
            throw new BusinessRuleException("Solo se pueden iniciar órdenes de producción programadas.");
        }

        produccion.Estado = ProductionStatus.EnProceso;
        _unitOfWork.Repository<Produccion>().Update(produccion);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Produccion), produccion.Id, "Iniciar producción", "Orden de producción iniciada.", usuarioId);

        return produccion;
    }

    public async Task<Produccion> FinalizarProduccionAsync(int produccionId, decimal cantidadConsumida, int usuarioId)
    {
        if (cantidadConsumida <= 0)
        {
            throw new BusinessRuleException("La cantidad consumida debe ser mayor que cero.");
        }

        var produccion = await _unitOfWork.Repository<Produccion>().GetByIdAsync(produccionId)
            ?? throw new BusinessRuleException("Orden de producción no encontrada.");

        if (produccion.Estado != ProductionStatus.EnProceso)
        {
            throw new BusinessRuleException("La orden debe estar en proceso para finalizarla.");
        }

        if (cantidadConsumida > produccion.CantidadProgramada)
        {
            throw new BusinessRuleException("La cantidad consumida no puede ser mayor que la cantidad programada.");
        }

        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(produccion.LoteId)
            ?? throw new BusinessRuleException("Lote asociado no encontrado.");

        if (lote.CantidadDisponible - cantidadConsumida < 0)
        {
            throw new BusinessRuleException("No hay suficiente cantidad disponible en el lote para completar la producción.");
        }

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(produccion.InsumoId)
            ?? throw new BusinessRuleException("Insumo asociado no encontrado.");

        if (insumo.StockActual - cantidadConsumida < 0)
        {
            throw new BusinessRuleException("No hay stock suficiente en el inventario general para completar la producción.");
        }

        var stockAnterior = insumo.StockActual;
        lote.CantidadDisponible -= cantidadConsumida;
        _unitOfWork.Repository<Lote>().Update(lote);

        insumo.StockActual -= cantidadConsumida;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        produccion.CantidadConsumida = cantidadConsumida;
        produccion.Estado = ProductionStatus.Finalizada;
        produccion.FechaFin = DateTime.UtcNow;
        _unitOfWork.Repository<Produccion>().Update(produccion);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            Tipo = InventoryMovementType.Produccion,
            Cantidad = cantidadConsumida,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = produccion.OrdenProduccion,
            Comentario = "Consumo de inventario para producción"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Produccion), produccion.Id, "Finalizar producción", "Orden de producción finalizada.", usuarioId);

        return produccion;
    }

    public async Task<IEnumerable<Produccion>> GetHistoricoProduccionAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Produccion>().FindAsync(p => p.InsumoId == insumoId);
    }
}
