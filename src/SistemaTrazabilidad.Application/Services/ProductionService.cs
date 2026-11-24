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
    private readonly InventoryService _inventoryService;

    public ProductionService(
        IUnitOfWork unitOfWork,
        AuditService auditService,
        InventoryService inventoryService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _inventoryService = inventoryService;
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

        ordenProduccion = ordenProduccion.Trim();
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo no encontrado.");
        if (!insumo.Activo)
        {
            throw new BusinessRuleException("No se puede programar producción con un insumo inactivo.");
        }

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

        if (lote.CantidadDisponible < cantidadProgramada)
        {
            throw new BusinessRuleException(
                $"El lote no tiene disponibilidad suficiente. Disponible: {lote.CantidadDisponible}; requerido: {cantidadProgramada}.");
        }

        var duplicateOrder = await _unitOfWork.Repository<Produccion>()
            .FindAsync(p => p.OrdenProduccion == ordenProduccion);
        if (duplicateOrder.Any())
        {
            throw new BusinessRuleException("Ya existe una orden de producción con ese número.");
        }

        var stockAnterior = await _inventoryService.GetAvailableQuantityAsync(insumoId);
        if (stockAnterior < cantidadProgramada)
        {
            throw new BusinessRuleException("El stock disponible del insumo no alcanza para reservar la producción.");
        }

        lote.CantidadDisponible -= cantidadProgramada;
        _unitOfWork.Repository<Lote>().Update(lote);
        insumo.StockActual = stockAnterior - cantidadProgramada;
        insumo.StockDisponible = insumo.StockActual;
        _unitOfWork.Repository<Insumo>().Update(insumo);

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
        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            Tipo = InventoryMovementType.ReservaProduccion,
            Cantidad = cantidadProgramada,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = ordenProduccion,
            Comentario = $"Reserva de {cantidadProgramada} para la orden de producción."
        });
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

        var cantidadReservada = await GetReservedQuantityAsync(produccion.OrdenProduccion);
        if (cantidadReservada != produccion.CantidadProgramada)
        {
            throw new BusinessRuleException(
                "La orden no tiene una reserva válida registrada y no se puede iniciar.");
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

        var cantidadReservada = await GetReservedQuantityAsync(produccion.OrdenProduccion);
        if (cantidadReservada != produccion.CantidadProgramada)
        {
            throw new BusinessRuleException(
                "La orden no tiene una reserva válida registrada. Revise las órdenes antiguas antes de finalizarla.");
        }

        if (cantidadConsumida > cantidadReservada)
        {
            throw new BusinessRuleException("La cantidad consumida no puede superar lo reservado para la orden.");
        }

        var loteId = produccion.LoteId
            ?? throw new BusinessRuleException("La orden no tiene un lote asociado.");
        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException("Lote asociado no encontrado.");

        var insumoId = produccion.InsumoId
            ?? throw new BusinessRuleException("La orden no tiene un insumo asociado.");
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo asociado no encontrado.");

        var cantidadLiberada = cantidadReservada - cantidadConsumida;
        var stockAnterior = await _inventoryService.GetAvailableQuantityAsync(insumo.Id);
        lote.CantidadDisponible += cantidadLiberada;
        _unitOfWork.Repository<Lote>().Update(lote);

        var stockPosterior = lote.Bloqueado || lote.EstaVencido
            ? stockAnterior
            : stockAnterior + cantidadLiberada;
        insumo.StockActual = stockPosterior;
        insumo.StockDisponible = stockPosterior;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        produccion.CantidadConsumida = cantidadConsumida;
        produccion.Estado = ProductionStatus.Finalizada;
        produccion.FechaFin = DateTime.UtcNow;
        _unitOfWork.Repository<Produccion>().Update(produccion);

        var movimientoConsumo = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            ProduccionId = produccion.Id,
            Tipo = InventoryMovementType.Produccion,
            Cantidad = cantidadConsumida,
            StockAnterior = stockAnterior,
            StockPosterior = stockAnterior,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = produccion.OrdenProduccion,
            Comentario = "Consumo de material previamente reservado para producción."
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimientoConsumo);
        if (cantidadLiberada > 0)
        {
            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
            {
                InsumoId = insumo.Id,
                LoteId = lote.Id,
                ProduccionId = produccion.Id,
                Tipo = InventoryMovementType.LiberacionReservaProduccion,
                Cantidad = cantidadLiberada,
                StockAnterior = stockAnterior,
                StockPosterior = stockPosterior,
                FechaMovimiento = DateTime.UtcNow,
                UsuarioId = usuarioId,
                Referencia = produccion.OrdenProduccion,
                Comentario = "Liberación de la parte no consumida de la reserva."
            });
        }
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Produccion), produccion.Id, "Finalizar producción", "Orden de producción finalizada.", usuarioId);

        return produccion;
    }

    public async Task<Produccion> CancelarProduccionAsync(int produccionId, int usuarioId)
    {
        var produccion = await _unitOfWork.Repository<Produccion>().GetByIdAsync(produccionId)
            ?? throw new BusinessRuleException("Orden de producción no encontrada.");

        if (produccion.Estado is not (ProductionStatus.Programada or ProductionStatus.EnProceso))
        {
            throw new BusinessRuleException("Solo se pueden cancelar órdenes programadas o en proceso.");
        }

        var cantidadReservada = await GetReservedQuantityAsync(produccion.OrdenProduccion);
        if (cantidadReservada != produccion.CantidadProgramada)
        {
            throw new BusinessRuleException(
                "La orden no tiene una reserva válida registrada. No se puede liberar inventario automáticamente.");
        }

        var loteId = produccion.LoteId
            ?? throw new BusinessRuleException("La orden no tiene un lote asociado.");
        var lote = await _unitOfWork.Repository<Lote>().GetByIdAsync(loteId)
            ?? throw new BusinessRuleException("Lote asociado no encontrado.");
        var insumoId = produccion.InsumoId
            ?? throw new BusinessRuleException("La orden no tiene un insumo asociado.");
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo asociado no encontrado.");

        var stockAnterior = await _inventoryService.GetAvailableQuantityAsync(insumo.Id);
        lote.CantidadDisponible += cantidadReservada;
        _unitOfWork.Repository<Lote>().Update(lote);

        var stockPosterior = lote.Bloqueado || lote.EstaVencido
            ? stockAnterior
            : stockAnterior + cantidadReservada;
        insumo.StockActual = stockPosterior;
        insumo.StockDisponible = stockPosterior;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        produccion.Estado = ProductionStatus.Cancelada;
        produccion.FechaFin = DateTime.UtcNow;
        _unitOfWork.Repository<Produccion>().Update(produccion);

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
        {
            InsumoId = insumo.Id,
            LoteId = lote.Id,
            ProduccionId = produccion.Id,
            Tipo = InventoryMovementType.LiberacionReservaProduccion,
            Cantidad = cantidadReservada,
            StockAnterior = stockAnterior,
            StockPosterior = stockPosterior,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = produccion.OrdenProduccion,
            Comentario = "Liberación de la reserva por cancelación de la orden."
        });

        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(
            nameof(Produccion), produccion.Id, "Cancelar producción",
            $"Se liberaron {cantidadReservada} unidades reservadas.", usuarioId);

        return produccion;
    }

    private async Task<decimal> GetReservedQuantityAsync(string orderNumber)
    {
        var movements = await _unitOfWork.Repository<MovimientoInventario>().FindAsync(m =>
            m.Referencia == orderNumber &&
            (m.Tipo == InventoryMovementType.ReservaProduccion ||
             m.Tipo == InventoryMovementType.LiberacionReservaProduccion));

        return movements.Sum(m => m.Tipo == InventoryMovementType.ReservaProduccion
            ? m.Cantidad
            : -m.Cantidad);
    }

    public async Task<IEnumerable<Produccion>> GetHistoricoProduccionAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Produccion>().FindAsync(p => p.InsumoId == insumoId);
    }
}
