using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaTrazabilidad.Domain.Entities;

namespace SistemaTrazabilidad.Persistence.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; } = null!;
    public DbSet<Rol> Roles { get; set; } = null!;
    public DbSet<Permiso> Permisos { get; set; } = null!;
    public DbSet<Proveedor> Proveedores { get; set; } = null!;
    public DbSet<Insumo> Insumos { get; set; } = null!;
    public DbSet<Lote> Lotes { get; set; } = null!;
    public DbSet<Compra> Compras { get; set; } = null!;
    public DbSet<Produccion> Producciones { get; set; } = null!;
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; } = null!;
    public DbSet<RegistroSanitario> RegistrosSanitarios { get; set; } = null!;
    public DbSet<Auditoria> Auditorias { get; set; } = null!;
    public DbSet<Alerta> Alertas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(ConfigureUsuario);
        modelBuilder.Entity<Rol>(entity => entity.HasIndex(e => e.Nombre).IsUnique());
        modelBuilder.Entity<Permiso>(entity => entity.HasIndex(e => e.Codigo).IsUnique());
        modelBuilder.Entity<Proveedor>(entity => entity.HasIndex(e => e.Codigo).IsUnique());
        modelBuilder.Entity<Insumo>(ConfigureInsumo);
        modelBuilder.Entity<Lote>(ConfigureLote);
        modelBuilder.Entity<Compra>(ConfigureCompra);
        modelBuilder.Entity<Produccion>(ConfigureProduccion);
        modelBuilder.Entity<MovimientoInventario>(ConfigureMovimientoInventario);
        modelBuilder.Entity<RegistroSanitario>(ConfigureRegistroSanitario);
        modelBuilder.Entity<Auditoria>(entity => entity.Property(e => e.FechaHora).HasDefaultValueSql("CURRENT_TIMESTAMP"));
        modelBuilder.Entity<Alerta>(ConfigureAlerta);
    }

    private static void ConfigureUsuario(EntityTypeBuilder<Usuario> entity)
    {
        entity.HasIndex(e => e.Username).IsUnique();
        entity.Property(e => e.NombreCompleto).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PasswordHash).IsRequired();
        entity.HasOne<Rol>().WithMany().HasForeignKey(e => e.RolId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureInsumo(EntityTypeBuilder<Insumo> entity)
    {
        entity.HasIndex(e => e.Codigo).IsUnique();
        entity.Property(e => e.Codigo).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        entity.Property(e => e.UnidadMedida).IsRequired().HasMaxLength(50);
        entity.HasCheckConstraint("CHK_Insumo_StockActual", "StockActual >= 0");
        entity.HasCheckConstraint("CHK_Insumo_StockMinimo", "StockMinimo >= 0");
    }

    private static void ConfigureLote(EntityTypeBuilder<Lote> entity)
    {
        entity.HasIndex(e => e.CodigoInterno).IsUnique();
        entity.Property(e => e.CodigoInterno).IsRequired().HasMaxLength(64);
        entity.Property(e => e.CodigoLote).IsRequired().HasMaxLength(150);
        entity.Property(e => e.FechaIngreso).IsRequired();
        entity.Property(e => e.FechaVencimiento).IsRequired();
        entity.HasCheckConstraint("CHK_Lote_CantidadDisponible", "CantidadDisponible >= 0");
        entity.HasOne(e => e.Insumo).WithMany(i => i.Lotes).HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Proveedor).WithMany(p => p.Lotes).HasForeignKey(e => e.ProveedorId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCompra(EntityTypeBuilder<Compra> entity)
    {
        entity.Property(e => e.FechaCompra).IsRequired();
        entity.Property(e => e.Cantidad).IsRequired();
        entity.Property(e => e.PrecioUnitario).IsRequired();
        entity.HasCheckConstraint("CHK_Compra_Cantidad", "Cantidad > 0");
        entity.HasCheckConstraint("CHK_Compra_PrecioUnitario", "PrecioUnitario >= 0");
        entity.HasOne(e => e.Proveedor).WithMany(p => p.Compras).HasForeignKey(e => e.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Insumo).WithMany(i => i.Compras).HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureProduccion(EntityTypeBuilder<Produccion> entity)
    {
        entity.Property(e => e.OrdenProduccion).IsRequired().HasMaxLength(120);
        entity.Property(e => e.FechaInicio).IsRequired();
        entity.HasCheckConstraint("CHK_Produccion_CantidadProgramada", "CantidadProgramada > 0");
        entity.HasOne(e => e.Insumo).WithMany(i => i.Producciones).HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Lote).WithMany(l => l.Producciones).HasForeignKey(e => e.LoteId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureMovimientoInventario(EntityTypeBuilder<MovimientoInventario> entity)
    {
        entity.Property(e => e.Cantidad).IsRequired();
        entity.Property(e => e.FechaMovimiento).IsRequired();
        entity.Property(e => e.StockAnterior).IsRequired();
        entity.Property(e => e.StockPosterior).IsRequired();
        entity.HasCheckConstraint("CHK_Movimiento_Cantidad", "Cantidad > 0");
        entity.HasOne(e => e.Insumo).WithMany(i => i.Movimientos).HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Lote).WithMany(l => l.Movimientos).HasForeignKey(e => e.LoteId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Produccion).WithMany(p => p.Movimientos).HasForeignKey(e => e.ProduccionId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureRegistroSanitario(EntityTypeBuilder<RegistroSanitario> entity)
    {
        entity.Property(e => e.FechaRegistro).IsRequired();
        entity.Property(e => e.Temperatura).IsRequired();
        entity.Property(e => e.Cloro).IsRequired();
        entity.Property(e => e.PH).IsRequired();
        entity.Property(e => e.Madurez).IsRequired();
        entity.HasOne(e => e.Insumo).WithMany(i => i.RegistrosSanitarios).HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAlerta(EntityTypeBuilder<Alerta> entity)
    {
        entity.Property(e => e.Codigo).IsRequired().HasMaxLength(120);
        entity.Property(e => e.Mensaje).IsRequired().HasMaxLength(400);
        entity.Property(e => e.FechaGeneracion).IsRequired();
        entity.HasOne(e => e.Insumo).WithMany().HasForeignKey(e => e.InsumoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Lote).WithMany().HasForeignKey(e => e.LoteId).OnDelete(DeleteBehavior.SetNull);
    }
}
