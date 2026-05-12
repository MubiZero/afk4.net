using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public interface IStaffTokenService
{
    Task<StaffSignInResponse> IssueAsync(StaffUserEntity user, CancellationToken cancellationToken);

    Task<StaffSignInResponse?> RefreshAsync(StaffRefreshTokenRequest request, CancellationToken cancellationToken);

    Task<StaffContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
}
