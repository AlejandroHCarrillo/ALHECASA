using System.Security.Cryptography;
using Alhecasa.Application.Abstractions;

namespace Alhecasa.Infrastructure.Identity;

/// <summary>
/// Hash de contraseñas con PBKDF2-HMAC-SHA512 (incluido en .NET), sal aleatoria por contraseña
/// y comparación en tiempo constante. Formato: PBKDF2$SHA512$iteraciones$sal$hash (Base64).
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const string Algorithm = "SHA512";
    private const int Iterations = 210_000; // Recomendación OWASP para PBKDF2-HMAC-SHA512
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;
    private static readonly Lazy<string> DummyHash = new(() => new Pbkdf2PasswordHasher().Hash("dummy-password-for-timing"));

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithm, HashSize);
        return $"{Prefix}${Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 5
            || parts[0] != Prefix
            || parts[1] != Algorithm
            || !int.TryParse(parts[2], out var iterations)
            || iterations is < 1 or > 5_000_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithm, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public void VerifyDummy(string password) => Verify(password, DummyHash.Value);
}
