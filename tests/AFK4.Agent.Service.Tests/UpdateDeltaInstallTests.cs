using System.Text.Json;
using AFK4.Agent.Service.Updates;
using AFK4.Agent.Service.Updates.Delta;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class UpdateDeltaInstallTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-delta-install-{Guid.NewGuid():N}");
    private readonly DeltaTestKit kit = new();

    public void Dispose()
    {
        kit.Dispose();
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── подмена ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Applier_ReplacesAndAdds_KeepsTheOldCopies_ThenRecoveryDropsThem()
    {
        var (install, staged) = await StageAsync(("App.dll", "app-v1", "app-v2"), ("added/New.dll", null, "new"));
        var journal = Path.Combine(tempRoot, "state", "journal.json");

        await UpdateDeltaApplier.ApplyAsync(staged, UpdateComponentNames.AgentService, "1.2.3", journal, CancellationToken.None);

        Assert.Equal("app-v2", await File.ReadAllTextAsync(Path.Combine(install, "App.dll")));
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(install, "added", "New.dll")));
        Assert.Equal("app-v1", await File.ReadAllTextAsync(Path.Combine(install, "App.dll" + UpdateDeltaApplier.OldSuffix)));
        Assert.Equal(UpdateDeltaJournal.Applied, ReadJournal(journal).State);

        Assert.Equal(UpdateDeltaJournal.Applied, await UpdateDeltaApplier.RecoverAsync(journal, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(install, "App.dll" + UpdateDeltaApplier.OldSuffix)));
        Assert.False(File.Exists(journal));
        Assert.Equal("app-v2", await File.ReadAllTextAsync(Path.Combine(install, "App.dll")));
    }

    [Fact]
    public async Task Applier_OnAnyFailure_PutsEveryReplacedFileBack_AndRemovesWhatItAdded()
    {
        var (install, staged) = await StageAsync(("A.dll", "a-v1", "a-v2"), ("added/B.dll", null, "b"), ("C.dll", "c-v1", "c-v2"));
        File.Delete(staged.Files[2].StagedPath); // третий файл не скопируется: подмена обрывается на нём
        var journal = Path.Combine(tempRoot, "state", "journal.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            UpdateDeltaApplier.ApplyAsync(staged, UpdateComponentNames.AgentService, "1.2.3", journal, CancellationToken.None));

        Assert.Equal("a-v1", await File.ReadAllTextAsync(Path.Combine(install, "A.dll")));
        Assert.Equal("c-v1", await File.ReadAllTextAsync(Path.Combine(install, "C.dll")));
        Assert.False(File.Exists(Path.Combine(install, "added", "B.dll")));
        Assert.Empty(Directory.EnumerateFiles(install, "*.afk4*", SearchOption.AllDirectories));
        Assert.False(File.Exists(journal));
    }

    [Fact]
    public async Task Recovery_RollsBackAnApplyThatWasCutOffMidway()
    {
        // Обрыв питания посреди подмены: A уже заменён (есть .afk4old), B добавлен, C не тронут.
        var install = Directory.CreateDirectory(Path.Combine(tempRoot, "install")).FullName;
        await File.WriteAllTextAsync(Path.Combine(install, "A.dll"), "a-v2");
        await File.WriteAllTextAsync(Path.Combine(install, "A.dll" + UpdateDeltaApplier.OldSuffix), "a-v1");
        await File.WriteAllTextAsync(Path.Combine(install, "B.dll"), "b");
        await File.WriteAllTextAsync(Path.Combine(install, "C.dll"), "c-v1");
        var journal = Path.Combine(tempRoot, "state", "journal.json");
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        await File.WriteAllTextAsync(journal, JsonSerializer.Serialize(
            new UpdateDeltaJournal(UpdateComponentNames.AgentService, "1.2.3", UpdateDeltaJournal.Applying,
            [
                new UpdateDeltaJournalEntry(Path.Combine(install, "A.dll"), Existed: true),
                new UpdateDeltaJournalEntry(Path.Combine(install, "B.dll"), Existed: false),
                new UpdateDeltaJournalEntry(Path.Combine(install, "C.dll"), Existed: true)
            ]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(UpdateDeltaJournal.Applying, await UpdateDeltaApplier.RecoverAsync(journal, CancellationToken.None));

        Assert.Equal("a-v1", await File.ReadAllTextAsync(Path.Combine(install, "A.dll")));
        Assert.False(File.Exists(Path.Combine(install, "B.dll")));
        Assert.Equal("c-v1", await File.ReadAllTextAsync(Path.Combine(install, "C.dll")));
        Assert.False(File.Exists(journal));
    }

    [Fact]
    public async Task Recovery_WithoutAJournal_DoesNothing()
    {
        Assert.Null(await UpdateDeltaApplier.RecoverAsync(Path.Combine(tempRoot, "none.json"), CancellationToken.None));
    }

    // ── установщик дельты ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Installer_DownloadsOnlyTheChange_ReplacesIt_BumpsTheVersion_AndSchedulesARestart()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        await File.WriteAllTextAsync(Path.Combine(service, "Runtime.dll"), "runtime");
        await File.WriteAllTextAsync(Path.Combine(service, "App.dll"), "app-v1");
        var (instruction, bytes) = kit.SignedInstruction(
        [
            DeltaTestKit.Entry("agent-service", "Runtime.dll", "runtime"),
            DeltaTestKit.Entry("agent-service", "App.dll", "app-v2")
        ]);
        var store = new DeltaTestKit.Store().WithManifest(bytes).WithBlobs("app-v2");
        var environment = new DeltaTestKit.RecordingEnvironment();
        var restart = new DeltaTestKit.RecordingRestart();
        var installer = CreateInstaller(store, new DeltaTestKit.Roots(("agent-service", service)), environment, restart);

        var preparation = await installer.PrepareAsync(instruction, CancellationToken.None);
        var result = await installer.ApplyAsync(instruction, preparation!, "1.2.2", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Succeeded, result.Message);
        Assert.Single(preparation!.Plan.ToFetch);
        Assert.DoesNotContain(store.Requests, uri => uri.Contains(DeltaTestKit.Sha("runtime"), StringComparison.Ordinal));
        Assert.Equal("app-v2", await File.ReadAllTextAsync(Path.Combine(service, "App.dll")));
        Assert.Equal("1.2.3", environment.Values["Agent__AgentVersion"]);
        var restarted = Assert.Single(restart.Scheduled);
        Assert.Equal(UpdateStatusNames.Installed, restarted.Status);
        Assert.Equal("1.2.3", restarted.InstalledVersion);
        Assert.False(Directory.Exists(preparation.Staged.StageDirectory));
    }

    [Fact]
    public async Task Installer_DeclinesWhenDisabled_WithoutManifest_NotWorthIt_OrWhenAnInstallFolderIsMissing()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var (instruction, bytes) = kit.SignedInstruction([DeltaTestKit.Entry("agent-service", "App.dll", "app-v2")], packageSizeBytes: 5);
        var store = new DeltaTestKit.Store().WithManifest(bytes).WithBlobs("app-v2");
        var roots = new DeltaTestKit.Roots(("agent-service", service));

        Assert.Null(await CreateInstaller(store, roots, enabled: false).PrepareAsync(instruction, CancellationToken.None));
        Assert.Null(await CreateInstaller(store, roots).PrepareAsync(instruction with { Manifest = null }, CancellationToken.None));
        // Пакет в 5 байт: качать дельту дороже целого MSI.
        Assert.Null(await CreateInstaller(store, roots).PrepareAsync(instruction, CancellationToken.None));
        Assert.Null(await CreateInstaller(store, new DeltaTestKit.Roots()).PrepareAsync(instruction with { SizeBytes = 60_000_000 }, CancellationToken.None));
    }

    [Fact]
    public async Task Installer_FallsBackWhenABlobIsMissingOrTheManifestIsForged()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var (instruction, bytes) = kit.SignedInstruction([DeltaTestKit.Entry("agent-service", "App.dll", "app-v2")]);
        var roots = new DeltaTestKit.Roots(("agent-service", service));

        var noBlob = await CreateInstaller(new DeltaTestKit.Store().WithManifest(bytes), roots).PrepareAsync(instruction, CancellationToken.None);
        var forged = await CreateInstaller(new DeltaTestKit.Store().WithManifest([.. bytes, 0x20]).WithBlobs("app-v2"), roots)
            .PrepareAsync(instruction, CancellationToken.None);

        Assert.Null(noBlob);
        Assert.Null(forged);
        Assert.False(File.Exists(Path.Combine(service, "App.dll")));
    }

    [Fact]
    public async Task Installer_WhenTheSwapFails_RollsBackAndReturnsNull_SoTheFullPackageIsInstalled()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        await File.WriteAllTextAsync(Path.Combine(service, "App.dll"), "app-v1");
        var (instruction, bytes) = kit.SignedInstruction([DeltaTestKit.Entry("agent-service", "App.dll", "app-v2")]);
        var environment = new DeltaTestKit.RecordingEnvironment();
        var restart = new DeltaTestKit.RecordingRestart();
        var installer = CreateInstaller(
            new DeltaTestKit.Store().WithManifest(bytes).WithBlobs("app-v2"), new DeltaTestKit.Roots(("agent-service", service)), environment, restart);
        var preparation = (await installer.PrepareAsync(instruction, CancellationToken.None))!;
        File.Delete(preparation.Staged.Files[0].StagedPath);

        var result = await installer.ApplyAsync(instruction, preparation, "1.2.2", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal("app-v1", await File.ReadAllTextAsync(Path.Combine(service, "App.dll")));
        Assert.Empty(environment.Values);
        Assert.Empty(restart.Scheduled);
    }

    // ── вспомогательное ──────────────────────────────────────────────────────────────────────

    private async Task<(string Install, StagedUpdate Staged)> StageAsync(params (string Path, string? Old, string New)[] files)
    {
        var install = Directory.CreateDirectory(Path.Combine(tempRoot, "install")).FullName;
        var stage = Directory.CreateDirectory(Path.Combine(tempRoot, "stage")).FullName;
        var staged = new List<StagedUpdateFile>();
        foreach (var (path, old, @new) in files)
        {
            var target = Path.Combine(install, path.Replace('/', Path.DirectorySeparatorChar));
            if (old is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, old);
            }

            var stagedPath = Path.Combine(stage, Guid.NewGuid().ToString("N"));
            await File.WriteAllTextAsync(stagedPath, @new);
            staged.Add(new StagedUpdateFile("agent-service", path, stagedPath, target, DeltaTestKit.Sha(@new)));
        }

        return (install, new StagedUpdate(stage, staged));
    }

    private static UpdateDeltaJournal ReadJournal(string path) =>
        JsonSerializer.Deserialize<UpdateDeltaJournal>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private UpdateDeltaInstaller CreateInstaller(
        DeltaTestKit.Store store,
        IUpdateRootResolver roots,
        DeltaTestKit.RecordingEnvironment? environment = null,
        DeltaTestKit.RecordingRestart? restart = null,
        bool enabled = true)
    {
        var options = Options.Create(new AgentOptions
        {
            UpdateDeltaEnabled = enabled,
            UpdatePackageSigningPublicKeyPem = kit.PublicKeyPem,
            UpdateDownloadStallTimeout = TimeSpan.FromSeconds(5),
            UpdateStagingDirectory = Path.Combine(tempRoot, "staging"),
            UpdateStateDirectory = Path.Combine(tempRoot, "state")
        });
        var http = new DeltaTestKit.HttpFactory(store);
        return new UpdateDeltaInstaller(
            NullLogger<UpdateDeltaInstaller>.Instance,
            new UpdateManifestClient(http, options),
            roots,
            new UpdateBlobFetcher(http, options),
            new FileUpdateInstallStateStore(options),
            restart ?? new DeltaTestKit.RecordingRestart(),
            environment ?? new DeltaTestKit.RecordingEnvironment(),
            options,
            TimeProvider.System);
    }
}
