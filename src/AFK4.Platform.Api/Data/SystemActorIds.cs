namespace AFK4.Platform.Api.Data;

/// <summary>
/// Reserved synthetic actors used where legacy financial schemas require a non-null actor id.
/// These ids are not staff users and must never be materialized as StaffUser rows.
/// </summary>
public static class SystemActorIds
{
    public static readonly Guid PlayerShop = Guid.Parse("00000000-0000-4000-8000-000000000004");

    public const string PlayerShopDisplayName = "Player Shop";

    public static bool TryGetDisplayName(Guid actorId, out string displayName)
    {
        if (actorId == PlayerShop)
        {
            displayName = PlayerShopDisplayName;
            return true;
        }

        displayName = string.Empty;
        return false;
    }
}
