using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;

namespace DiagnosticLabs.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthenticatedUser>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}
