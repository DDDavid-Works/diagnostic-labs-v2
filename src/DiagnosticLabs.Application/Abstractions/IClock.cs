namespace DiagnosticLabs.Application.Abstractions;

public interface IClock
{
    /// <summary>Current time in UTC. All stored timestamps are UTC; convert to local only for display.</summary>
    DateTime UtcNow { get; }
}
