using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests.Protection;

/// <summary>Профиль защиты на ПК: применение, снятие на обслуживание, синхронизация по версии, честный отчёт.</summary>
public sealed class ProtectionEnforcerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    private static ProtectionProfileDto Profile(int version, bool usb = true, IReadOnlyList<string>? drives = null) =>
        new(version, usb, false, false, false, drives ?? ["D"], [], [], SessionTraceNames.All);

    [Fact]
    public async Task Applying_WritesEnabledRules_RemovesDisabledOnes_AndReportsEachHonestly()
    {
        var fixture = new Fixture(stored: Profile(2));

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Contains(("Deny_All", 1), fixture.Registry.Values);
        Assert.Contains("DownloadRestrictions", fixture.Registry.Removed);
        var report = fixture.Platform.Reports.Single();
        Assert.Equal(2, report.Version);
        Assert.Equal(ProtectionItemStatusNames.Applied, Status(report, ProtectionItemNames.RemovableStorage));
        Assert.Equal(ProtectionItemStatusNames.ExplorerOnly, Status(report, ProtectionItemNames.HiddenDrives));
        Assert.Equal(ProtectionItemStatusNames.Applied, Status(report, ProtectionItemNames.KioskBaseline));
        // Выключенное в отчёт не попадает: клуб видит то, что включал.
        Assert.DoesNotContain(report.Items, item => item.Item == ProtectionItemNames.BrowserDownloads);
    }

    [Fact]
    public async Task AFailedRule_IsNamedAsFailed_WhileTheRestStillApply()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Registry.FailOn = "Deny_All";

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        var report = fixture.Platform.Reports.Single();
        Assert.Equal(ProtectionItemStatusNames.Failed, Status(report, ProtectionItemNames.RemovableStorage));
        Assert.Equal(ProtectionItemStatusNames.ExplorerOnly, Status(report, ProtectionItemNames.HiddenDrives));
    }

    [Fact]
    public async Task WhereMachinePoliciesCannotBeWritten_TheReportSaysSo()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Registry.Supported = false;

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.All(fixture.Platform.Reports.Single().Items, item => Assert.Equal(ProtectionItemStatusNames.Unsupported, item.Status));
        Assert.Empty(fixture.Registry.Values);
    }

    /// <summary>Спека, §6.5: в обслуживании технику нужна обычная Windows — всё снимается.</summary>
    [Fact]
    public async Task Releasing_RemovesEverything_AndReportsItReleased()
    {
        var fixture = new Fixture(stored: Profile(2));
        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        await fixture.Enforcer.ReleaseAsync(CancellationToken.None);

        Assert.Contains("Deny_All", fixture.Registry.Removed);
        Assert.Contains("NoLogoff", fixture.Registry.Removed);
        Assert.All(fixture.Platform.Reports.Last().Items, item => Assert.Equal(ProtectionItemStatusNames.Released, item.Status));
    }

    [Fact]
    public async Task ANewVersionInTheHeartbeat_FetchesAndAppliesTheProfile()
    {
        var fixture = new Fixture(stored: Profile(1, usb: false));
        fixture.Platform.Profile = Profile(2, usb: true);

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Equal(1, fixture.Platform.Fetches);
        Assert.Contains(("Deny_All", 1), fixture.Registry.Values);
        Assert.Equal(2, fixture.Store.Saved?.Version);
    }

    [Fact]
    public async Task TheSameVersion_IsNotFetchedAgain()
    {
        var fixture = new Fixture(stored: Profile(2));
        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Equal(0, fixture.Platform.Fetches);
        Assert.Single(fixture.Platform.Reports);
    }

    [Fact]
    public async Task UnderMaintenance_ANewProfileIsKept_ButNotApplied()
    {
        var fixture = new Fixture(stored: Profile(1, usb: false));
        fixture.Runtime.Save(AgentRuntimeState.Maintenance(Now));
        fixture.Platform.Profile = Profile(2, usb: true);

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Equal(2, fixture.Store.Saved?.Version);
        Assert.DoesNotContain(("Deny_All", 1), fixture.Registry.Values);
    }

    /// <summary>Сервер молчит — ПК остаётся на прежнем профиле и не долбит сервер каждые десять секунд.</summary>
    [Fact]
    public async Task WhenTheServerCannotGiveTheProfile_ThePcKeepsTheOldOne_AndWaitsBeforeAskingAgain()
    {
        var fixture = new Fixture(stored: Profile(1));
        fixture.Platform.FailFetch = true;

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);
        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Equal(1, fixture.Platform.Fetches);
        Assert.Null(fixture.Store.Saved);

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);
        Assert.Equal(2, fixture.Platform.Fetches);
    }

    [Fact]
    public async Task AReportThatDidNotArrive_IsSentWithTheNextHeartbeat()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Platform.FailReport = true;
        await fixture.Enforcer.ApplyAsync(CancellationToken.None);
        Assert.Empty(fixture.Platform.Reports);

        fixture.Platform.FailReport = false;
        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Single(fixture.Platform.Reports);
    }

    [Fact]
    public async Task TheRefreshCommand_FetchesNow_AndSaysWhetherItWorked()
    {
        var fixture = new Fixture(stored: Profile(1));
        fixture.Platform.Profile = Profile(3);

        var applied = await fixture.Enforcer.RefreshAsync(CancellationToken.None);
        Assert.Equal(DeviceCommandOutcomeNames.ProtectionApplied, applied.Outcome);

        fixture.Platform.FailFetch = true;
        var failed = await fixture.Enforcer.RefreshAsync(CancellationToken.None);
        Assert.Equal(DeviceCommandOutcomeNames.ProtectionUnavailable, failed.Outcome);
    }

    /// <summary>
    /// Запреты для игрока живут в его кусте, а не в HKLM: иначе они задевали бы и администратора с техником.
    /// Машинным остаётся только то, что Windows по-другому не читает.
    /// </summary>
    [Fact]
    public async Task PlayerRestrictions_GoToThePlayersHive_AndOnlyWhatMustBeMachineWideStaysInHklm()
    {
        var fixture = new Fixture(stored: Profile(2) with { DisableRunDialog = true });

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        foreach (var name in new[] { "NoRun", "NoLogoff", "DisableLockWorkstation", "DisableChangePassword", "Deny_All", "NoDrives" })
        {
            Assert.Equal(PolicyScope.Player, fixture.Registry.Scopes[name]);
        }

        Assert.Equal(PolicyScope.Machine, fixture.Registry.Scopes["HideFastUserSwitching"]);
    }

    /// <summary>ПК только включился: игрок ещё не вошёл, его куст не загружен — писать в него нельзя.</summary>
    [Fact]
    public async Task UntilThePlayerSignsIn_OnlyMachinePoliciesAreWritten_AndTheRestIsAppliedWithTheNextHeartbeat()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Registry.SignedIn = false;

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Equal(["HideFastUserSwitching"], fixture.Registry.Scopes.Keys);
        Assert.Empty(fixture.Platform.Reports);

        fixture.Registry.SignedIn = true;
        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Contains(("Deny_All", 1), fixture.Registry.Values);
        Assert.Equal(ProtectionItemStatusNames.Applied, Status(fixture.Platform.Reports.Single(), ProtectionItemNames.RemovableStorage));
    }

    /// <summary>Киоск снят: запрещать некому, а машинное от прежнего применения не должно остаться на администраторе.</summary>
    [Fact]
    public async Task WithoutAPlayerAccount_NothingIsWrittenForThePlayer_AndTheReportSaysSo()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Registry.PlayerAccount = false;

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Empty(fixture.Registry.Values);
        Assert.Contains("HideFastUserSwitching", fixture.Registry.Removed);
        var report = fixture.Platform.Reports.Single();
        Assert.Equal(ProtectionItemStatusNames.Unsupported, Status(report, ProtectionItemNames.KioskBaseline));
        Assert.Equal(ProtectionItemStatusNames.Unsupported, Status(report, ProtectionItemNames.RemovableStorage));
    }

    /// <summary>
    /// «Нет киоска — ничего не держим»: машинные значения профиля (браузер, смена пользователя) и
    /// Диспетчер задач от прежнего киоска снимаются, а не пишутся. Иначе на админском столе оставался
    /// бы запрет, поставленный «для игроков».
    /// </summary>
    [Fact]
    public async Task WithoutAPlayerAccount_NoMachinePolicyIsHeld_EvenTheTaskManagerOfAFormerKiosk()
    {
        var fixture = new Fixture(stored: Profile(2) with { BlockBrowserDownloads = true, BlockBrowserIncognito = true });
        fixture.Registry.PlayerAccount = false;

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Empty(fixture.Registry.Values);
        foreach (var name in new[] { "DisableTaskMgr", "HideFastUserSwitching", "DownloadRestrictions", "IncognitoModeAvailability", "InPrivateModeAvailability" })
        {
            Assert.Contains(name, fixture.Registry.RemovedMachineWide);
        }
    }

    /// <summary>Не только при смене профиля: версия та же и всё «применено», а сняться должно на каждом сердцебиении.</summary>
    [Fact]
    public async Task WithoutAPlayerAccount_EveryHeartbeatReleasesAgain()
    {
        var fixture = new Fixture(stored: Profile(2));
        fixture.Registry.PlayerAccount = false;
        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);
        fixture.Registry.RemovedMachineWide.Clear();

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.Contains("DisableTaskMgr", fixture.Registry.RemovedMachineWide);
        Assert.Empty(fixture.Registry.Values);
    }

    /// <summary>С киоском Диспетчер задач — забота блокировки ПК, профиль его не трогает.</summary>
    [Fact]
    public async Task WithAPlayerAccount_TheTaskManagerIsLeftToTheLock()
    {
        var fixture = new Fixture(stored: Profile(2));

        await fixture.Enforcer.SyncAsync(2, CancellationToken.None);

        Assert.DoesNotContain("DisableTaskMgr", fixture.Registry.Removed);
    }

    /// <summary>Прежний агент писал эти запреты в HKLM — на всех; после обновления они не должны остаться на администраторе.</summary>
    [Fact]
    public async Task MachineWideCopiesLeftByAnOlderAgent_AreRemoved()
    {
        var fixture = new Fixture(stored: Profile(2) with { DisableRunDialog = true });

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        foreach (var name in new[] { "NoRun", "NoLogoff", "DisableLockWorkstation", "DisableChangePassword", "Deny_All", "NoDrives" })
        {
            Assert.Contains(name, fixture.Registry.RemovedMachineWide);
        }

        Assert.DoesNotContain("HideFastUserSwitching", fixture.Registry.RemovedMachineWide);
    }

    [Fact]
    public async Task ABranchThatNeverSavedAProfile_GetsTheDefaults()
    {
        var fixture = new Fixture(stored: null);

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Contains(("NoRun", 1), fixture.Registry.Values);
        Assert.DoesNotContain(("Deny_All", 1), fixture.Registry.Values);
        Assert.Equal(
            ["ConsoleWindowClass", "TaskManagerWindow", "RegEdit_RegEdit"],
            fixture.Enforcer.BlockedWindows.Select(rule => rule.ClassName));
    }

    /// <summary>Прежние агенты сохраняли в версию 0 пустой набор: после обновления ПК берёт умолчания, а не его.</summary>
    [Fact]
    public async Task AnEmptyVersionZeroSavedByAnOlderAgent_IsReplacedByTheDefaults()
    {
        var fixture = new Fixture(stored: new ProtectionProfileDto(0, false, false, false, false, [], [], [], SessionTraceNames.All));

        await fixture.Enforcer.ApplyAsync(CancellationToken.None);

        Assert.Contains(("NoRun", 1), fixture.Registry.Values);
    }

    private static string? Status(DeviceProtectionReportRequest report, string item) =>
        report.Items.SingleOrDefault(candidate => candidate.Item == item)?.Status;

    private sealed class Fixture
    {
        public Fixture(ProtectionProfileDto? stored)
        {
            Store = new MemoryStore(stored);
            Enforcer = new ProtectionEnforcer(
                Registry, Store, Platform, Runtime,
                Microsoft.Extensions.Options.Options.Create(new AgentOptions()),
                Clock, NullLogger<ProtectionEnforcer>.Instance);
        }

        public FakeRegistry Registry { get; } = new();

        public MemoryStore Store { get; }

        public FakePlatform Platform { get; } = new();

        public PlayerShellStateBuilderTests.MemoryRuntimeStateStore Runtime { get; } = new(AgentRuntimeState.Locked(Now));

        public MovableClock Clock { get; } = new(Now);

        public ProtectionEnforcer Enforcer { get; }
    }

    internal sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    internal sealed class FakeRegistry : IMachineRegistry
    {
        public bool Supported { get; set; } = true;

        public bool PlayerAccount { get; set; } = true;

        public bool SignedIn { get; set; } = true;

        /// <summary>В какую ветку ушла каждая запись — по имени значения.</summary>
        public Dictionary<string, PolicyScope> Scopes { get; } = [];

        public string? FailOn { get; set; }

        public List<(string Name, int? Number)> Values { get; } = [];

        public List<string> Removed { get; } = [];

        public List<string> RemovedMachineWide { get; } = [];

        public bool IsSupported => Supported;

        public bool HasPlayerAccount => PlayerAccount;

        public bool PlayerSignedIn => SignedIn;

        public void Write(RegistryWrite write)
        {
            Assert.True(write.Scope == PolicyScope.Machine || (PlayerAccount && SignedIn), "A player write needs a signed-in player.");
            Scopes[write.Name] = write.Scope;
            if (write.Name == FailOn)
            {
                throw new UnauthorizedAccessException("Access is denied.");
            }

            Values.Add((write.Name, write.Number));
        }

        public void Remove(RegistryWrite write)
        {
            Assert.True(write.Scope == PolicyScope.Machine || (PlayerAccount && SignedIn), "A player write needs a signed-in player.");
            Removed.Add(write.Name);
            if (write.Scope == PolicyScope.Machine)
            {
                RemovedMachineWide.Add(write.Name);
            }
        }
    }

    internal sealed class MemoryStore(ProtectionProfileDto? initial) : IProtectionProfileStore
    {
        public ProtectionProfileDto? Saved { get; private set; }

        public ProtectionProfileDto? Load() => Saved ?? initial;

        public void Save(ProtectionProfileDto profile) => Saved = profile;
    }

    internal sealed class FakePlatform : IProtectionPlatformClient
    {
        public ProtectionProfileDto Profile { get; set; } = ProtectionProfileDefaults.Initial;

        public bool FailFetch { get; set; }

        public bool FailReport { get; set; }

        public int Fetches { get; private set; }

        public List<DeviceProtectionReportRequest> Reports { get; } = [];

        public Task<ProtectionProfileDto> GetProfileAsync(CancellationToken cancellationToken)
        {
            Fetches++;
            return FailFetch ? throw new HttpRequestException("offline") : Task.FromResult(Profile);
        }

        public Task ReportAsync(DeviceProtectionReportRequest report, CancellationToken cancellationToken)
        {
            if (FailReport)
            {
                throw new HttpRequestException("offline");
            }

            Reports.Add(report);
            return Task.CompletedTask;
        }
    }
}
