using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Отказы, в которые упирается мастер установки, должны приезжать машинным именем.
///
/// Мастер работает на трёх языках, а текст отказа сервер пишет по-английски: показать его
/// человеку у ПК нельзя. Без кода все причины сливались в одно «не удалось», и человек правил
/// цену там, где занято имя, или перезванивал сотруднику там, где тот уже работает в клубе.
/// </summary>
public sealed class WizardSetupFailureCodeTests
{
    private static readonly Guid OtherStaffUserId = Guid.Parse("7c2f1c2d-9a44-4a54-9a2d-2a4b1f0c1101");

    [Fact]
    public async Task CreateTariff_WithATakenName_AnswersWithTheNameTakenCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        await CreateTariffAsync(client, "Стандартный");
        var duplicate = await CreateTariffAsync(client, "  стандартный ");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(TariffErrorCodeNames.NameTaken, await ReadCodeAsync(duplicate));
    }

    [Fact]
    public async Task InviteStaff_WithAPhoneThatIsNotAPhone_AnswersWithTheInvalidPhoneCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await InviteAsync(client, userName: "dilshod", phoneNumber: "не телефон");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StaffInviteErrorCodeNames.InvalidPhone, await ReadCodeAsync(response));
    }

    // Человек уже работает в клубе — приглашать его незачем, и «проверьте номер» здесь ложный след.
    [Fact]
    public async Task InviteStaff_WithAPhoneThatAlreadyWorksHere_AnswersWithThePhoneTakenCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedVerifiedStaffPhoneAsync(factory, "+992900000001");

        var response = await InviteAsync(client, userName: "dilshod", phoneNumber: "+992 90 000 00 01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StaffInviteErrorCodeNames.PhoneTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task InviteStaff_WithATakenLogin_AnswersWithTheUserNameTakenCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await InviteAsync(client, userName: "tech@afk4.test", phoneNumber: "+992900000002");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StaffInviteErrorCodeNames.UserNameTaken, await ReadCodeAsync(response));
    }

    // Повторная установка на то же место — обычное дело: мастер должен предложить выбрать другое,
    // а не сказать «не удалось подключить этот ПК».
    [Fact]
    public async Task Enroll_OnASeatThatAlreadyHasAPc_AnswersWithTheSeatOccupiedCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var first = await EnrollAsync(client, "WIN-INSTALL-01");
        var second = await EnrollAsync(client, "WIN-INSTALL-02");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(InstallErrorCodeNames.SeatOccupied, await ReadCodeAsync(second));
    }

    private static Task<HttpResponseMessage> CreateTariffAsync(HttpClient client, string name) =>
        client.PostAsJsonAsync(
            TariffRoutes.Tariffs(TestIds.OrganizationId, TestIds.BranchId),
            new CreateTariffRequest(TestIds.OrganizationId, name, Guid.NewGuid().ToString("N")));

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, string userName, string phoneNumber) =>
        client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(
                TestIds.OrganizationId,
                userName,
                "Дилшод",
                phoneNumber,
                null,
                [OrganizationRoleNames.Operator]));

    private static Task<HttpResponseMessage> EnrollAsync(HttpClient client, string machineName) =>
        client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "Стенд 12",
                machineName,
                // Ключ у каждой машины свой: по нему платформа опознаёт ту же машину при
                // повторной регистрации, так что одинаковый ключ означал бы один и тот же ПК.
                $"-----BEGIN PUBLIC KEY-----\n{machineName}\n-----END PUBLIC KEY-----"));

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
    }

    private static async Task SeedVerifiedStaffPhoneAsync(PlatformApiFactory factory, string phoneNumber)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = OtherStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "already@afk4.test",
            NormalizedUserName = "ALREADY@AFK4.TEST",
            DisplayName = "Уже работает",
            Phone = phoneNumber,
            // Тем же способом, что и сервер: иначе «уже работает» не нашлось бы по номеру.
            NormalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber),
            PhoneVerifiedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            PasswordHash = "x"
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await dbContext.Branches.SingleAsync(row => row.BranchId == TestIds.BranchId);
        branch.RequireManualDeviceApproval = false;

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = TestIds.ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main hall",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = TestIds.SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = TestIds.ZoneId,
            Name = "PC-101",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }
}
