using AFK4.Shared.Contracts.Identity;

namespace AFK4.OrganizationAdmin.App.Auth;

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

    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        => NotConfiguredVoid();

    public Task ResetPasswordByEmailAsync(
        string userNameOrEmail,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => NotConfiguredVoid();

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        => NotConfiguredVoid();

    public Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => NotConfiguredVoid();

    private static Task<StaffSignInResponse> NotConfigured()
    {
        throw new InvalidOperationException("Operator auth API client is not configured.");
    }

    private static Task NotConfiguredVoid()
    {
        throw new InvalidOperationException("Operator auth API client is not configured.");
    }
}
