using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class SupplierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public SupplierService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Proveedor> CreateProveedorAsync(string codigo, string nombre, string telefono, string email, int tiempoEntregaDias)
    {
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombre))
        {
            throw new BusinessRuleException("El código y nombre del proveedor son obligatorios.");
        }

        if (tiempoEntregaDias < 0)
        {
            throw new BusinessRuleException("El tiempo de entrega debe ser un número positivo.");
        }

        var existing = (await _unitOfWork.Repository<Proveedor>().FindAsync(p => p.Codigo == codigo)).FirstOrDefault();
        if (existing != null)
        {
            throw new BusinessRuleException("Ya existe un proveedor con el mismo código.");
        }

        var proveedor = new Proveedor
        {
            Codigo = codigo,
            Nombre = nombre,
            Telefono = telefono,
            Email = email,
            TiempoEntregaDias = tiempoEntregaDias,
            Activo = true
        };

        await _unitOfWork.Repository<Proveedor>().AddAsync(proveedor);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Proveedor), proveedor.Id, "Crear proveedor", $"Proveedor {nombre} creado.");

        return proveedor;
    }

    public async Task<Proveedor> UpdateProveedorAsync(int proveedorId, string nombre, string telefono, string email, int tiempoEntregaDias, bool activo)
    {
        var proveedor = await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId)
            ?? throw new BusinessRuleException("Proveedor no encontrado.");

        proveedor.Nombre = string.IsNullOrWhiteSpace(nombre) ? proveedor.Nombre : nombre;
        proveedor.Telefono = telefono;
        proveedor.Email = email;
        proveedor.TiempoEntregaDias = tiempoEntregaDias;
        proveedor.Activo = activo;

        _unitOfWork.Repository<Proveedor>().Update(proveedor);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Proveedor), proveedor.Id, "Actualizar proveedor", $"Proveedor {proveedor.Nombre} actualizado.");

        return proveedor;
    }

    public async Task<Proveedor?> GetProveedorByIdAsync(int proveedorId)
    {
        return await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId);
    }

    public async Task<IEnumerable<Proveedor>> GetAllProveedoresAsync()
    {
        return await _unitOfWork.Repository<Proveedor>().GetAllAsync();
    }

    public async Task<IEnumerable<Proveedor>> GetSugerenciasDeCompraAsync(int top = 5)
    {
        var insumosEnRiesgo = await _unitOfWork.Repository<Insumo>().FindAsync(i => i.Activo && i.StockActual <= i.StockMinimo);
        var proveedoresActivos = await _unitOfWork.Repository<Proveedor>().FindAsync(p => p.Activo);

        if (!insumosEnRiesgo.Any())
        {
            return proveedoresActivos.OrderBy(p => p.TiempoEntregaDias).Take(top);
        }

        return proveedoresActivos.OrderBy(p => p.TiempoEntregaDias).Take(top);
    }
}
