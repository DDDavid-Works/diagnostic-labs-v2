namespace DiagnosticLabs.Application.Common;

public static class Errors
{
    public static readonly Error Forbidden = new("Auth.Forbidden", "You do not have permission to do that.");

    public static Error NotFound(string what) => new($"{what}.NotFound", $"{what} was not found.");

    public static Error Invalid(IEnumerable<string> messages) => new("Validation.Invalid", string.Join(Environment.NewLine, messages));

    public static Error Conflict(string what) =>
        new($"{what}.Conflict", $"This {what.ToLowerInvariant()} was changed by someone else. Reload it and try again.");
}
