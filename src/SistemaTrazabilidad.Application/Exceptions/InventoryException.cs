namespace SistemaTrazabilidad.Application.Exceptions;

public class InventoryException : BusinessException
{
    public InventoryException(string message) : base(message)
    {
    }
}