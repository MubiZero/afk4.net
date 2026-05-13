using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Auth;

public interface IOperatorAuthApiClient
{
    Task<StaffSignInResponse> SignInAsync(
        Guid organizationId,
        string userName,
        string password,
        CancellationToken cancellationToken);

    Task<StaffSignInResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);
}
