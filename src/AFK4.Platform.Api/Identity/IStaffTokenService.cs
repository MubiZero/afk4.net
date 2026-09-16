using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public interface IStaffTokenService
{
    Task<StaffSignInResponse> IssueAsync(StaffUserEntity user, CancellationToken cancellationToken);

    Task<StaffSignInResponse?> RefreshAsync(StaffRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<StaffContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);

    /// <summary>
    /// Гасит ровно ту пару токенов, с которой пришёл выход: предъявленный refresh и access,
    /// которым подписан запрос. Та же учётная запись, открытая на другой машине, продолжает
    /// работать — выход с одной кассы не выкидывает сотрудника со второй.
    /// </summary>
    Task<bool> RevokeAsync(
        StaffSignOutRequest request,
        string? accessToken,
        CancellationToken cancellationToken);
}
