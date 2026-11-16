using System;

namespace SistemaTrazabilidad.Shared.DTOs.Traceability;

public class ProveedorCreateDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TiempoEntregaDias { get; set; }
}

public class ProveedorUpdateDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TiempoEntregaDias { get; set; }
    public bool Activo { get; set; }
}

public class ProveedorResponseDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TiempoEntregaDias { get; set; }
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProveedorFilterDto
{
    public string? Nombre { get; set; }
    public bool? Activo { get; set; }
    public int? MaxTiempoEntregaDias { get; set; }
}

public class InsumoCreateDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
}

public class InsumoUpdateDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
    public bool Activo { get; set; }
}

public class InsumoResponseDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockMinimo { get; set; }
    public decimal StockActual { get; set; }
    public bool Activo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class InsumoFilterDto
{
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public bool? Activo { get; set; }
    public bool? BajoStock { get; set; }
}

public class LoteCreateDto
{
    public int InsumoId { get; set; }
    public string CodigoLote { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal CantidadInicial { get; set; }
}

public class LoteUpdateDto
{
    public int Id { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public bool Bloqueado { get; set; }
}

public class LoteResponseDto
{
    public int Id { get; set; }
    public string CodigoLote { get; set; } = string.Empty;
    public int InsumoId { get; set; }
    public DateTime FechaIngreso { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal CantidadInicial { get; set; }
    public decimal CantidadDisponible { get; set; }
    public bool Bloqueado { get; set; }
    public bool PEPS { get; set; }
    public bool EstaVencido { get; set; }
}

public class LoteFilterDto
{
    public int? InsumoId { get; set; }
    public string? CodigoLote { get; set; }
    public bool? Bloqueado { get; set; }
    public bool? Vencido { get; set; }
}
