using SistemaTrazabilidad.Domain.Common;

namespace SistemaTrazabilidad.Domain.Entities;

public class Rol : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
}