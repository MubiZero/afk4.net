using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Auth;

public sealed class UnconfiguredOperatorAuthApiClient : IOperatorAuthApiClient
{
    public Task<StaffSignInResponse> SignInAsync(
        Guid organizationId,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        return NotConfigured();
    }

    public Task<StaffSignInResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return NotConfigured();
    }

    private static Task<StaffSignInResponse> NotConfigured()
    {
        throw new InvalidOperationException("Operator auth API client is not configured.");
    }
}
