using System.IO;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

/// <summary>
/// Какой именно мастер увидит человек, открывший AFK4.SetupWizard.exe.
///
/// Раньше в репозитории лежала собранная копия фронтенда, и она молча устаревала: запуск не через
/// релизный скрипт открывал мастер трёхмесячной давности — без экранов оформления, сотрудников,
/// зала и тарифа — и ничем себя не выдавал. Теперь фронтенд линкуется из dist при сборке, а его
/// отсутствие — явный отказ, а не подмена старой версией.
/// </summary>
public sealed class SetupWizardWebAssetResolverTests
{
    private static readonly string BaseDirectory = Path.Combine(Path.GetTempPath(), "afk4-wizard-assets");

    [Fact]
    public void Resolve_WithADevServer_TakesItOverAnythingOnDisk()
    {
        var resolver = new SetupWizardWebAssetResolver(BaseDirectory, _ => true);
        var options = new SetupWizardWebShellOptions { DevServerUrl = new Uri("http://127.0.0.1:5175") };

        var target = resolver.Resolve(options);

        Assert.Equal("dev-server", target.Mode);
        Assert.Equal(new Uri("http://127.0.0.1:5175"), target.Source);
        Assert.False(target.UsesLocalFolder);
    }

    [Fact]
    public void Resolve_WithAssetsNextToTheExe_ServesThemFromTheLocalVirtualHost()
    {
        var assetsIndex = Path.Combine(BaseDirectory, "WebAssets", "index.html");
        var resolver = new SetupWizardWebAssetResolver(BaseDirectory, path => path == assetsIndex);

        var target = resolver.Resolve(new SetupWizardWebShellOptions());

        Assert.Equal("fallback-assets", target.Mode);
        Assert.Equal($"https://{SetupWizardWebAssetResolver.LocalVirtualHost}/index.html", target.Source.ToString());
        Assert.Equal(Path.GetDirectoryName(assetsIndex), target.LocalFolderPath);
    }

    // Ради этого тест и написан: без собранного фронтенда мастер обязан сказать об этом, а не
    // открыться чем-то похожим на мастер.
    [Fact]
    public void Resolve_WithoutAnyBuiltFrontend_SaysSoInsteadOfOpeningSomethingElse()
    {
        var resolver = new SetupWizardWebAssetResolver(BaseDirectory, _ => false);

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(new SetupWizardWebShellOptions()));

        Assert.Contains("Setup Wizard frontend assets were not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromEnvironment_WithoutTheVariable_LeavesTheDevServerUnset()
    {
        var options = SetupWizardWebShellOptions.LoadFromEnvironment(_ => null);

        Assert.Null(options.DevServerUrl);
    }

    // Адрес фронтенда приходит из переменной окружения, и он же становится источником страницы:
    // чужой хост здесь означал бы «покажи мастеру всё, что угодно, с чужой машины».
    [Theory]
    [InlineData("http://example.com:5175")]
    [InlineData("ftp://127.0.0.1:5175")]
    [InlineData("не адрес")]
    public void LoadFromEnvironment_WithSomethingOtherThanALocalHttpUrl_Refuses(string value)
    {
        Assert.Throws<InvalidOperationException>(() => SetupWizardWebShellOptions.LoadFromEnvironment(_ => value));
    }

    [Fact]
    public void LoadFromEnvironment_WithALoopbackUrl_KeepsIt()
    {
        var options = SetupWizardWebShellOptions.LoadFromEnvironment(_ => " http://localhost:5175 ");

        Assert.Equal(new Uri("http://localhost:5175"), options.DevServerUrl);
    }
}
