using System.Security.Cryptography;

namespace DiagnosticLabs.Application.Auth;

/// <summary>The rules every new password must meet, and a generator for temporary passwords.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    // No look-alike characters (0/O, 1/l/I) so a temporary password can be read out over the phone.
    private const string GeneratorAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static IReadOnlyList<string> Validate(string? password, string? username = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password can not be empty.");
            return errors;
        }

        if (password.Length < MinLength)
            errors.Add($"Password must be at least {MinLength} characters long.");
        else if (password.Length > MaxLength)
            errors.Add($"Password can not be longer than {MaxLength} characters.");

        if (!string.IsNullOrWhiteSpace(username) && string.Equals(password.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
            errors.Add("Password can not be the same as the username.");

        return errors;
    }

    /// <summary>A random password that always satisfies <see cref="Validate"/>.</summary>
    public static string Generate(int length = 10)
    {
        length = Math.Max(length, MinLength);
        return string.Create(length, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = GeneratorAlphabet[RandomNumberGenerator.GetInt32(GeneratorAlphabet.Length)];
        });
    }
}
