using SistemaTrazabilidad.Domain.Common;

namespace SistemaTrazabilidad.Domain.Entities;

public class Usuario : BaseEntity
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RolId { get; set; }
    public bool Bloqueado { get; set; } = false;
}