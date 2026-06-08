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

    Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken);

    Task ResetPasswordByEmailAsync(
        string userNameOrEmail,
        string code,
        string newPassword,
        CancellationToken cancellationToken);

    Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

    Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken);
}
