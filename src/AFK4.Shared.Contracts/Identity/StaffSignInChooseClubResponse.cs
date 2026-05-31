namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInChooseClubResponse(
    IReadOnlyList<StaffSignInClubChoice> Clubs);
