using System;

namespace SistemaTrazabilidad.Shared.DTOs.Alert;

public class AlertCreateDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int? InsumoId { get; set; }
    public int? LoteId { get; set; }
    public string EntidadRelacionada { get; set; } = string.Empty;
}

public class AlertResponseDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int? InsumoId { get; set; }
    public int? LoteId { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public bool Activa { get; set; }
    public bool Leida { get; set; }
    public string EntidadRelacionada { get; set; } = string.Empty;
}

public class AlertFilterDto
{
    public bool? Activa { get; set; }
    public bool? Leida { get; set; }
    public int? InsumoId { get; set; }
    public int? LoteId { get; set; }
    public string? Tipo { get; set; }
}
