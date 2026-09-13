using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public interface IStaffCredentialService
{
    Task<StaffSignInResponse?> SignInAsync(StaffSignInRequest request, CancellationToken cancellationToken);

    Task<StaffSignInResponse?> SignInByOrganizationKeyAsync(
        StaffSignInByOrganizationKeyRequest request,
        CancellationToken cancellationToken);

    /// <param name="organizationId">
    /// Организация, в которой искать учётную запись, или <c>null</c> — «искать по всей сети».
    /// Второе нужно мастеру установки: он входит до того, как организация известна, и именно
    /// из ответа её и узнаёт. Сотрудник, работающий в нескольких клубах, при этом получает
    /// список на выбор — ровно тот случай, ради которого заведён
    /// <see cref="StaffLoginResolution.Clubs"/>.
    /// </param>
    Task<StaffLoginResolution> SignInByLoginAsync(
        Guid? organizationId,
        StaffSignInByLoginRequest request,
        CancellationToken cancellationToken);

    Task<StaffSignInResponse?> SignInByPhoneAsync(
        StaffSignInByPhoneRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of resolving a club from a bare login. Exactly one of the cases holds:
/// <list type="bullet">
/// <item>0 password-verified matches: <see cref="SignedIn"/> null and <see cref="Clubs"/> empty.</item>
/// <item>1 match: <see cref="SignedIn"/> set.</item>
/// <item>2+ matches: <see cref="Clubs"/> populated for a disambiguation picker.</item>
/// </list>
/// </summary>
public sealed record StaffLoginResolution(
    StaffSignInResponse? SignedIn,
    IReadOnlyList<StaffSignInClubChoice> Clubs)
{
    public static readonly StaffLoginResolution None =
        new(null, Array.Empty<StaffSignInClubChoice>());
}
