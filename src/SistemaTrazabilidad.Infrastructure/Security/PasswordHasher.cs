using System.Security.Cryptography;
using System.Text;

namespace SistemaTrazabilidad.Infrastructure.Security;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verify(string password, string passwordHash)
    {
        return Hash(password) == passwordHash;
    }
}
