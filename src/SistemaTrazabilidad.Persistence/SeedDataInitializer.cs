using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Infrastructure.Security;
using SistemaTrazabilidad.Persistence.Context;

namespace SistemaTrazabilidad.Persistence;

public class SeedDataInitializer
{
    private readonly AppDbContext _context;

    public SeedDataInitializer(AppDbContext context)
    {
        _context = context;
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();

        if (await _context.Roles.FindAsync(1) != null)
            return; // Ya existe seed data

        try
        {
            // 1. Crear Roles
            var adminRole = new Rol { Nombre = "Administrador" };
            var operatorRole = new Rol { Nombre = "Operador" };
            var supervisorRole = new Rol { Nombre = "Supervisor" };

            _context.Roles.Add(adminRole);
            _context.Roles.Add(operatorRole);
            _context.Roles.Add(supervisorRole);
            await _context.SaveChangesAsync();

            // 2. Crear Permisos
            var permisos = new List<Permiso>
            {
                new Permiso { Codigo = "ADMIN_FULL", Descripcion = "Acceso total al sistema" },
                new Permiso { Codigo = "INVENTORY_READ", Descripcion = "Consultar insumos y stock" },
                new Permiso { Codigo = "INVENTORY_WRITE", Descripcion = "Crear y actualizar insumos" },
                new Permiso { Codigo = "PURCHASE_READ", Descripcion = "Consultar órdenes de compra" },
                new Permiso { Codigo = "PURCHASE_WRITE", Descripcion = "Crear y recibir compras" },
                new Permiso { Codigo = "PRODUCTION_READ", Descripcion = "Consultar órdenes de producción" },
                new Permiso { Codigo = "PRODUCTION_WRITE", Descripcion = "Crear y ejecutar órdenes" },
                new Permiso { Codigo = "ALERTS_VIEW", Descripcion = "Consultar alertas activas" },
                new Permiso { Codigo = "AUDIT_VIEW", Descripcion = "Consultar historial de cambios" },
            };

            _context.Permisos.AddRange(permisos);
            await _context.SaveChangesAsync();

            // 3. Crear Usuario Administrador
            var adminUser = new Usuario
            {
                Username = "admin",
                NombreCompleto = "Administrador Sistema",
                PasswordHash = PasswordHasher.Hash("admin123"),
                RolId = adminRole.Id,
                Bloqueado = false
            };

            _context.Usuarios.Add(adminUser);
            await _context.SaveChangesAsync();

            // 4. Crear Proveedores de ejemplo
            var proveedores = new List<Proveedor>
            {
                new Proveedor
                {
                    Codigo = "PROV001",
                    Nombre = "Proveedora de Embutidos S.A.",
                    Telefono = "555-0001",
                    Email = "contacto@embutidos.com",
                    TiempoEntregaDias = 2,
                    Activo = true
                },
                new Proveedor
                {
                    Codigo = "PROV002",
                    Nombre = "Distribuidora de Ingredientes La Costena",
                    Telefono = "555-0002",
                    Email = "ventas@lacostena.com",
                    TiempoEntregaDias = 3,
                    Activo = true
                },
                new Proveedor
                {
                    Codigo = "PROV003",
                    Nombre = "Especias y Condimentos Premium",
                    Telefono = "555-0003",
                    Email = "info@especiaspremium.com",
                    TiempoEntregaDias = 5,
                    Activo = true
                }
            };

            _context.Proveedores.AddRange(proveedores);
            await _context.SaveChangesAsync();

            // 5. Crear Insumos de ejemplo
            var insumos = new List<Insumo>
            {
                new Insumo
                {
                    Codigo = "INS001",
                    Nombre = "Jamón Serrano 100g",
                    UnidadMedida = "kg",
                    StockActual = 50,
                    StockMinimo = 10,
                    Activo = true
                },
                new Insumo
                {
                    Codigo = "INS002",
                    Nombre = "Queso Manchego",
                    UnidadMedida = "kg",
                    StockActual = 30,
                    StockMinimo = 5,
                    Activo = true
                },
                new Insumo
                {
                    Codigo = "INS003",
                    Nombre = "Pan de Trigo Integral",
                    UnidadMedida = "unidad",
                    StockActual = 200,
                    StockMinimo = 50,
                    Activo = true
                },
                new Insumo
                {
                    Codigo = "INS004",
                    Nombre = "Tomate",
                    UnidadMedida = "kg",
                    StockActual = 15,
                    StockMinimo = 5,
                    Activo = true
                },
                new Insumo
                {
                    Codigo = "INS005",
                    Nombre = "Lechuga",
                    UnidadMedida = "kg",
                    StockActual = 25,
                    StockMinimo = 8,
                    Activo = true
                }
            };

            _context.Insumos.AddRange(insumos);
            await _context.SaveChangesAsync();

            // 6. Crear Lotes de ejemplo
            var lotes = new List<Lote>();
            foreach (var insumo in insumos)
            {
                lotes.Add(new Lote
                {
                    CodigoLote = $"LOTE-{insumo.Codigo}-{DateTime.Now:yyyyMMdd}-001",
                    InsumoId = insumo.Id,
                    FechaIngreso = DateTime.Now.AddDays(-5),
                    FechaVencimiento = DateTime.Now.AddDays(30),
                    CantidadInicial = insumo.StockActual,
                    CantidadDisponible = insumo.StockActual,
                    Bloqueado = false
                });
            }

            _context.Lotes.AddRange(lotes);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("Error inicializando seed data", ex);
        }
    }
}
