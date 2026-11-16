using System;

namespace SistemaTrazabilidad.Shared.DTOs.Sanitary;

public class RegistroSanitarioCreateDto
{
    public int InsumoId { get; set; }
    public DateTime FechaRegistro { get; set; }
    public decimal Temperatura { get; set; }
    public decimal Cloro { get; set; }
    public decimal PH { get; set; }
    public decimal Madurez { get; set; }
    public string Observaciones { get; set; } = string.Empty;
}

public class RegistroSanitarioResponseDto
{
    public int Id { get; set; }
    public int InsumoId { get; set; }
    public string InsumoNombre { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public decimal Temperatura { get; set; }
    public decimal Cloro { get; set; }
    public decimal PH { get; set; }
    public decimal Madurez { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public bool Aprobado { get; set; }
}

public class RegistroSanitarioFilterDto
{
    public int? InsumoId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public bool? Aprobado { get; set; }
}
