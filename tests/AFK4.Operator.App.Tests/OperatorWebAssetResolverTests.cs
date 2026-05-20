using AFK4.Operator.App.Web;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorWebAssetResolverTests
{
    [Fact]
    public void Resolve_UsesDevServerBeforeLocalAssets()
    {
        var resolver = new OperatorWebAssetResolver(@"C:\afk4-missing", _ => false);
        var options = new OperatorWebShellOptions
        {
            DevServerUrl = new Uri("http://127.0.0.1:5174")
        };

        var target = resolver.Resolve(options);

        Assert.Equal("dev-server", target.Mode);
        Assert.Equal(new Uri("http://127.0.0.1:5174"), target.Source);
        Assert.False(target.UsesLocalFolder);
    }

    [Fact]
    public void Resolve_UsesViteDistWhenRepositoryRootIsAvailable()
    {
        using var workspace = TempWorkspace.Create();
        var distDirectory = Path.Combine(workspace.Root, "src", "AFK4.Operator.App.Web", "dist");
        Directory.CreateDirectory(distDirectory);
        File.WriteAllText(Path.Combine(distDirectory, "index.html"), "<html></html>");
        var resolver = new OperatorWebAssetResolver(workspace.BaseDirectory);

        var target = resolver.Resolve(new OperatorWebShellOptions());

        Assert.Equal("vite-dist", target.Mode);
        Assert.Equal(new Uri("https://operator.afk4.local/index.html"), target.Source);
        Assert.Equal(distDirectory, target.LocalFolderPath);
    }

    [Fact]
    public void Resolve_UsesCopiedFallbackAssetsWhenDistIsMissing()
    {
        using var workspace = TempWorkspace.CreateWithoutRepositoryRoot();
        var fallbackDirectory = Path.Combine(workspace.BaseDirectory, "WebAssets");
        Directory.CreateDirectory(fallbackDirectory);
        File.WriteAllText(Path.Combine(fallbackDirectory, "index.html"), "<html></html>");
        var resolver = new OperatorWebAssetResolver(workspace.BaseDirectory);

        var target = resolver.Resolve(new OperatorWebShellOptions());

        Assert.Equal("fallback-assets", target.Mode);
        Assert.Equal(new Uri("https://operator.afk4.local/index.html"), target.Source);
        Assert.Equal(fallbackDirectory, target.LocalFolderPath);
    }

    [Fact]
    public void Resolve_ThrowsWhenAssetsAreMissing()
    {
        using var workspace = TempWorkspace.CreateWithoutRepositoryRoot();
        var resolver = new OperatorWebAssetResolver(workspace.BaseDirectory);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(new OperatorWebShellOptions()));

        Assert.Contains("Operator frontend assets", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TempWorkspace : IDisposable
    {
        private TempWorkspace(string root, string baseDirectory)
        {
            Root = root;
            BaseDirectory = baseDirectory;
        }

        public string Root { get; }

        public string BaseDirectory { get; }

        public static TempWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"afk4-web-assets-{Guid.NewGuid():N}");
            var baseDirectory = Path.Combine(root, "src", "AFK4.Operator.App", "bin", "Debug", "net10.0-windows");
            Directory.CreateDirectory(baseDirectory);
            File.WriteAllText(Path.Combine(root, "AFK4.sln"), "");
            return new TempWorkspace(root, baseDirectory);
        }

        public static TempWorkspace CreateWithoutRepositoryRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), $"afk4-web-assets-{Guid.NewGuid():N}");
            var baseDirectory = Path.Combine(root, "bin");
            Directory.CreateDirectory(baseDirectory);
            return new TempWorkspace(root, baseDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
