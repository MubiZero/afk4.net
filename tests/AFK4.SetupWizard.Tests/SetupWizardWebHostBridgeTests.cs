using System.Text.Json;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Media;

namespace AFK4.SetupWizard.Tests;

/// <summary>
/// Мост между окном мастера и платформой: через него проходит вся установка — вход сотрудника,
/// выбор филиала и места, регистрация устройства, запись машинной конфигурации и установка
/// нужного приложения. До этих тестов он не был покрыт вовсе, хотя ошибка здесь означает
/// испорченную установку на живом ПК, а не красный экран у разработчика.
/// </summary>
public sealed class SetupWizardWebHostBridgeTests
{
    private const string Access = "access-token-1";
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SeatId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DeviceId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // --- разбор входящего сообщения -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WithMalformedJson_AnswersNothing()
    {
        var bridge = CreateBridge(out _);

        Assert.Null(await bridge.HandleAsync("{ это не json", CancellationToken.None));
    }

    [Theory]
    // Чужое сообщение в том же окне: отвечать на него нельзя, иначе мост становится открытым
    // каналом для всего, что окажется на странице.
    [InlineData("""{"type":"other:thing","requestId":"r1"}""")]
    [InlineData("""{"type":"wizard:phoneSignIn"}""")]
    [InlineData("""{"requestId":"r1"}""")]
    public async Task HandleAsync_WithForeignOrIncompleteMessage_AnswersNothing(string message)
    {
        var bridge = CreateBridge(out _);

        Assert.Null(await bridge.HandleAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithUnknownWizardRequest_AnswersWithError()
    {
        var bridge = CreateBridge(out _);

        var response = await Send(bridge, "wizard:teleport", "{}");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("wizard_request_failed", response.GetProperty("error").GetProperty("code").GetString());
    }

    // --- токен ----------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverAuth_BeforeSignIn_IsRefused()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, "wizard:discoverAuth", "{}");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("wizard_discover_failed", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(deps.Api.DiscoverCalls);
    }

    // --- свой логотип ---------------------------------------------------------------------

    [Fact]
    public async Task UploadLogo_SendsTheChosenFileAndReturnsItsUrl()
    {
        var picker = new StubLogoPicker("C:\\logo.png");
        var bridge = CreateBridge(out var deps, logoFilePicker: picker);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge, "wizard:uploadLogo", $$"""{"branchId":"{{Guid.NewGuid():D}}"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal("https://cdn.afk4.net/logo.png", response.GetProperty("payload").GetProperty("logoUrl").GetString());
        Assert.Equal(["C:\\logo.png"], deps.Api.UploadedLogoPaths);
    }

    // Закрытое окно выбора — обычный исход, а не отказ: грузить нечего, ошибки нет.
    [Fact]
    public async Task UploadLogo_WhenTheDialogIsDismissed_UploadsNothing()
    {
        var bridge = CreateBridge(out var deps, logoFilePicker: new StubLogoPicker(null));
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge, "wizard:uploadLogo", $$"""{"branchId":"{{Guid.NewGuid():D}}"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        // Адреса в ответе нет вовсе: пустые поля мост не сериализует, и веб обязан читать это как
        // «логотип не выбран», а не как значение.
        var payload = response.GetProperty("payload");
        Assert.True(
            !payload.TryGetProperty("logoUrl", out var logoUrl) || logoUrl.ValueKind == JsonValueKind.Null);
        Assert.Empty(deps.Api.UploadedLogoPaths);
    }

    private sealed class StubLogoPicker(string? path) : ILogoFilePicker
    {
        public string? PickImage() => path;
    }

    // --- зал и тарифы ---------------------------------------------------------------------

    [Fact]
    public async Task CreateSeats_MakesNumberedSeatsInTheChosenZone()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");
        var branchId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        var response = await Send(
            bridge,
            "wizard:createSeats",
            $$"""{"branchId":"{{branchId:D}}","zoneId":"{{zoneId:D}}","namePrefix":" ПК ","count":3}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(3, response.GetProperty("payload").GetProperty("names").GetArrayLength());
        Assert.Equal(3, deps.Api.SeatNames.Count);
        Assert.Equal(["ПК-1", "ПК-2", "ПК-3"], deps.Api.SeatNames);
    }

    // Зал в сотню мест — это импорт, а не установка: такой запрос до сервера доходить не должен.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task CreateSeats_WithCountOutsideTheLimit_IsRefused(int count)
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge,
            "wizard:createSeats",
            $$"""{"branchId":"{{Guid.NewGuid():D}}","zoneId":"{{Guid.NewGuid():D}}","namePrefix":"ПК","count":{{count}}}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(deps.Api.SeatNames);
    }

    [Fact]
    public async Task CreateTariff_PassesNameAndHourlyPrice()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");
        var branchId = Guid.NewGuid();

        var response = await Send(
            bridge,
            "wizard:createTariff",
            $$"""{"branchId":"{{branchId:D}}","name":" Стандартный ","pricePerHourMinorUnits":1250}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        var tariff = Assert.Single(deps.Api.Tariffs);
        Assert.Equal(branchId, tariff.BranchId);
        Assert.Equal("Стандартный", tariff.Name);
        Assert.Equal(1250, tariff.PricePerHour);
    }

    [Fact]
    public async Task CreateTariff_WithoutPrice_IsRefused()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge,
            "wizard:createTariff",
            $$"""{"branchId":"{{Guid.NewGuid():D}}","name":"Стандартный","pricePerHourMinorUnits":0}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(deps.Api.Tariffs);
    }

    // --- сотрудники -----------------------------------------------------------------------

    [Fact]
    public async Task InviteStaff_SendsTheInviteAndReturnsTheCodeToHandOver()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");
        var branchId = Guid.NewGuid();

        var response = await Send(
            bridge,
            "wizard:inviteStaff",
            $$"""{"branchId":"{{branchId:D}}","displayName":" Дилшод ","phoneNumber":" +992900000001 ","roleName":"operator"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal("123456", response.GetProperty("payload").GetProperty("code").GetString());
        var invite = Assert.Single(deps.Api.Invites);
        Assert.Equal(branchId, invite.BranchId);
        Assert.Equal("Дилшод", invite.DisplayName);
        Assert.Equal("+992900000001", invite.Phone);
        Assert.Equal("operator", invite.Role);
    }

    [Fact]
    public async Task InviteStaff_BeforeSignIn_IsRefused()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(
            bridge,
            "wizard:inviteStaff",
            $$"""{"branchId":"{{Guid.NewGuid():D}}","displayName":"Дилшод","phoneNumber":"+992900000001","roleName":"operator"}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(deps.Api.Invites);
    }

    // Без имени или номера приглашать некого — запрос до сервера доходить не должен.
    [Theory]
    [InlineData("", "+992900000001", "operator")]
    [InlineData("Дилшод", "", "operator")]
    [InlineData("Дилшод", "+992900000001", "")]
    public async Task InviteStaff_WithMissingFields_IsRefused(string displayName, string phone, string role)
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge,
            "wizard:inviteStaff",
            $$"""{"branchId":"{{Guid.NewGuid():D}}","displayName":"{{displayName}}","phoneNumber":"{{phone}}","roleName":"{{role}}"}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(deps.Api.Invites);
    }

    // --- оформление -----------------------------------------------------------------------

    [Fact]
    public async Task SaveBranding_SendsChoiceWithTheOrganizationFromSignIn()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(
            bridge,
            "wizard:saveBranding",
            """{"logoUrl":" https://api.afk4.net/branding/presets/bolt.svg ","accentColor":"#C8FF00"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.NotNull(deps.Api.Branding);
        Assert.Equal("https://api.afk4.net/branding/presets/bolt.svg", deps.Api.Branding!.Value.LogoUrl);
        Assert.Equal("#C8FF00", deps.Api.Branding.Value.AccentColor);
    }

    [Fact]
    public async Task SaveBranding_BeforeSignIn_IsRefused()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, "wizard:saveBranding", """{"logoUrl":null,"accentColor":"#C8FF00"}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Null(deps.Api.Branding);
    }

    // Пропуск шага — это тоже выбор: пустые значения снимают оформление, а не молча ничего не делают.
    [Fact]
    public async Task SaveBranding_WithBlankValues_ClearsTheLook()
    {
        var bridge = CreateBridge(out var deps);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        await Send(bridge, "wizard:saveBranding", """{"logoUrl":"  ","accentColor":null}""");

        Assert.NotNull(deps.Api.Branding);
        Assert.Null(deps.Api.Branding!.Value.LogoUrl);
        Assert.Null(deps.Api.Branding.Value.AccentColor);
    }

    [Fact]
    public async Task BrandingPresets_AreServedAsPlatformUrls()
    {
        var bridge = CreateBridge(out _);

        var response = await Send(bridge, "wizard:brandingPresets", "{}");

        Assert.True(response.GetProperty("ok").GetBoolean());
        var presets = response.GetProperty("payload").GetProperty("presets");
        Assert.Equal(10, presets.GetArrayLength());
        Assert.EndsWith(
            "/branding/presets/bolt.svg",
            presets[0].GetProperty("url").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PhoneSignIn_KeepsTokenInsideHost_AndUsesItForNextCall()
    {
        var bridge = CreateBridge(out var deps);

        var signIn = await Send(bridge, "wizard:phoneSignIn", """{"phone":" +992900000000 ","password":"pass"}""");

        Assert.True(signIn.GetProperty("ok").GetBoolean());
        Assert.Equal("Оператор Дилшод", signIn.GetProperty("payload").GetProperty("displayName").GetString());
        Assert.Equal(("+992900000000", "pass"), deps.Api.PhoneSignIn);

        // Токен не уезжает в веб ни при каком запросе: страница мастера его не видит и не хранит.
        Assert.DoesNotContain(Access, signIn.ToString(), StringComparison.Ordinal);

        var discover = await Send(bridge, "wizard:discoverAuth", "{}");

        Assert.True(discover.GetProperty("ok").GetBoolean());
        Assert.Equal([Access], deps.Api.DiscoverCalls);
    }

    [Fact]
    public async Task PhoneSignIn_WithEmptyCredentials_IsRefusedBeforeCallingPlatform()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, "wizard:phoneSignIn", """{"phone":"  ","password":""}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("wizard_phone_sign_in_failed", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Null(deps.Api.PhoneSignIn);
    }

    // --- отказ платформы ------------------------------------------------------------------

    [Fact]
    public async Task WhenPlatformRefusesWithCode_ErrorKeepsCodeAndRemainingAttempts()
    {
        var bridge = CreateBridge(out var deps);
        deps.Api.ResetByPhoneThrows = new SetupWizardApiException("invalid_code", "Код не подошёл.", 2);

        var response = await Send(
            bridge,
            "wizard:resetByPhone",
            """{"phoneNumber":"+992900000000","code":"0000","newPassword":"secret12"}""");

        var error = response.GetProperty("error");
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_code", error.GetProperty("code").GetString());
        Assert.Equal(2, error.GetProperty("remainingAttempts").GetInt32());
    }

    // --- регистрация устройства ------------------------------------------------------------

    [Fact]
    public async Task Enroll_ForGamingPcWithoutSeat_IsRefused()
    {
        var bridge = await SignedIn();

        var response = await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","role":"gaming_pc","displayName":"PC-07"}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("wizard_enroll_failed", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Null(bridge.Deps.Bootstrap.Written);
        Assert.False(bridge.Deps.Completion.Completed);
    }

    // Рабочее место управляющего заводится без места на карте зала: сажать кассира на игровой
    // ПК не нужно, а требование места однажды уже сломало установку в клубе.
    [Fact]
    public async Task Enroll_ForManagerWorkstation_NeedsNoSeat()
    {
        var bridge = await SignedIn();

        var response = await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","role":"manager_workstation","displayName":"Стойка"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Null(bridge.Deps.Api.EnrollRequest!.SeatId);
    }

    [Fact]
    public async Task Enroll_WritesMachineConfigurationFromPlatformAnswer()
    {
        var bridge = await SignedIn();

        await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","seatId":"{{SeatId}}","role":"gaming_pc","displayName":"PC-07"}""");

        var written = bridge.Deps.Bootstrap.Written;
        Assert.NotNull(written);
        Assert.Equal(OrganizationId, written!.OrganizationId);
        Assert.Equal(DeviceId, written.DeviceId);
        Assert.Equal("gaming_pc", written.Role);
        Assert.Equal("https://afk4.example", written.ApiBaseUrl);
        Assert.Equal("internal", written.UpdateChannel);
        Assert.Equal("lease-key", written.LeaseSigningPublicKeyPem);
        Assert.Equal("update-key", written.UpdatePackageSigningPublicKeyPem);
        Assert.Equal("secret-1", written.CredentialSecret);
    }

    [Fact]
    public async Task Enroll_WithoutDisplayName_UsesMachineName()
    {
        var bridge = await SignedIn();

        await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","seatId":"{{SeatId}}","role":"gaming_pc"}""");

        Assert.Equal("CLUB-PC-01", bridge.Deps.Api.EnrollRequest!.DisplayName);
    }

    // --- установка приложения и запуск службы ----------------------------------------------

    [Fact]
    public async Task Enroll_ForGamingPc_InstallsPlayerShellAndStartsAgent()
    {
        var bridge = await SignedIn();

        var response = await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","seatId":"{{SeatId}}","role":"gaming_pc"}""");

        Assert.Equal("installed", response.GetProperty("payload").GetProperty("shell").GetProperty("status").GetString());
        Assert.Equal(1, bridge.Deps.Shell.Calls);
        Assert.Equal(0, bridge.Deps.Operator.Calls);
        Assert.True(bridge.Deps.Completion.Completed);
        // Оболочку игрока запускает служба агента на экране блокировки, а не мастер.
        Assert.False(bridge.Deps.Launcher.Launched);
    }

    [Fact]
    public async Task Enroll_ForManagerWorkstation_InstallsOrganizationAdminAndOpensIt()
    {
        var bridge = await SignedIn();

        await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","role":"manager_workstation"}""");

        Assert.Equal(1, bridge.Deps.Operator.Calls);
        Assert.Equal(0, bridge.Deps.Shell.Calls);
        Assert.True(bridge.Deps.Completion.Completed);
        // У стойки нет агента, который откроет админку на экране блокировки, — открывает мастер.
        Assert.True(bridge.Deps.Launcher.Launched);
    }

    /// <summary>
    /// Самое дорогое место всего мастера: установка приложения провалилась — служба агента
    /// стартовать не должна. Иначе ПК числится готовым, показывает экран блокировки и не может
    /// открыть оболочку, а человек за стойкой видит устройство «в порядке».
    /// </summary>
    [Fact]
    public async Task Enroll_WhenAppInstallFails_DoesNotStartAgent()
    {
        var bridge = await SignedIn();
        bridge.Deps.Shell.Result = ShellProvisionResult.Failed(1603, "msiexec 1603");

        var response = await Send(
            bridge.Bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","seatId":"{{SeatId}}","role":"gaming_pc"}""");

        var shell = response.GetProperty("payload").GetProperty("shell");
        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal("failed", shell.GetProperty("status").GetString());
        Assert.Equal(1603, shell.GetProperty("exitCode").GetInt32());
        Assert.False(bridge.Deps.Completion.Completed);
        Assert.False(bridge.Deps.Launcher.Launched);
        // Машинная конфигурация уже записана: устройство зарегистрировано, повтор установки
        // не должен гонять человека через весь мастер заново.
        Assert.NotNull(bridge.Deps.Bootstrap.Written);
    }

    [Fact]
    public async Task ProvisionShell_RetriesInstallForTheRoleItIsGiven()
    {
        var bridge = CreateBridge(out var deps);

        await Send(bridge, "wizard:provisionShell", """{"role":"manager_workstation"}""");

        Assert.Equal(1, deps.Operator.Calls);
        Assert.Equal(0, deps.Shell.Calls);
    }

    // Экран завершения старых сборок повторяет установку, не передавая роль. Считать такой
    // повтор игровым ПК — то поведение, на которое эти сборки и рассчитывают.
    [Fact]
    public async Task ProvisionShell_WithoutRole_RepeatsGamingPcInstall()
    {
        var bridge = CreateBridge(out var deps);

        await Send(bridge, "wizard:provisionShell", "{}");

        Assert.Equal(1, deps.Shell.Calls);
        Assert.Equal(0, deps.Operator.Calls);
    }

    [Fact]
    public async Task ProvisionShell_WhenAppIsAlreadyThere_StartsAgentAnyway()
    {
        var bridge = CreateBridge(out var deps);
        deps.Shell.Result = ShellProvisionResult.AlreadyPresent(1638);

        var response = await Send(bridge, "wizard:provisionShell", """{"role":"gaming_pc"}""");

        Assert.Equal("already_present", response.GetProperty("payload").GetProperty("status").GetString());
        Assert.True(deps.Completion.Completed);
    }

    // --- места и филиалы -------------------------------------------------------------------

    [Fact]
    public async Task CreateSeat_WithEmptyName_IsRefusedBeforeCallingPlatform()
    {
        var bridge = await SignedIn();

        var response = await Send(
            bridge.Bridge,
            "wizard:createSeatAuth",
            $$"""{"branchId":"{{BranchId}}","zoneId":"{{Guid.Empty}}","name":"   "}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("wizard_create_seat_failed", response.GetProperty("error").GetProperty("code").GetString());
        Assert.False(bridge.Deps.Api.SeatCreated);
    }

    [Fact]
    public async Task Discover_SortsBranchesByNameSoTheListDoesNotJumpAround()
    {
        var bridge = await SignedIn();
        bridge.Deps.Api.Branches =
        [
            Branch("Ягона"),
            Branch("Алмаз"),
            Branch("бухара")
        ];

        var response = await Send(bridge.Bridge, "wizard:discoverAuth", "{}");

        var names = response.GetProperty("payload").GetProperty("branches")
            .EnumerateArray()
            .Select(branch => branch.GetProperty("branchName").GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(["Алмаз", "бухара", "Ягона"], names);
    }

    // --- обвязка ---------------------------------------------------------------------------

    private static InstallBranchDto Branch(string name) => new(
        Guid.NewGuid(),
        name.ToLowerInvariant(),
        name,
        new FloorMapDto(BranchId, name, []),
        []);

    // --- забытый пароль ---------------------------------------------------------------------

    // Человек ставит клуб на новой машине и не помнит пароль. Из четырёх команд восстановления
    // проверялась одна: остальные три могли молча ничего не отправлять, и мастер показывал бы
    // «код отправлен» там, где на телефон и почту не ушло ничего.
    [Fact]
    public async Task ForgotByEmail_SendsTheLoginToThePlatform()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, "wizard:forgotByEmail", """{"userNameOrEmail":"  owner@club.tj "}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(["owner@club.tj"], deps.Api.ForgotByEmail);
    }

    [Fact]
    public async Task ResetByEmail_SendsCodeAndNewPassword()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(
            bridge,
            "wizard:resetByEmail",
            """{"userNameOrEmail":"owner@club.tj","code":" 123456 ","newPassword":"Passw0rd!"}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal([("owner@club.tj", "123456", "Passw0rd!")], deps.Api.ResetByEmail);
    }

    [Fact]
    public async Task ForgotByPhone_SendsTheNumberToThePlatform()
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, "wizard:forgotByPhone", """{"phoneNumber":" +992 90 000 00 00 "}""");

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(["+992 90 000 00 00"], deps.Api.ForgotByPhone);
    }

    // Пустое поле до сервера доходить не должно: отказ платформы на пустой строке читается как
    // «не тот логин», хотя человек просто ничего не ввёл.
    [Theory]
    [InlineData("wizard:forgotByEmail", """{"userNameOrEmail":"   "}""")]
    [InlineData("wizard:resetByEmail", """{"userNameOrEmail":"owner@club.tj","code":"","newPassword":"Passw0rd!"}""")]
    [InlineData("wizard:forgotByPhone", """{"phoneNumber":""}""")]
    public async Task PasswordRecovery_WithAnEmptyField_IsRefusedBeforeTheNetwork(string type, string payload)
    {
        var bridge = CreateBridge(out var deps);

        var response = await Send(bridge, type, payload);

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(deps.Api.ForgotByEmail);
        Assert.Empty(deps.Api.ResetByEmail);
        Assert.Empty(deps.Api.ForgotByPhone);
    }

    // Хост без диалога выбора файла (превью, будущий безоконный режим) — это отказ с кодом, а не
    // падение моста.
    [Fact]
    public async Task UploadLogo_WithoutAFilePicker_AnswersWithAnError()
    {
        var bridge = CreateBridge(out var deps, logoFilePicker: null);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");

        var response = await Send(bridge, "wizard:uploadLogo", $$"""{"branchId":"{{Guid.NewGuid():D}}"}""");

        Assert.False(response.GetProperty("ok").GetBoolean());
        // Код отказа проверяется там, где он и живёт (ErrorCodeFor); здесь важно, что мост ответил
        // ошибкой и ничего не отправил на сервер.
        Assert.NotNull(response.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(deps.Api.UploadedLogoPaths);
    }

    // Установка идёт минутами: msiexec на чистой машине не быстрый. Пока она шла в потоке окна,
    // мастер не реагировал ни на перетаскивание, ни на сворачивание — со стороны это неотличимо
    // от зависшей программы. Проверка простая: вызывающий поток получает управление обратно, не
    // дожидаясь установки. Без ухода в фон этот тест просто повис бы на самом вызове.
    [Fact]
    public async Task ProvisionShell_DoesNotHoldTheCallingThreadWhileTheInstallerRuns()
    {
        using var installerRunning = new ManualResetEventSlim(false);
        using var releaseInstaller = new ManualResetEventSlim(false);
        var dependencies = new Dependencies();
        dependencies.Shell.Blocker = () =>
        {
            installerRunning.Set();
            releaseInstaller.Wait(TimeSpan.FromSeconds(10));
        };
        var bridge = dependencies.Build();

        var pending = Send(bridge, "wizard:provisionShell", """{"role":"GamingPc"}""");

        Assert.True(installerRunning.Wait(TimeSpan.FromSeconds(10)));
        Assert.False(pending.IsCompleted);

        releaseInstaller.Set();
        var response = await pending;

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(1, dependencies.Shell.Calls);
    }

    // Тот же поток окна держала и запись машинной конфигурации — прямо перед самой долгой частью.
    [Fact]
    public async Task Enroll_DoesNotHoldTheCallingThreadWhileMachineConfigurationIsWritten()
    {
        using var writing = new ManualResetEventSlim(false);
        using var releaseWrite = new ManualResetEventSlim(false);
        var (bridge, dependencies) = await SignedIn();
        dependencies.Bootstrap.Blocker = () =>
        {
            writing.Set();
            releaseWrite.Wait(TimeSpan.FromSeconds(10));
        };

        var pending = Send(
            bridge,
            "wizard:enrollAuth",
            $$"""{"branchId":"{{BranchId}}","seatId":"{{SeatId}}","role":"gaming_pc","displayName":"PC-07"}""");

        Assert.True(writing.Wait(TimeSpan.FromSeconds(10)));
        Assert.False(pending.IsCompleted);

        releaseWrite.Set();
        var response = await pending;

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.NotNull(dependencies.Bootstrap.Written);
    }

    private static async Task<JsonElement> Send(SetupWizardWebHostBridge bridge, string type, string payloadJson)
    {
        var message = $$"""{"type":"{{type}}","requestId":"r-1","payload":{{payloadJson}}}""";
        var response = await bridge.HandleAsync(message, CancellationToken.None);
        Assert.NotNull(response);
        return JsonDocument.Parse(response!).RootElement;
    }

    private static SetupWizardWebHostBridge CreateBridge(
        out Dependencies dependencies,
        ILogoFilePicker? logoFilePicker = null)
    {
        dependencies = new Dependencies();
        return dependencies.Build(logoFilePicker);
    }

    private static async Task<(SetupWizardWebHostBridge Bridge, Dependencies Deps)> SignedIn()
    {
        var bridge = CreateBridge(out var dependencies);
        await Send(bridge, "wizard:phoneSignIn", """{"phone":"+992900000000","password":"pass"}""");
        return (bridge, dependencies);
    }

    private sealed class Dependencies
    {
        public FakeApiClient Api { get; } = new();
        public FakeKeyStore Keys { get; } = new();
        public FakeBootstrapWriter Bootstrap { get; } = new();
        public FakeCompletionAction Completion { get; } = new();
        public FakeProvisioner Shell { get; } = new();
        public FakeProvisioner Operator { get; } = new();
        public FakeLauncher Launcher { get; } = new();

        public SetupWizardWebHostBridge Build(ILogoFilePicker? logoFilePicker = null) => new(
            Api,
            Keys,
            Bootstrap,
            new SetupWizardMachineInfo("CLUB-PC-01"),
            Completion,
            Shell,
            Operator,
            Launcher,
            logoFilePicker);
    }

    private sealed class FakeApiClient : ISetupWizardApiClient
    {
        public (string Phone, string Password)? PhoneSignIn { get; private set; }
        public List<string> DiscoverCalls { get; } = [];
        public AuthenticatedInstallEnrollRequest? EnrollRequest { get; private set; }
        public bool SeatCreated { get; private set; }
        public SetupWizardApiException? ResetByPhoneThrows { get; set; }
        public IReadOnlyList<InstallBranchDto> Branches { get; set; } = [];

        public Task<StaffSignInResponse> SignInByPhoneAsync(string phoneNumber, string password, CancellationToken cancellationToken)
        {
            PhoneSignIn = (phoneNumber, password);
            return Task.FromResult(SignInResponse());
        }

        public Task<SetupWizardLoginResult> SignInByLoginAsync(string login, string password, CancellationToken cancellationToken) =>
            Task.FromResult(new SetupWizardLoginResult(SignInResponse(), []));

        public Task<StaffSignInResponse> SignInToClubAsync(Guid organizationId, string login, string password, CancellationToken cancellationToken) =>
            Task.FromResult(SignInResponse());

        public List<string> ForgotByEmail { get; } = [];

        public List<(string Identity, string Code, string NewPassword)> ResetByEmail { get; } = [];

        public List<string> ForgotByPhone { get; } = [];

        public List<(string Phone, string Code, string NewPassword)> ResetByPhone { get; } = [];

        public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        {
            ForgotByEmail.Add(userNameOrEmail);
            return Task.CompletedTask;
        }

        public Task ResetPasswordByEmailAsync(string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken)
        {
            ResetByEmail.Add((userNameOrEmail, code, newPassword));
            return Task.CompletedTask;
        }

        public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        {
            ForgotByPhone.Add(phoneNumber);
            return Task.CompletedTask;
        }

        public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken)
        {
            ResetByPhone.Add((phoneNumber, code, newPassword));
            return ResetByPhoneThrows is null ? Task.CompletedTask : throw ResetByPhoneThrows;
        }

        // Организация, с которой мост пошёл в установочные запросы: тесты проверяют, что она
        // берётся из ответа входа, а не теряется по дороге.
        public List<Guid> DiscoverOrganizationIds { get; } = [];

        public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
            Guid organizationId, string accessToken, CancellationToken cancellationToken)
        {
            DiscoverCalls.Add(accessToken);
            DiscoverOrganizationIds.Add(organizationId);
            return Task.FromResult(new InstallDiscoverResponse("Владелец", Branches));
        }

        public List<string> SeatNames { get; } = [];

        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            Guid organizationId, string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
        {
            SeatCreated = true;
            SeatNames.Add(name);
            return Task.FromResult(new InstallCreateSeatResponse(OrganizationId, branchId, zoneId, SeatId, name, 1));
        }

        // Что мастер отправил как оформление клуба.
        public (Guid OrganizationId, string? LogoUrl, string? AccentColor)? Branding { get; private set; }

        public Task UpdateBrandingAsync(
            Guid organizationId, string accessToken, string? logoUrl, string? accentColor, CancellationToken cancellationToken)
        {
            Branding = (organizationId, logoUrl, accentColor);
            return Task.CompletedTask;
        }

        // Приглашения, отправленные мастером.
        public List<(Guid OrganizationId, Guid BranchId, string DisplayName, string Phone, string Role)> Invites { get; } = [];

        public Task<StaffInviteDto> InviteStaffAsync(
            Guid organizationId,
            Guid branchId,
            string accessToken,
            string displayName,
            string phoneNumber,
            string roleName,
            CancellationToken cancellationToken)
        {
            Invites.Add((organizationId, branchId, displayName, phoneNumber, roleName));
            return Task.FromResult(new StaffInviteDto(Guid.NewGuid(), "123456", DateTimeOffset.UnixEpoch.AddYears(56)));
        }

        public List<string> UploadedLogoPaths { get; } = [];

        public Task<UploadedMediaDto> UploadOrganizationLogoAsync(
            Guid organizationId,
            Guid branchId,
            string accessToken,
            string filePath,
            CancellationToken cancellationToken)
        {
            UploadedLogoPaths.Add(filePath);
            return Task.FromResult(new UploadedMediaDto(Guid.NewGuid(), "https://cdn.afk4.net/logo.png", "image/png", 10));
        }

        public List<(Guid BranchId, string Name, long PricePerHour)> Tariffs { get; } = [];

        public Task<TariffDto> CreateTariffAsync(
            Guid organizationId,
            Guid branchId,
            string accessToken,
            string name,
            long pricePerHourMinorUnits,
            CancellationToken cancellationToken)
        {
            Tariffs.Add((branchId, name, pricePerHourMinorUnits));
            return Task.FromResult(new TariffDto(Guid.NewGuid(), organizationId, branchId, name, true, DateTimeOffset.UnixEpoch));
        }

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            Guid organizationId, string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
        {
            EnrollRequest = request;
            return Task.FromResult(new InstallEnrollResponse(
                OrganizationId,
                request.BranchId,
                DeviceId,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "secret-1",
                "approved",
                "https://afk4.example",
                "internal",
                DateTimeOffset.UnixEpoch)
            {
                LeaseSigningPublicKeyPem = "lease-key",
                UpdatePackageSigningPublicKeyPem = "update-key"
            });
        }

        private static StaffSignInResponse SignInResponse() => new(
            Guid.NewGuid(),
            OrganizationId,
            "Оператор Дилшод",
            Access,
            DateTimeOffset.UnixEpoch.AddHours(1),
            "refresh-1",
            DateTimeOffset.UnixEpoch.AddDays(30),
            [BranchId],
            []);
    }

    private sealed class FakeKeyStore : IDeviceKeyStore
    {
        public Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken) =>
            Task.FromResult("device-public-key");
    }

    private sealed class FakeBootstrapWriter : ISetupWizardBootstrapWriter
    {
        public SetupWizardBootstrapConfig? Written { get; private set; }

        /// Изображает медленную запись: файлы под %ProgramData%, icacls и рассылку
        /// WM_SETTINGCHANGE тест отпускает сам, проверив, что поток вызова свободен.
        public Action? Blocker { get; set; }

        public void Write(SetupWizardBootstrapConfig config)
        {
            Blocker?.Invoke();
            Written = config;
        }
    }

    private sealed class FakeCompletionAction : ISetupWizardCompletionAction
    {
        public bool Completed { get; private set; }

        public void Complete() => Completed = true;
    }

    private sealed class FakeProvisioner : ISetupWizardShellProvisioner
    {
        public int Calls { get; private set; }
        public ShellProvisionResult Result { get; set; } = ShellProvisionResult.Installed(0);

        /// Изображает долгий msiexec: тест отпускает установку сам, когда проверит, что поток
        /// вызова свободен.
        public Action? Blocker { get; set; }

        public ShellProvisionResult Provision()
        {
            Calls++;
            Blocker?.Invoke();
            return Result;
        }
    }

    private sealed class FakeLauncher : ISetupWizardOperatorLauncher
    {
        public bool Launched { get; private set; }

        public void Launch() => Launched = true;
    }
}
