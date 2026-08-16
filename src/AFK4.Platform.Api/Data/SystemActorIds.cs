namespace AFK4.Platform.Api.Data;

/// <summary>
/// Reserved synthetic actors used where legacy financial schemas require a non-null actor id.
/// These ids are not staff users and must never be materialized as StaffUser rows.
/// </summary>
public static class SystemActorIds
{
    public static readonly Guid PlayerShop = Guid.Parse("00000000-0000-4000-8000-000000000004");

    /// <summary>
    /// The player acting on their own account from the mobile app, with no staff member involved —
    /// buying a package at 3am is a legitimate operation and the cash journal must name who did it
    /// rather than print a truncated empty guid.
    /// </summary>
    public static readonly Guid PlayerSelfService = Guid.Parse("00000000-0000-4000-8000-000000000005");

    public const string PlayerShopDisplayName = "Player Shop";

    public const string PlayerSelfServiceDisplayName = "Player Self-Service";

    public static bool TryGetDisplayName(Guid actorId, out string displayName)
    {
        if (actorId == PlayerShop)
        {
            displayName = PlayerShopDisplayName;
            return true;
        }

        if (actorId == PlayerSelfService)
        {
            displayName = PlayerSelfServiceDisplayName;
            return true;
        }

        displayName = string.Empty;
        return false;
    }
}
