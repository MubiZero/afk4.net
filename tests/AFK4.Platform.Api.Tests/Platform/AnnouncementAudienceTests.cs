using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Announcements;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class AnnouncementAudienceTests
{
    private static readonly Guid Club = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherClub = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void All_TakesEveryClub()
    {
        Assert.True(Match(AnnouncementAudienceKinds.All, "[]", "[]", Club, "starter"));
        Assert.True(Match(AnnouncementAudienceKinds.All, "[]", "[]", OtherClub, null));
    }

    [Fact]
    public void Plans_TakesOnlyTheMatchingPlan()
    {
        Assert.True(Match(AnnouncementAudienceKinds.Plans, "[\"starter\"]", "[]", Club, "starter"));
        Assert.False(Match(AnnouncementAudienceKinds.Plans, "[\"starter\"]", "[]", Club, "pro"));
    }

    [Fact]
    public void Plans_DoesNotTakeAClubWithoutASubscription()
    {
        // «Нет тарифа» — это не «подходит всем»: сообщение «вы на старом тарифе» такому клубу
        // адресовано быть не может.
        Assert.False(Match(AnnouncementAudienceKinds.Plans, "[\"starter\"]", "[]", Club, null));
    }

    [Fact]
    public void Organizations_TakesOnlyTheListedClubs()
    {
        var listed = $"[\"{Club}\"]";
        Assert.True(Match(AnnouncementAudienceKinds.Organizations, "[]", listed, Club, "starter"));
        Assert.False(Match(AnnouncementAudienceKinds.Organizations, "[]", listed, OtherClub, "starter"));
    }

    [Fact]
    public void AnEmptyListTakesNobody()
    {
        // Пустой список — это «никому», а не «всем»: обратное разослало бы сообщение всему парку.
        Assert.False(Match(AnnouncementAudienceKinds.Plans, "[]", "[]", Club, "starter"));
        Assert.False(Match(AnnouncementAudienceKinds.Organizations, "[]", "[]", Club, "starter"));
    }

    [Fact]
    public void BrokenJsonTakesNobody()
    {
        Assert.False(Match(AnnouncementAudienceKinds.Plans, "not json", "[]", Club, "starter"));
        Assert.False(Match(AnnouncementAudienceKinds.Organizations, "[]", "{oops", Club, "starter"));
    }

    [Fact]
    public void AnUnknownAudienceKindTakesNobody()
    {
        Assert.False(Match("everyone_ever", "[]", "[]", Club, "starter"));
    }

    [Fact]
    public void PlanCodesAreComparedWithoutCase()
    {
        Assert.True(Match(AnnouncementAudienceKinds.Plans, "[\"Starter\"]", "[]", Club, "starter"));
    }

    private static bool Match(string kind, string plansJson, string organizationsJson, Guid organizationId, string? planCode) =>
        AnnouncementAudience.Matches(
            new PlatformAnnouncementEntity
            {
                AudienceKind = kind,
                AudiencePlanCodesJson = plansJson,
                AudienceOrganizationIdsJson = organizationsJson
            },
            organizationId,
            planCode);
}
