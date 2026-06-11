using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public sealed record SetupWizardMachineInfo(string MachineName);

/// <summary>
/// Result of a login/email sign-in: either the staffer is signed in (single club), or the same
/// login matches several clubs and the caller must pick one (<see cref="Clubs"/>) and re-submit.
/// </summary>
public sealed record SetupWizardLoginResult(
    StaffSignInResponse? SignedIn,
    IReadOnlyList<StaffSignInClubChoice> Clubs);

public sealed record SetupWizardBootstrapConfig(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CredentialId,
    string CredentialSecret,
    string Role,
    string ApiBaseUrl,
    string UpdateChannel,
    string LeaseSigningPublicKeyPem,
    string UpdatePackageSigningPublicKeyPem);

public interface ISetupWizardApiClient
{
    Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken);

    Task<InstallCreateSeatResponse> CreateSeatAsync(
        string ownerCode,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken);

    Task<InstallEnrollResponse> EnrollAsync(InstallEnrollRequest request, CancellationToken cancellationToken);

    Task<StaffSignInResponse> SignInByPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken);

    Task<SetupWizardLoginResult> SignInByLoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken);

    Task<StaffSignInResponse> SignInToClubAsync(
        Guid organizationId,
        string login,
        string password,
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

    Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken);

    Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken);
}

public interface IDeviceKeyStore
{
    Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken);
}

public interface ISetupWizardBootstrapWriter
{
    void Write(SetupWizardBootstrapConfig config);
}

public interface ISetupWizardCompletionAction
{
    void Complete();
}

/// <summary>
/// Starts the Operator App once it has been installed for a manager/cashier workstation. Gaming PCs
/// get their Player Shell launched by the agent service at the lock screen; the operator app has no
/// such trigger, so the wizard launches it directly after a successful install.
/// </summary>
public interface ISetupWizardOperatorLauncher
{
    void Launch();
}
