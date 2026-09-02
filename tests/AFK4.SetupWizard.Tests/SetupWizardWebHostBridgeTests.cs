using System.Text.Json;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

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

    private static async Task<JsonElement> Send(SetupWizardWebHostBridge bridge, string type, string payloadJson)
    {
        var message = $$"""{"type":"{{type}}","requestId":"r-1","payload":{{payloadJson}}}""";
        var response = await bridge.HandleAsync(message, CancellationToken.None);
        Assert.NotNull(response);
        return JsonDocument.Parse(response!).RootElement;
    }

    private static SetupWizardWebHostBridge CreateBridge(out Dependencies dependencies)
    {
        dependencies = new Dependencies();
        return dependencies.Build();
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

        public SetupWizardWebHostBridge Build() => new(
            Api,
            Keys,
            Bootstrap,
            new SetupWizardMachineInfo("CLUB-PC-01"),
            Completion,
            Shell,
            Operator,
            Launcher);
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

        public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetPasswordByEmailAsync(string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken) =>
            ResetByPhoneThrows is null ? Task.CompletedTask : throw ResetByPhoneThrows;

        public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(string accessToken, CancellationToken cancellationToken)
        {
            DiscoverCalls.Add(accessToken);
            return Task.FromResult(new InstallDiscoverResponse("Владелец", Branches));
        }

        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
        {
            SeatCreated = true;
            return Task.FromResult(new InstallCreateSeatResponse(OrganizationId, branchId, zoneId, SeatId, name, 1));
        }

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
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

        public void Write(SetupWizardBootstrapConfig config) => Written = config;
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

        public ShellProvisionResult Provision()
        {
            Calls++;
            return Result;
        }
    }

    private sealed class FakeLauncher : ISetupWizardOperatorLauncher
    {
        public bool Launched { get; private set; }

        public void Launch() => Launched = true;
    }
}
