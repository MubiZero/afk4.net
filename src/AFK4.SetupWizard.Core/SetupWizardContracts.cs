using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public enum SetupWizardStep
{
    OwnerCode,
    BranchSelection,
    SeatSelection,
    RoleSelection,
    Finished
}

public sealed record SetupWizardMachineInfo(string MachineName);

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
