using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class PurchaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public PurchaseService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Compra> CreateOrdenCompraAsync(int proveedorId, int insumoId, DateTime fechaCompra, decimal cantidad, decimal precioUnitario, DateTime fechaEntregaEstimada, string observaciones, int usuarioId)
    {
        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad de la orden de compra debe ser mayor que cero.");
        }

        if (precioUnitario < 0)
        {
            throw new BusinessRuleException("El precio unitario debe ser mayor o igual a cero.");
        }

        if (fechaEntregaEstimada < fechaCompra)
        {
            throw new BusinessRuleException("La fecha de entrega estimada debe ser igual o posterior a la fecha de compra.");
        }

        var proveedor = await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId)
            ?? throw new BusinessRuleException("Proveedor no encontrado.");

        if (!proveedor.Activo)
        {
            throw new BusinessRuleException("El proveedor no está activo.");
        }

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("El insumo no está activo.");
        }

        var compra = new Compra
        {
            ProveedorId = proveedorId,
            InsumoId = insumoId,
            FechaCompra = fechaCompra,
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            FechaEntregaEstimada = fechaEntregaEstimada,
            Observaciones = observaciones,
            Estado = PurchaseStatus.Pendiente
        };

        await _unitOfWork.Repository<Compra>().AddAsync(compra);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Compra), compra.Id, "Crear orden de compra", $"Orden de compra creada para el insumo {insumo.Nombre}.", usuarioId);

        return compra;
    }

    public async Task<Compra> RecibirOrdenCompraAsync(int compraId, int usuarioId)
    {
        var compra = await _unitOfWork.Repository<Compra>().GetByIdAsync(compraId)
            ?? throw new BusinessRuleException("Orden de compra no encontrada.");

        if (compra.Estado != PurchaseStatus.Pendiente)
        {
            throw new BusinessRuleException("Solo se pueden recibir órdenes de compra en estado pendiente.");
        }

        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(compra.InsumoId)
            ?? throw new BusinessRuleException("Insumo asociado no encontrado.");

        insumo.StockActual += compra.Cantidad;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        compra.Estado = PurchaseStatus.Recibida;
        _unitOfWork.Repository<Compra>().Update(compra);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Tipo = InventoryMovementType.Compra,
            Cantidad = compra.Cantidad,
            StockAnterior = insumo.StockActual - compra.Cantidad,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = $"Recepción orden {compra.Id}",
            Comentario = "Entrada de inventario por compra recibida"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Compra), compra.Id, "Recibir orden de compra", $"Orden de compra recibida y stock actualizado.", usuarioId);

        return compra;
    }

    public async Task<Compra> CancelarOrdenCompraAsync(int compraId, int usuarioId)
    {
        var compra = await _unitOfWork.Repository<Compra>().GetByIdAsync(compraId)
            ?? throw new BusinessRuleException("Orden de compra no encontrada.");

        if (compra.Estado != PurchaseStatus.Pendiente)
        {
            throw new BusinessRuleException("Solo se pueden cancelar órdenes de compra en estado pendiente.");
        }

        compra.Estado = PurchaseStatus.Cancelada;
        _unitOfWork.Repository<Compra>().Update(compra);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Compra), compra.Id, "Cancelar orden de compra", "Orden de compra cancelada.", usuarioId);

        return compra;
    }

    public async Task<IEnumerable<Compra>> GetHistoricoPreciosAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Compra>().FindAsync(c => c.InsumoId == insumoId);
    }

    public async Task<IEnumerable<Compra>> GetComprasPorProveedorAsync(int proveedorId)
    {
        return await _unitOfWork.Repository<Compra>().FindAsync(c => c.ProveedorId == proveedorId);
    }
}
