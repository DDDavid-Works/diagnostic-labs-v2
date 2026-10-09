using System.Security.Cryptography;
using System.Text;
using DiagnosticLabs.Application.Abstractions;

namespace DiagnosticLabs.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a per-password salt. Stored as <c>v1$iterations$salt$hash</c> (Base64).
/// Also recognises the legacy unsalted SHA-1 hex digest so existing users can sign in once
/// and be upgraded.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private const int LegacySha1HexLength = 40;

    private static readonly byte[] DummySalt = new byte[SaltSize];

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Derive(password, salt, Iterations);
        return $"{Version}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public PasswordVerification Verify(string password, string storedHash)
    {
        if (IsLegacySha1(storedHash))
        {
            var legacy = SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var expected = Convert.FromHexString(storedHash);
            return CryptographicOperations.FixedTimeEquals(legacy, expected)
                ? PasswordVerification.SuccessRehashNeeded
                : PasswordVerification.Failed;
        }

        if (!TryParse(storedHash, out var iterations, out var salt, out var expectedKey))
        {
            // Burn comparable time for missing/unknown hashes so callers cannot tell them apart.
            _ = Derive(password, DummySalt, Iterations);
            return PasswordVerification.Failed;
        }

        var actual = Derive(password, salt, iterations);
        if (!CryptographicOperations.FixedTimeEquals(actual, expectedKey))
            return PasswordVerification.Failed;

        return iterations < Iterations
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Success;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);

    private static bool IsLegacySha1(string hash) =>
        hash.Length == LegacySha1HexLength && hash.All(Uri.IsHexDigit);

    private static bool TryParse(string stored, out int iterations, out byte[] salt, out byte[] key)
    {
        iterations = 0;
        salt = [];
        key = [];

        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out iterations) || iterations <= 0)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            key = Convert.FromBase64String(parts[3]);
            return salt.Length > 0 && key.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
