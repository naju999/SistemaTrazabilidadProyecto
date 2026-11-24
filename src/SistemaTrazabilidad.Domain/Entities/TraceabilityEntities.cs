using System.ComponentModel.DataAnnotations.Schema;
using SistemaTrazabilidad.Domain.Common;
using SistemaTrazabilidad.Domain.Enums;

namespace SistemaTrazabilidad.Domain.Entities;

public class Proveedor : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
}

public class Insumo : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
    public decimal StockActual { get; set; }
    [NotMapped]
    public decimal StockDisponible { get; set; }
    [NotMapped]
    public bool TieneDiferenciaStock => StockActual != StockDisponible;
    public bool Activo { get; set; } = true;
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<RegistroSanitario> RegistrosSanitarios { get; set; } = new List<RegistroSanitario>();
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    public ICollection<Produccion> Producciones { get; set; } = new List<Produccion>();
}

public class Lote : BaseEntity
{
    public string CodigoInterno { get; set; } = string.Empty;
    public string CodigoLote { get; set; } = string.Empty;
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public DateTime FechaIngreso { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal CantidadInicial { get; set; }
    public decimal CantidadDisponible { get; set; }
    public bool Bloqueado { get; set; }
    public bool PEPS { get; set; } = true;
    public bool EstaVencido => FechaVencimiento.Date < DateTime.Today;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<Produccion> Producciones { get; set; } = new List<Produccion>();
}

public class Compra : BaseEntity
{
    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public DateTime FechaCompra { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public PurchaseStatus Estado { get; set; } = PurchaseStatus.Pendiente;
    public string Observaciones { get; set; } = string.Empty;

    [NotMapped]
    public decimal Total => Math.Round(Cantidad * PrecioUnitario, 2);
}

public class Produccion : BaseEntity
{
    public string OrdenProduccion { get; set; } = string.Empty;
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public int? LoteId { get; set; }
    public Lote? Lote { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public decimal CantidadProgramada { get; set; }
    public decimal CantidadConsumida { get; set; }
    public ProductionStatus Estado { get; set; } = ProductionStatus.Programada;
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}

public class MovimientoInventario : BaseEntity
{
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public int? LoteId { get; set; }
    public Lote? Lote { get; set; }
    public int? ProduccionId { get; set; }
    public Produccion? Produccion { get; set; }
    public InventoryMovementType Tipo { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockAnterior { get; set; }
    public decimal StockPosterior { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public string Comentario { get; set; } = string.Empty;
}

public class RegistroSanitario : BaseEntity
{
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public DateTime FechaRegistro { get; set; }
    public decimal Temperatura { get; set; }
    public decimal Cloro { get; set; }
    public decimal PH { get; set; }
    public decimal Madurez { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public bool Aprobado { get; set; }
}

public class Auditoria : BaseEntity
{
    public string Entidad { get; set; } = string.Empty;
    public int EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}

public class Alerta : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public AlertType Tipo { get; set; }
    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public int? LoteId { get; set; }
    public Lote? Lote { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public bool Activa { get; set; } = true;
    public bool Leida { get; set; }
    public string EntidadRelacionada { get; set; } = string.Empty;
}

public enum AlertType
{
    StockMinimo = 1,
    Vencimiento = 2,
    Anomalia = 3,
    Sanidad = 4,
    Produccion = 5
}

public enum PurchaseStatus
{
    Pendiente = 1,
    Recibida = 2,
    Cancelada = 3
}

public enum ProductionStatus
{
    Programada = 1,
    EnProceso = 2,
    Finalizada = 3,
    Cancelada = 4
}
