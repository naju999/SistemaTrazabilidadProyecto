using System;

namespace SistemaTrazabilidad.Shared.DTOs.Production;

public class ProduccionCreateDto
{
    public int InsumoId { get; set; }
    public int LoteId { get; set; }
    public string OrdenProduccion { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public decimal CantidadProgramada { get; set; }
}

public class ProduccionUpdateDto
{
    public int Id { get; set; }
    public DateTime? FechaFin { get; set; }
    public decimal CantidadConsumida { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ProduccionResponseDto
{
    public int Id { get; set; }
    public string OrdenProduccion { get; set; } = string.Empty;
    public int InsumoId { get; set; }
    public string InsumoNombre { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string LoteCodigo { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public decimal CantidadProgramada { get; set; }
    public decimal CantidadConsumida { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ProduccionFilterDto
{
    public int? InsumoId { get; set; }
    public int? LoteId { get; set; }
    public string? Estado { get; set; }
    public DateTime? FechaInicioDesde { get; set; }
    public DateTime? FechaInicioHasta { get; set; }
}
