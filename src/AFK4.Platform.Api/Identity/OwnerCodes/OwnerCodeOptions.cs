namespace AFK4.Platform.Api.Identity.OwnerCodes;

public sealed class OwnerCodeOptions
{
    public const string SectionName = "OwnerCodes";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(90);
}
