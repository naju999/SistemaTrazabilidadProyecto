using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class SupplierService
{
    private const string SupplierCodePrefix = "PROV";
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public SupplierService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<Proveedor> CreateProveedorAsync(string nombre, string telefono, string email)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new BusinessRuleException("El nombre del proveedor es obligatorio.");
        }

        ValidatePhoneNumber(telefono);
        var codigo = await GenerateNextSupplierCodeAsync();

        var proveedor = new Proveedor
        {
            Codigo = codigo,
            Nombre = nombre,
            Telefono = telefono,
            Email = email,
            Activo = true
        };

        await _unitOfWork.Repository<Proveedor>().AddAsync(proveedor);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Proveedor), proveedor.Id, "Crear proveedor", $"Proveedor {nombre} creado.");

        return proveedor;
    }

    private async Task<string> GenerateNextSupplierCodeAsync()
    {
        var suppliers = await _unitOfWork.Repository<Proveedor>().GetAllAsync();
        var highestSequence = 0;

        foreach (var supplier in suppliers)
        {
            if (supplier.Codigo.StartsWith(SupplierCodePrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(
                    supplier.Codigo.AsSpan(SupplierCodePrefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sequence))
            {
                highestSequence = Math.Max(highestSequence, sequence);
            }
        }

        return $"{SupplierCodePrefix}{checked(highestSequence + 1):D3}";
    }

    public async Task<Proveedor> UpdateProveedorAsync(int proveedorId, string nombre, string telefono, string email, bool activo)
    {
        ValidatePhoneNumber(telefono);
        var proveedor = await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId)
            ?? throw new BusinessRuleException("Proveedor no encontrado.");

        proveedor.Nombre = string.IsNullOrWhiteSpace(nombre) ? proveedor.Nombre : nombre;
        proveedor.Telefono = telefono;
        proveedor.Email = email;
        proveedor.Activo = activo;

        _unitOfWork.Repository<Proveedor>().Update(proveedor);
        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(nameof(Proveedor), proveedor.Id, "Actualizar proveedor", $"Proveedor {proveedor.Nombre} actualizado.");

        return proveedor;
    }

    private static void ValidatePhoneNumber(string telefono)
    {
        if (telefono.Any(character => character is < '0' or > '9'))
        {
            throw new BusinessRuleException("El teléfono solo debe contener números.");
        }
    }

    public async Task<Proveedor?> GetProveedorByIdAsync(int proveedorId)
    {
        return await _unitOfWork.Repository<Proveedor>().GetByIdAsync(proveedorId);
    }

    public async Task<IEnumerable<Proveedor>> GetAllProveedoresAsync()
    {
        return await _unitOfWork.Repository<Proveedor>().GetAllAsync();
    }

}
