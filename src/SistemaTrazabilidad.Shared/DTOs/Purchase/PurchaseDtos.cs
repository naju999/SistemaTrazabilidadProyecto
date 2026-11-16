using System;

namespace SistemaTrazabilidad.Shared.DTOs.Purchase;

public class CompraCreateDto
{
    public int ProveedorId { get; set; }
    public int InsumoId { get; set; }
    public DateTime FechaCompra { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public string Observaciones { get; set; } = string.Empty;
}

public class CompraUpdateDto
{
    public int Id { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public string Observaciones { get; set; } = string.Empty;
}

public class CompraResponseDto
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public string ProveedorNombre { get; set; } = string.Empty;
    public int InsumoId { get; set; }
    public string InsumoNombre { get; set; } = string.Empty;
    public DateTime FechaCompra { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class CompraFilterDto
{
    public int? ProveedorId { get; set; }
    public int? InsumoId { get; set; }
    public DateTime? FechaCompraDesde { get; set; }
    public DateTime? FechaCompraHasta { get; set; }
    public string? Estado { get; set; }
}
