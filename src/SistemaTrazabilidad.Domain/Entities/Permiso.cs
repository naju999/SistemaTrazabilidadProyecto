using SistemaTrazabilidad.Domain.Common;

namespace SistemaTrazabilidad.Domain.Entities;

public class Permiso : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}