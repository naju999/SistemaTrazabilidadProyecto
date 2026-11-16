using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task<MovimientoInventario> RegisterEntradaAsync(int insumoId, decimal cantidad, int usuarioId, string referencia = "Entrada de inventario")
    {
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException($"Insumo {insumoId} no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("El insumo está inactivo.");
        }

        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad de entrada debe ser mayor que cero.");
        }

        var stockAnterior = insumo.StockActual;
        insumo.StockActual += cantidad;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Tipo = InventoryMovementType.Entrada,
            Cantidad = cantidad,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = referencia,
            Comentario = "Entrada registrada en inventario"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();

        return movimiento;
    }

    public async Task<MovimientoInventario> RegisterSalidaAsync(int insumoId, decimal cantidad, int usuarioId, string referencia = "Salida de inventario")
    {
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException($"Insumo {insumoId} no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("El insumo está inactivo.");
        }

        if (cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad de salida debe ser mayor que cero.");
        }

        if (insumo.StockActual - cantidad < 0)
        {
            throw new BusinessRuleException("No hay stock suficiente para procesar la salida.");
        }

        var stockAnterior = insumo.StockActual;
        insumo.StockActual -= cantidad;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Tipo = InventoryMovementType.Salida,
            Cantidad = cantidad,
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = referencia,
            Comentario = "Salida registrada en inventario"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();

        return movimiento;
    }

    public async Task<IEnumerable<MovimientoInventario>> GetHistorialAsync(int insumoId)
    {
        return await _unitOfWork.Repository<MovimientoInventario>().FindAsync(m => m.InsumoId == insumoId);
    }

    public async Task<IEnumerable<Insumo>> GetStockActualAsync()
    {
        return await _unitOfWork.Repository<Insumo>().GetAllAsync();
    }

    public async Task<Insumo> AdjustStockAsync(int insumoId, decimal nuevoStock, int usuarioId, string referencia = "Ajuste de stock")
    {
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException($"Insumo {insumoId} no encontrado.");

        if (nuevoStock < 0)
        {
            throw new BusinessRuleException("El stock ajustado no puede ser negativo.");
        }

        var stockAnterior = insumo.StockActual;
        insumo.StockActual = nuevoStock;
        _unitOfWork.Repository<Insumo>().Update(insumo);

        var movimiento = new MovimientoInventario
        {
            InsumoId = insumo.Id,
            Tipo = InventoryMovementType.Ajuste,
            Cantidad = Math.Abs(nuevoStock - stockAnterior),
            StockAnterior = stockAnterior,
            StockPosterior = insumo.StockActual,
            FechaMovimiento = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Referencia = referencia,
            Comentario = "Ajuste de inventario"
        };

        await _unitOfWork.Repository<MovimientoInventario>().AddAsync(movimiento);
        await _unitOfWork.CommitAsync();

        return insumo;
    }
}
