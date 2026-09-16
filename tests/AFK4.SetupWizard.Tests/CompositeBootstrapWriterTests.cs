using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

/// <summary>
/// Машинная конфигурация пишется в два места сразу: файл под %ProgramData% для агента и машинные
/// переменные среды для панели управляющего. Потерянный на полпути второй адресат означал бы
/// устройство, которое агент видит, а приложение — нет.
/// </summary>
public sealed class CompositeBootstrapWriterTests
{
    private static readonly SetupWizardBootstrapConfig Config = new(
        OrganizationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        BranchId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        DeviceId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
        CredentialId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
        CredentialSecret: "secret",
        Role: "gaming_pc",
        ApiBaseUrl: "https://api.afk4.net",
        UpdateChannel: "stable",
        LeaseSigningPublicKeyPem: "lease-pem",
        UpdatePackageSigningPublicKeyPem: "update-pem");

    [Fact]
    public void Write_ReachesEveryWriterInOrder()
    {
        var order = new List<string>();
        var writer = new CompositeBootstrapWriter(
            new RecordingWriter("file", order),
            new RecordingWriter("environment", order));

        writer.Write(Config);

        Assert.Equal(["file", "environment"], order);
    }

    [Fact]
    public void Write_PassesTheSameConfigurationOn()
    {
        var file = new RecordingWriter("file", []);
        var environment = new RecordingWriter("environment", []);

        new CompositeBootstrapWriter(file, environment).Write(Config);

        Assert.Same(Config, file.Written);
        Assert.Same(Config, environment.Written);
    }

    // Отказ одного адресата не должен выглядеть как успешная запись: устройство, у которого файл
    // есть, а переменных нет, — наполовину настроенное, и мастер обязан это показать.
    [Fact]
    public void Write_WhenOneWriterFails_LetsTheFailureThrough()
    {
        var environment = new RecordingWriter("environment", []);
        var writer = new CompositeBootstrapWriter(new ThrowingWriter(), environment);

        Assert.Throws<UnauthorizedAccessException>(() => writer.Write(Config));
        Assert.Null(environment.Written);
    }

    private sealed class RecordingWriter(string name, List<string> order) : ISetupWizardBootstrapWriter
    {
        public SetupWizardBootstrapConfig? Written { get; private set; }

        public void Write(SetupWizardBootstrapConfig config)
        {
            order.Add(name);
            Written = config;
        }
    }

    private sealed class ThrowingWriter : ISetupWizardBootstrapWriter
    {
        public void Write(SetupWizardBootstrapConfig config) =>
            throw new UnauthorizedAccessException("ProgramData is not writable.");
    }
}
