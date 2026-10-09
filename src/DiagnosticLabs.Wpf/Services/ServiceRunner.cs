using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>
/// Runs one unit of work against a short-lived DI scope. Application services use a scoped
/// DbContext; a desktop window must not hold one open for its whole lifetime, so each command
/// asks the runner for fresh services instead.
/// </summary>
public interface IServiceRunner
{
    Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull;
}

public sealed class ServiceRunner(IServiceScopeFactory scopeFactory) : IServiceRunner
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
