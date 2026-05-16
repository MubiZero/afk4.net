using System.Diagnostics;
using System.Globalization;
using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class ClientReleaseAutomationTests : IDisposable
{
    private const int PowerShellTimeoutMilliseconds = 30_000;

    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-release-automation-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SignClientPackagesScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/sign-client-packages.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "PackagePath");
        AssertParameter(ast, "PackageDirectory");
        AssertParameter(ast, "CertificatePath");
        AssertParameter(ast, "CertificatePasswordEnvVar");
        AssertParameter(ast, "CertificateSha1");
        AssertParameter(ast, "CertificateStoreLocation");
        AssertParameter(ast, "CertificateStoreName");
        AssertParameter(ast, "TimestampUrl");
        AssertParameter(ast, "SigntoolPath");
    }

    [Fact]
    public void SignClientPackagesScript_SearchesWindowsSdkSigntoolLocations()
    {
        var script = File.ReadAllText(ScriptPath("scripts/sign-client-packages.ps1"));

        Assert.Contains("ProgramFiles(x86)", script, StringComparison.Ordinal);
        Assert.Contains("ProgramFiles", script, StringComparison.Ordinal);
        Assert.Contains("Windows Kits", script, StringComparison.Ordinal);
        Assert.Contains("10", script, StringComparison.Ordinal);
        Assert.Contains("x64", script, StringComparison.Ordinal);
        Assert.Contains("Sort-Object", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SignClientPackages_WithPfxSource_InvokesSigntoolForExplicitPackage()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");
        var certificatePath = Path.Combine(tempRoot, "release-signing.pfx");
        File.WriteAllText(certificatePath, "pfx");
        var capturedArgumentsPath = Path.Combine(tempRoot, "signtool-args.txt");
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(capturedArgumentsPath, exitCode: 0);

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_PFX_PASSWORD"] = "test-password"
            },
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificatePath", certificatePath,
            "-CertificatePasswordEnvVar", "AFK4_TEST_PFX_PASSWORD",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.Equal(0, result.ExitCode);
        var capturedArguments = File.ReadAllLines(capturedArgumentsPath);
        Assert.Contains("sign", capturedArguments);
        Assert.Contains("/fd", capturedArguments);
        Assert.Contains("SHA256", capturedArguments);
        Assert.Contains("/tr", capturedArguments);
        Assert.Contains("http://timestamp.test", capturedArguments);
        Assert.Contains("/f", capturedArguments);
        Assert.Contains(certificatePath, capturedArguments);
        Assert.Contains("/p", capturedArguments);
        Assert.Contains(packagePath, capturedArguments);
    }

    [Fact]
    public void SignClientPackages_WithCertificateStoreSource_InvokesSigntoolWithStoreArguments()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-gaming-pc-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");
        var capturedArgumentsPath = Path.Combine(tempRoot, "signtool-store-args.txt");
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(capturedArgumentsPath, exitCode: 0);

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificateSha1", "abcdef1234567890abcdef1234567890abcdef12",
            "-CertificateStoreLocation", "LocalMachine",
            "-CertificateStoreName", "TrustedPublisher",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.Equal(0, result.ExitCode);
        var capturedArguments = File.ReadAllLines(capturedArgumentsPath);
        Assert.Contains("/sha1", capturedArguments);
        Assert.Contains("abcdef1234567890abcdef1234567890abcdef12", capturedArguments);
        Assert.Contains("/s", capturedArguments);
        Assert.Contains("TrustedPublisher", capturedArguments);
        Assert.Contains("/sm", capturedArguments);
        Assert.Contains(packagePath, capturedArguments);
    }

    [Fact]
    public void SignClientPackages_WhenSigntoolReturnsNonZero_FailsPackage()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");
        var certificatePath = Path.Combine(tempRoot, "release-signing.pfx");
        File.WriteAllText(certificatePath, "pfx");
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(Path.Combine(tempRoot, "signtool-failure-args.txt"), exitCode: 17);

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_PFX_PASSWORD"] = "test-password"
            },
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificatePath", certificatePath,
            "-CertificatePasswordEnvVar", "AFK4_TEST_PFX_PASSWORD",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("signtool failed for", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public void SignClientPackages_WithPackagePathDirectory_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "directory-package.msi");
        Directory.CreateDirectory(packagePath);
        var certificatePath = Path.Combine(tempRoot, "release-signing.pfx");
        File.WriteAllText(certificatePath, "pfx");
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(Path.Combine(tempRoot, "signtool-args.txt"), exitCode: 0);

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_PFX_PASSWORD"] = "test-password"
            },
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificatePath", certificatePath,
            "-CertificatePasswordEnvVar", "AFK4_TEST_PFX_PASSWORD",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.NotEqual(0, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("Package '", output);
        Assert.Contains("directory-package.msi", output);
        Assert.Contains("was not found.", output);
    }

    [Fact]
    public void SignClientPackages_WithCertificatePathDirectory_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");
        var certificatePath = Path.Combine(tempRoot, "release-signing.pfx");
        Directory.CreateDirectory(certificatePath);
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(Path.Combine(tempRoot, "signtool-args.txt"), exitCode: 0);

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_PFX_PASSWORD"] = "test-password"
            },
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificatePath", certificatePath,
            "-CertificatePasswordEnvVar", "AFK4_TEST_PFX_PASSWORD",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.NotEqual(0, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("CertificatePath '", output);
        Assert.Contains("release-signing.pfx", output);
        Assert.Contains("was not found.", output);
    }

    [Fact]
    public void SignClientPackages_WithPackageDirectoryFile_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        File.WriteAllText(packageDirectory, "not a directory");
        var fakeSigntoolPath = CreateFakeSigntoolThatRecordsArguments(Path.Combine(tempRoot, "signtool-args.txt"), exitCode: 0);

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackageDirectory", packageDirectory,
            "-CertificateSha1", "abcdef1234567890abcdef1234567890abcdef12",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.NotEqual(0, result.ExitCode);
        var output = result.StandardError + result.StandardOutput;
        Assert.Contains("Package directory '", output);
        Assert.Contains("client-packages", output);
        Assert.Contains("was not found.", output);
    }

    [Fact]
    public void SignClientPackages_WithoutExactlyOneSigningSource_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", ScriptPath("scripts/sign-client-packages.ps1"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one Authenticode signing source", result.StandardError + result.StandardOutput);
    }

    private static ScriptBlockAst ParseScript(string relativePath, out ParseError[] errors)
    {
        var absolutePath = ScriptPath(relativePath);
        var ast = Parser.ParseFile(absolutePath, out _, out errors);
        return ast;
    }

    private static void AssertParameter(ScriptBlockAst ast, string parameterName)
    {
        Assert.NotNull(ast.ParamBlock);
        Assert.Contains(
            ast.ParamBlock.Parameters,
            parameter => string.Equals(parameter.Name.VariablePath.UserPath, parameterName, StringComparison.Ordinal));
    }

    private static ProcessResult RunPowerShell(
        IReadOnlyDictionary<string, string?>? environment,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("PowerShell process did not start.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(PowerShellTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            var timedOutOutput = standardOutputTask.GetAwaiter().GetResult();
            var timedOutError = standardErrorTask.GetAwaiter().GetResult();
            return new ProcessResult(
                -1,
                timedOutOutput,
                timedOutError + Environment.NewLine + "PowerShell process timed out after " + PowerShellTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms.");
        }

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string ScriptPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), relativePath));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        return directory.FullName;
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private string CreateFakeSigntoolThatRecordsArguments(string capturePath, int exitCode)
    {
        var fakeSigntoolPath = Path.Combine(tempRoot, $"fake-signtool-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(
            fakeSigntoolPath,
            "@echo off" + Environment.NewLine +
            "setlocal" + Environment.NewLine +
            ":capture" + Environment.NewLine +
            "if \"%~1\"==\"\" goto done" + Environment.NewLine +
            ">>\"" + capturePath + "\" echo(%~1" + Environment.NewLine +
            "shift" + Environment.NewLine +
            "goto capture" + Environment.NewLine +
            ":done" + Environment.NewLine +
            "exit /b " + exitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
        return fakeSigntoolPath;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
