namespace SistemaTrazabilidad.Shared.DTOs.Lots;

public class LotSelectionResultDTO
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public decimal QuantitySelected { get; set; }
    public DateTime ExpirationDate { get; set; }
}