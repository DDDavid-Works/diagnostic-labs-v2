using System.Security.Cryptography;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>
/// Passwords IT types on each station: four groups of four, no look-alike characters (0/O, 1/l/I),
/// always with an upper case letter, a lower case letter and a digit so Windows password rules accept it.
/// </summary>
internal static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;

    public static string New()
    {
        while (true)
        {
            var chars = RandomNumberGenerator.GetItems<char>(All, 16);
            var text = string.Join('-', chars.Chunk(4).Select(g => new string(g)));
            if (text.Any(Upper.Contains) && text.Any(Lower.Contains) && text.Any(Digits.Contains)) return text;
        }
    }
}
