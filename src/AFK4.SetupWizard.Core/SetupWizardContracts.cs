using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Media;

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
        Guid organizationId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken);

    /// <summary>Оформление клуба: логотип и цвет. Мастер ставит их один раз при установке.</summary>
    Task UpdateBrandingAsync(
        Guid organizationId,
        string accessToken,
        string? logoUrl,
        string? accentColor,
        CancellationToken cancellationToken);

    /// <summary>Приглашение сотрудника: код уходит ему в SMS, пароль он задаёт себе сам.</summary>
    Task<StaffInviteDto> InviteStaffAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string displayName,
        string phoneNumber,
        string roleName,
        CancellationToken cancellationToken);

    /// <summary>Загружает логотип клуба и возвращает его публичный адрес.</summary>
    Task<UploadedMediaDto> UploadOrganizationLogoAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string filePath,
        CancellationToken cancellationToken);

    /// <summary>Первый тариф клуба: имя и цена за час. Возвращает имя созданного тарифа.</summary>
    Task<TariffDto> CreateTariffAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string name,
        long pricePerHourMinorUnits,
        CancellationToken cancellationToken);

    Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Тихая установка: ПК предъявляет код установки вместо входа сотрудника.</summary>
public interface IInstallCodeEnrollmentClient
{
    Task<InstallEnrollResponse> EnrollByCodeAsync(InstallCodeEnrollRequest request, CancellationToken cancellationToken);
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

/// <summary>Перезагрузить ПК: автовход в учётку игрока срабатывает только при запуске Windows.</summary>
public interface ISetupWizardRebootAction
{
    void Reboot();
}

/// <summary>Перезагрузка через shutdown.exe с паузой: мастер успевает ответить экрану и закрыться.</summary>
public sealed class ShutdownRebootAction(IProcessRunner processRunner) : ISetupWizardRebootAction
{
    public void Reboot()
    {
        var shutdown = Path.Combine(Environment.SystemDirectory, "shutdown.exe");
        var result = processRunner.Run(shutdown, ["/r", "/t", "5", "/c", "AFK4: the PC restarts to sign in to the player account.", "/d", "p:4:1"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"shutdown.exe exited with {result.ExitCode}.");
        }
    }
}

/// <summary>
/// Starts the Organization Admin once it has been installed for a manager/cashier workstation. Gaming PCs
/// get their Player Shell launched by the agent service at the lock screen; Organization Admin has no
/// such trigger, so the wizard launches it directly after a successful install.
/// </summary>
public interface ISetupWizardOperatorLauncher
{
    void Launch();
}
