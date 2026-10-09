using DiagnosticLabs.Application.Abstractions;

namespace DiagnosticLabs.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
