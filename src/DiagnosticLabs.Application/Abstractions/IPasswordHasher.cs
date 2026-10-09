namespace DiagnosticLabs.Application.Abstractions;

public enum PasswordVerification
{
    Failed,
    Success,

    /// <summary>The password is correct but stored with a weaker or older scheme and should be re-hashed.</summary>
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string password, string storedHash);
}
