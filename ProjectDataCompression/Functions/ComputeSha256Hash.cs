using System.Security.Cryptography;
using System.Text;

namespace ProjectDataCompression.Functions;

public static class ComputeSha256Hash
{
    public static string Make(string rawData)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToBase64String(bytes);
    }
}