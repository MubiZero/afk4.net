using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class ClientReleaseAutomationTests : IDisposable
{
    private const int PowerShellTimeoutMilliseconds = 120_000;

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
    public void PublishClientMsiUpdatesScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/publish-client-msi-updates.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "Version");
        AssertParameter(ast, "Channel");
        AssertParameter(ast, "OrganizationId");
        AssertParameter(ast, "PackageDirectory");
        AssertParameter(ast, "OutputDirectory");
        AssertParameter(ast, "ArtifactStore");
        AssertParameter(ast, "HostingRoot");
        AssertParameter(ast, "PublicBaseUri");
        AssertParameter(ast, "OperatorArtifactUploadUri");
        AssertParameter(ast, "OperatorArtifactPublicUri");
        AssertParameter(ast, "GamingPcArtifactUploadUri");
        AssertParameter(ast, "GamingPcArtifactPublicUri");
        AssertParameter(ast, "S3Endpoint");
        AssertParameter(ast, "S3Bucket");
        AssertParameter(ast, "S3KeyPrefix");
        AssertParameter(ast, "S3AccessKeyEnvVar");
        AssertParameter(ast, "S3SecretKeyEnvVar");
        AssertParameter(ast, "S3Region");
        AssertParameter(ast, "SigningKeyPath");
        AssertParameter(ast, "SigningKeyEnvVar");
        AssertParameter(ast, "ReleaseNotes");
        AssertParameter(ast, "DotnetPath");
        AssertParameter(ast, "PublishMaxAttempts");
    }

    [Fact]
    public void PublishStagingBootstrapperScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/publish-staging-bootstrapper.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "Version");
        AssertParameter(ast, "Channel");
        AssertParameter(ast, "PackageDirectory");
        AssertParameter(ast, "OutputDirectory");
        AssertParameter(ast, "S3Endpoint");
        AssertParameter(ast, "S3Bucket");
        AssertParameter(ast, "S3KeyPrefix");
        AssertParameter(ast, "S3PublicBaseUri");
        AssertParameter(ast, "S3AccessKeyEnvVar");
        AssertParameter(ast, "S3SecretKeyEnvVar");
        AssertParameter(ast, "S3Region");
        AssertParameter(ast, "PlatformBaseUrl");
        AssertParameter(ast, "OrganizationId");
        AssertParameter(ast, "BranchId");
        AssertParameter(ast, "SmokeSeatId");
        AssertParameter(ast, "LeaseSigningPublicKeyPath");
        AssertParameter(ast, "UpdateSigningPublicKeyPath");
    }

    [Fact]
    public void PublishStagingBootstrapper_BuildsS3ObjectUrisFromSlashDelimitedSegments()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/publish-staging-bootstrapper.ps1")));

        Assert.Contains("$pathSegments = @($Bucket) + ($ObjectKey -split '/')", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Bucket, ($ObjectKey -split '/')", script, StringComparison.Ordinal);
        Assert.Contains("Join-S3Key -Segments @($S3KeyPrefix, $Channel, $Version, $bootstrapFileName)", script, StringComparison.Ordinal);
        Assert.Contains("Join-S3Key -Segments @($S3KeyPrefix, $Channel, 'latest.json')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishStagingBootstrapper_WritesAgentConfigurationBeforeMsiInstall()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/publish-staging-bootstrapper.ps1")));

        var configFunctionIndex = script.IndexOf("function Write-AgentMachineConfiguration", StringComparison.Ordinal);
        var credentialConfigIndex = script.IndexOf("Set-MachineEnvironmentVariable -Name 'Agent__DeviceCredentialSecret'", StringComparison.Ordinal);
        var firstConfigCallIndex = script.IndexOf("Write-AgentMachineConfiguration\nStop-Service AFK4.Agent.Service", StringComparison.Ordinal);
        var msiInstallIndex = script.IndexOf("$msiProcess = Start-Process", StringComparison.Ordinal);
        var postInstallConfigCallIndex = script.LastIndexOf("Write-AgentMachineConfiguration\nStart-Service AFK4.Agent.Service", StringComparison.Ordinal);

        Assert.True(configFunctionIndex >= 0, "The staging bootstrapper should define a reusable Agent machine configuration writer.");
        Assert.True(credentialConfigIndex > configFunctionIndex, "The configuration writer should persist the enrolled device credential secret.");
        Assert.True(firstConfigCallIndex > credentialConfigIndex, "The bootstrapper should call the configuration writer before installing the MSI.");
        Assert.True(msiInstallIndex > firstConfigCallIndex, "Machine configuration must be present before Windows Installer starts AFK4.Agent.Service.");
        Assert.True(postInstallConfigCallIndex > msiInstallIndex, "The bootstrapper should rewrite configuration after MSI install before the final service start.");
    }

    [Fact]
    public void RegisterUpdatePackageRequestsScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/register-update-package-requests.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "PlatformBaseUrl");
        AssertParameter(ast, "BranchId");
        AssertParameter(ast, "RequestPath");
        AssertParameter(ast, "RequestDirectory");
        AssertParameter(ast, "AccessToken");
        AssertParameter(ast, "AccessTokenEnvVar");
        AssertParameter(ast, "CreateRollouts");
        AssertParameter(ast, "RolloutComponent");
        AssertParameter(ast, "RolloutTargetKind");
        AssertParameter(ast, "RolloutTargetDeviceId");
        AssertParameter(ast, "RolloutBatchPercent");
        AssertParameter(ast, "RolloutStartsAtUtc");
        AssertParameter(ast, "RolloutReason");
    }

    [Fact]
    public async Task RegisterUpdatePackageRequests_PostsRequestJsonWithBearerToken()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        const string requestBody = """{"organizationId":"0c04d6c0-bfa8-4e26-9263-fc0d307d0f08","component":"agent-service"}""";
        await File.WriteAllTextAsync(
            requestPath,
            requestBody);
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        var capturedRequestTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            context.Response.StatusCode = 201;
            context.Response.ContentType = "application/json";
            var responseBody = Encoding.UTF8.GetBytes("""{"updatePackageId":"4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7"}""");
            await context.Response.OutputStream.WriteAsync(responseBody);
            context.Response.Close();
            var requestUriPath = context.Request.Url is null
                ? string.Empty
                : context.Request.Url.AbsolutePath;
            var authorization = context.Request.Headers["Authorization"];
            return new CapturedHttpRequest(
                context.Request.HttpMethod,
                requestUriPath,
                authorization is null ? string.Empty : authorization,
                body);
        });

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_REGISTRATION_TOKEN"] = "test-token"
            },
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", baseUrl.TrimEnd('/'),
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessTokenEnvVar", "AFK4_TEST_REGISTRATION_TOKEN");

        if (result.ExitCode != 0)
        {
            listener.Stop();
        }

        Assert.Equal(0, result.ExitCode);
        var capturedRequest = await capturedRequestTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("POST", capturedRequest.Method);
        Assert.Equal("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequest.Path);
        Assert.Equal("Bearer test-token", capturedRequest.Authorization);
        Assert.Equal(requestBody, capturedRequest.Body);
    }

    [Fact]
    public async Task RegisterUpdatePackageRequests_WithCreateRollouts_PostsDeviceRolloutForSelectedComponent()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        const string requestBody = """{"organizationId":"0c04d6c0-bfa8-4e26-9263-fc0d307d0f08","component":"agent-service","channel":"internal"}""";
        await File.WriteAllTextAsync(requestPath, requestBody);
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        var capturedRequestsTask = Task.Run(async () =>
        {
            var requests = new List<CapturedHttpRequest>();
            for (var index = 0; index < 2; index++)
            {
                var context = await listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                context.Response.StatusCode = 201;
                context.Response.ContentType = "application/json";
                var responseBody = index == 0
                    ? Encoding.UTF8.GetBytes("""{"updatePackageId":"4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7"}""")
                    : Encoding.UTF8.GetBytes("""{"updateRolloutId":"7c62965e-fc6b-4e7d-a40a-11dac4a3c544"}""");
                await context.Response.OutputStream.WriteAsync(responseBody);
                context.Response.Close();
                requests.Add(new CapturedHttpRequest(
                    context.Request.HttpMethod,
                    context.Request.Url?.AbsolutePath ?? string.Empty,
                    context.Request.Headers["Authorization"] ?? string.Empty,
                    body));
            }

            return requests;
        });

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", baseUrl.TrimEnd('/'),
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token",
            "-CreateRollouts",
            "-RolloutComponent", "agent-service",
            "-RolloutTargetKind", "device",
            "-RolloutTargetDeviceId", "0588fb59-3edb-4704-bbdb-094e12417cf1",
            "-RolloutReason", "Automated smoke rollout.");

        if (result.ExitCode != 0)
        {
            listener.Stop();
        }

        Assert.Equal(0, result.ExitCode);
        var capturedRequests = await capturedRequestsTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequests[0].Path);
        Assert.Equal("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts", capturedRequests[1].Path);
        Assert.Contains("\"updatePackageId\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"targetKind\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"device\"", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("0588fb59-3edb-4704-bbdb-094e12417cf1", capturedRequests[1].Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    public async Task RegisterUpdatePackageRequests_WhenPlatformReturnsError_ExitsNonZero(int statusCode)
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        await File.WriteAllTextAsync(requestPath, """{"component":"agent-service"}""");
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        var capturedRequestTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            context.Response.StatusCode = statusCode;
            context.Response.Close();
            return body;
        });

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", baseUrl.TrimEnd('/'),
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token");

        if (!capturedRequestTask.IsCompleted)
        {
            listener.Stop();
        }

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("""{"component":"agent-service"}""", await capturedRequestTask.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void RegisterUpdatePackageRequests_WithoutAccessTokenSource_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "http://127.0.0.1:9",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one access token source", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public void RegisterUpdatePackageRequests_WithBothAccessTokenSources_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_REGISTRATION_TOKEN"] = "test-token-from-env"
            },
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "http://127.0.0.1:9",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token",
            "-AccessTokenEnvVar", "AFK4_TEST_REGISTRATION_TOKEN");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one access token source", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public async Task RegisterUpdatePackageRequests_WithRequestDirectory_PostsRequestJsonInNameOrder()
    {
        var requestDirectory = Path.Combine(tempRoot, "requests");
        Directory.CreateDirectory(requestDirectory);
        var firstBody = """{"component":"agent-service","order":1}""";
        var secondBody = """{"component":"operator-app","order":2}""";
        var thirdBody = """{"component":"player-shell","order":3}""";
        await File.WriteAllTextAsync(Path.Combine(requestDirectory, "02-operator-app-request.json"), secondBody);
        await File.WriteAllTextAsync(Path.Combine(requestDirectory, "01-agent-service-request.json"), firstBody);
        await File.WriteAllTextAsync(Path.Combine(requestDirectory, "03-player-shell-request.json"), thirdBody);
        await File.WriteAllTextAsync(Path.Combine(requestDirectory, "00-ignored.json"), """{"component":"ignored"}""");
        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        var capturedRequestsTask = Task.Run(async () =>
        {
            var requests = new List<CapturedHttpRequest>();
            for (var index = 0; index < 3; index++)
            {
                var context = await listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                context.Response.StatusCode = 201;
                context.Response.ContentType = "application/json";
                var responseBody = Encoding.UTF8.GetBytes("""{"updatePackageId":"4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7"}""");
                await context.Response.OutputStream.WriteAsync(responseBody);
                context.Response.Close();
                var requestUriPath = context.Request.Url is null
                    ? string.Empty
                    : context.Request.Url.AbsolutePath;
                var authorization = context.Request.Headers["Authorization"];
                requests.Add(new CapturedHttpRequest(
                    context.Request.HttpMethod,
                    requestUriPath,
                    authorization is null ? string.Empty : authorization,
                    body));
            }

            return requests;
        });

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", baseUrl.TrimEnd('/'),
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestDirectory", requestDirectory,
            "-AccessToken", "test-token");

        if (result.ExitCode != 0)
        {
            listener.Stop();
        }

        Assert.Equal(0, result.ExitCode);
        var capturedRequests = await capturedRequestsTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { firstBody, secondBody, thirdBody }, capturedRequests.Select(request => request.Body));
        Assert.All(capturedRequests, request => Assert.Equal("POST", request.Method));
        Assert.All(capturedRequests, request => Assert.Equal("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", request.Path));
        Assert.All(capturedRequests, request => Assert.Equal("Bearer test-token", request.Authorization));
    }

    [Fact]
    public void RegisterUpdatePackageRequests_WithNonRequestJsonPath_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "http://127.0.0.1:9",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("RequestPath must reference *-request.json files.", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public void RegisterUpdatePackageRequests_WithNonHttpPlatformBaseUrl_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "file:///tmp/afk4",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PlatformBaseUrl must use http or https scheme.", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public void ClientPackagesWorkflow_ContainsGuardedSigningPublishingAndRegistrationSteps()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml")));
        var guardStep = ExtractWorkflowStep(workflow, "Guard release mode");
        var buildStep = ExtractWorkflowStep(workflow, "Build client packages");
        var publishStep = ExtractWorkflowStep(workflow, "Publish update metadata");
        var publishRunBlock = ExtractWorkflowRunBlock(publishStep);
        var registrationStep = ExtractWorkflowStep(workflow, "Register update packages");
        var registrationRunBlock = ExtractWorkflowRunBlock(registrationStep);

        Assert.Contains("sign_packages:", workflow, StringComparison.Ordinal);
        Assert.Contains("publish_update_metadata:", workflow, StringComparison.Ordinal);
        Assert.Contains("register_update_packages:", workflow, StringComparison.Ordinal);
        Assert.Contains("platform_base_url:", workflow, StringComparison.Ordinal);
        Assert.Contains("Stable releases require signing and update metadata publishing.", workflow, StringComparison.Ordinal);
        Assert.Contains("Backend registration requires publish_update_metadata=true.", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/sign-client-packages.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/publish-client-msi-updates.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/register-update-package-requests.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_AUTHENTICODE_PFX_BASE64", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_UPDATE_SIGNING_KEY_PEM", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_UPDATE_REGISTRATION_TOKEN", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_ALLOWED_PLATFORM_BASE_URLS", workflow, StringComparison.Ordinal);
        Assert.Contains("platform_base_url is not in AFK4_ALLOWED_PLATFORM_BASE_URLS.", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/update-packages/*-request.json", workflow, StringComparison.Ordinal);
        Assert.Contains("ALLOWED_PLATFORM_BASE_URLS: ${{ vars.AFK4_ALLOWED_PLATFORM_BASE_URLS }}", guardStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_REGISTER_UPDATE_PACKAGES -eq 'true' -and [string]::IsNullOrWhiteSpace($env:ALLOWED_PLATFORM_BASE_URLS)", guardStep, StringComparison.Ordinal);
        Assert.Contains("$requestedPlatformBaseUrl = $env:INPUT_PLATFORM_BASE_URL.Trim().TrimEnd('/')", guardStep, StringComparison.Ordinal);
        Assert.Contains("$allowedPlatformBaseUrls = @(", guardStep, StringComparison.Ordinal);

        Assert.Contains("INPUT_VERSION: ${{ inputs.version }}", buildStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_CHANNEL: ${{ inputs.channel }}", buildStep, StringComparison.Ordinal);
        Assert.Contains("-Version $env:INPUT_VERSION", buildStep, StringComparison.Ordinal);
        Assert.Contains("-Channel $env:INPUT_CHANNEL", buildStep, StringComparison.Ordinal);

        Assert.Contains("INPUT_VERSION: ${{ inputs.version }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_CHANNEL: ${{ inputs.channel }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_ORGANIZATION_ID: ${{ inputs.organization_id }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_ARTIFACT_STORE: ${{ inputs.artifact_store }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_HOSTING_ROOT: ${{ inputs.hosting_root }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_PUBLIC_BASE_URI: ${{ inputs.public_base_uri }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_S3_ENDPOINT: ${{ inputs.s3_endpoint }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_S3_BUCKET: ${{ inputs.s3_bucket }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_OPERATOR_ARTIFACT_UPLOAD_URI: ${{ inputs.operator_artifact_upload_uri }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_OPERATOR_ARTIFACT_PUBLIC_URI: ${{ inputs.operator_artifact_public_uri }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_GAMING_PC_ARTIFACT_UPLOAD_URI: ${{ inputs.gaming_pc_artifact_upload_uri }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_GAMING_PC_ARTIFACT_PUBLIC_URI: ${{ inputs.gaming_pc_artifact_public_uri }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_VERSION", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_CHANNEL", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_ORGANIZATION_ID", publishStep, StringComparison.Ordinal);
        Assert.Contains("-SigningKeyEnvVar', 'AFK4_UPDATE_SIGNING_KEY_PEM'", publishStep, StringComparison.Ordinal);
        Assert.Contains("-S3AccessKeyEnvVar', 'AFK4_UPDATE_ARTIFACTS_S3_ACCESS_KEY'", publishStep, StringComparison.Ordinal);
        Assert.Contains("-S3SecretKeyEnvVar', 'AFK4_UPDATE_ARTIFACTS_S3_SECRET_KEY'", publishStep, StringComparison.Ordinal);
        Assert.DoesNotContain("${{ inputs.", publishRunBlock, StringComparison.Ordinal);

        Assert.Contains("INPUT_PLATFORM_BASE_URL: ${{ inputs.platform_base_url }}", registrationStep, StringComparison.Ordinal);
        Assert.Contains("INPUT_BRANCH_ID: ${{ inputs.branch_id }}", registrationStep, StringComparison.Ordinal);
        Assert.Contains("'-PlatformBaseUrl', $env:INPUT_PLATFORM_BASE_URL", registrationStep, StringComparison.Ordinal);
        Assert.Contains("'-BranchId', $env:INPUT_BRANCH_ID", registrationStep, StringComparison.Ordinal);
        Assert.Contains("'-AccessTokenEnvVar', 'AFK4_UPDATE_REGISTRATION_TOKEN'", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-CreateRollouts", registrationStep, StringComparison.Ordinal);
        Assert.DoesNotContain("${{ inputs.", registrationRunBlock, StringComparison.Ordinal);

        var workflowWithoutAccessTokenEnvVar = workflow.Replace("-AccessTokenEnvVar", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("-AccessToken", workflowWithoutAccessTokenEnvVar, StringComparison.Ordinal);
    }

    [Fact]
    public void PrVerificationWorkflow_UsesCostAwareRequiredResultGate()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/pr-verification.yml")));

        Assert.Contains("name: PR Verification", workflow, StringComparison.Ordinal);
        Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
        Assert.Contains("- main", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("group: pr-verification-${{ github.workflow }}-${{ github.event.pull_request.number }}", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("paths-ignore:", workflow, StringComparison.Ordinal);

        Assert.Contains("changes:", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 5", workflow, StringComparison.Ordinal);
        Assert.Contains("run_windows: ${{ steps.filter.outputs.run_windows }}", workflow, StringComparison.Ordinal);
        Assert.Contains("git diff --name-only \"$base...$head\"", workflow, StringComparison.Ordinal);
        Assert.Contains(".config/dotnet-tools.json", workflow, StringComparison.Ordinal);
        Assert.Contains("run_windows=$($runWindows.ToString().ToLowerInvariant())", workflow, StringComparison.Ordinal);
        Assert.Contains("$env:GITHUB_OUTPUT", workflow, StringComparison.Ordinal);

        Assert.Contains("build-test-windows:", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ needs.changes.outputs.run_windows == 'true' }}", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 45", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore AFK4.sln", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);

        Assert.Contains("pr-verification-result:", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ always() }}", workflow, StringComparison.Ordinal);
        Assert.Contains("Windows build/test gate did not pass.", workflow, StringComparison.Ordinal);
        Assert.Contains("No Windows-relevant changes detected; skipping paid Windows runner.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageSmokeWorkflow_BuildsUnsignedMsiArtifactsWithShortRetention()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/package-smoke.yml")));

        Assert.Contains("name: Package Smoke", workflow, StringComparison.Ordinal);
        Assert.Contains("push:", workflow, StringComparison.Ordinal);
        Assert.Contains("- main", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 60", workflow, StringComparison.Ordinal);
        Assert.Contains("- \".config/dotnet-tools.json\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"src/AFK4.Shared.Contracts/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"src/AFK4.BuildingBlocks/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"NuGet.config\"", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_PACKAGE_VERSION=$version", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"scripts/publish-client-msi-updates.ps1\"", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/publish-staging-bootstrapper.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("LeaseSigningPublicKeyPath deploy/coolify/staging-session-signing-public.pem", workflow, StringComparison.Ordinal);
        Assert.Contains("UpdateSigningPublicKeyPath deploy/coolify/staging-update-signing-public.pem", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-operator-app-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-gaming-pc-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("afk4-gaming-pc-setup-$env:AFK4_PACKAGE_VERSION-internal.exe", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("$env:GITHUB_RUN_NUMBER-ci", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/publish-client-msi-updates.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("ArtifactStore s3", workflow, StringComparison.Ordinal);
        Assert.Contains("Publish staging bootstrapper to MinIO", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/bootstrapper", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/bootstrapper/*.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-package-smoke-bootstrapper-0.1.${{ github.run_number }}-internal", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_STAGING_UPDATE_STAFF_USERNAME", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_STAGING_UPDATE_STAFF_PASSWORD", workflow, StringComparison.Ordinal);
        Assert.Contains("/api/auth/staff/sign-in", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/register-update-package-requests.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("-CreateRollouts", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 3", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void CoolifyStagingDeployWorkflow_TriggersDeployAndFailsClosedForMigrations()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/coolify-staging-deploy.yml")));

        Assert.Contains("name: Coolify Staging Deploy", workflow, StringComparison.Ordinal);
        Assert.Contains("push:", workflow, StringComparison.Ordinal);
        Assert.Contains("- main", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("force_rebuild:", workflow, StringComparison.Ordinal);
        Assert.Contains("confirm_migrations_applied:", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("group: coolify-staging-deploy-${{ github.workflow }}-${{ github.ref }}", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 30", workflow, StringComparison.Ordinal);

        Assert.Contains("- \".github/workflows/coolify-staging-deploy.yml\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"deploy/coolify/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"src/AFK4.Platform.Api/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"src/AFK4.Shared.Contracts/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"src/AFK4.BuildingBlocks/**\"", workflow, StringComparison.Ordinal);

        Assert.Contains("Guard EF migrations", workflow, StringComparison.Ordinal);
        Assert.Contains("git diff --name-only \"$before...$head\"", workflow, StringComparison.Ordinal);
        Assert.Contains("src/AFK4.Platform.Api/Data/Migrations/", workflow, StringComparison.Ordinal);
        Assert.Contains("EF migration changes require explicit backup/migration run before Coolify deploy.", workflow, StringComparison.Ordinal);
        Assert.Contains("$env:CONFIRM_MIGRATIONS_APPLIED -ne \"true\"", workflow, StringComparison.Ordinal);

        Assert.Contains("COOLIFY_API_TOKEN: ${{ secrets.COOLIFY_API_TOKEN }}", workflow, StringComparison.Ordinal);
        Assert.Contains("COOLIFY_BASE_URL: ${{ vars.COOLIFY_BASE_URL }}", workflow, StringComparison.Ordinal);
        Assert.Contains("COOLIFY_STAGING_APP_UUID: ${{ vars.COOLIFY_STAGING_APP_UUID }}", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_STAGING_PLATFORM_BASE_URL: ${{ vars.AFK4_STAGING_PLATFORM_BASE_URL }}", workflow, StringComparison.Ordinal);
        Assert.Contains("/api/v1/deploy?uuid=", workflow, StringComparison.Ordinal);
        Assert.Contains("/api/v1/deployments/", workflow, StringComparison.Ordinal);
        Assert.Contains("/api/health", workflow, StringComparison.Ordinal);
        Assert.Contains("deployment_uuid=$deploymentUuid", workflow, StringComparison.Ordinal);
        Assert.Contains("@(\"finished\", \"success\", \"succeeded\")", workflow, StringComparison.Ordinal);
        Assert.Contains("@(\"failed\", \"error\", \"cancelled\", \"canceled\")", workflow, StringComparison.Ordinal);
        Assert.Contains("status=ok", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Write-Host $env:COOLIFY_API_TOKEN", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildClientPackagesScript_PublishesStagingGamingPcSetupExeWithEmbeddedMsi()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/build-client-packages.ps1")));

        Assert.Contains("StagingLeasePublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("StagingUpdateSigningPublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("AFK4.GamingPc.Setup/AFK4.GamingPc.Setup.csproj", script, StringComparison.Ordinal);
        Assert.Contains("GamingPcMsiPath=", script, StringComparison.Ordinal);
        Assert.Contains("$resolvedStagingLeasePublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("$resolvedStagingUpdateSigningPublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("Resolve-Path -LiteralPath $StagingLeasePublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("Resolve-Path -LiteralPath $StagingUpdateSigningPublicKeyPath", script, StringComparison.Ordinal);
        Assert.Contains("StagingLeasePublicKeyPath=", script, StringComparison.Ordinal);
        Assert.Contains("StagingUpdateSigningPublicKeyPath=", script, StringComparison.Ordinal);
        Assert.Contains("PublishSingleFile=true", script, StringComparison.Ordinal);
        Assert.Contains("SelfContained=true", script, StringComparison.Ordinal);
        Assert.Contains("afk4-gaming-pc-setup-$Version-$Channel.exe", script, StringComparison.Ordinal);
        Assert.Contains("agent-service", script, StringComparison.Ordinal);
        Assert.Contains("player-shell", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackagesWorkflow_UsesCostControlsForManualReleaseRuns()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml")));

        Assert.Contains("timeout-minutes: 90", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(workflow, "retention-days: 3"));
    }

    [Theory]
    [InlineData(".github/workflows/client-packages.yml")]
    [InlineData(".github/workflows/coolify-staging-deploy.yml")]
    [InlineData(".github/workflows/package-smoke.yml")]
    [InlineData(".github/workflows/pr-verification.yml")]
    public void GitHubActionsWorkflows_OptIntoNode24JavascriptActions(string workflowPath)
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(workflowPath)));

        Assert.Contains("env:\n  FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: true", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishClientMsiUpdates_InvokesPublisherForOperatorAgentAndPlayerShell()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        var operatorMsi = Path.Combine(packageDirectory, "afk4-operator-app-1.2.3-internal.msi");
        var gamingPcMsi = Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.3-internal.msi");
        File.WriteAllText(operatorMsi, "operator");
        File.WriteAllText(gamingPcMsi, "gaming-pc");
        var dotnetArgumentsPath = Path.Combine(tempRoot, "dotnet-args.log");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(dotnetArgumentsPath);
        var signingKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
        File.WriteAllText(signingKeyPath, "pem");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "1.2.3",
            "-Channel", "internal",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "file-system",
            "-HostingRoot", Path.Combine(tempRoot, "hosted"),
            "-PublicBaseUri", "https://updates.afk4.test/packages/",
            "-SigningKeyPath", signingKeyPath,
            "-ReleaseNotes", "Internal MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.Equal(0, result.ExitCode);
        var dotnetInvocations = File.ReadAllLines(dotnetArgumentsPath);
        Assert.Equal(3, dotnetInvocations.Length);
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|operator-app", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|agent-service", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|player-shell", StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--organization-id|0c04d6c0-bfa8-4e26-9263-fc0d307d0f08", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--artifact-store|file-system", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--hosting-root|" + Path.Combine(tempRoot, "hosted"), invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--public-base-uri|https://updates.afk4.test/packages/", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--signing-key|" + signingKeyPath, invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--release-notes|Internal MSI release.", invocation, StringComparison.Ordinal));
        Assert.Equal(1, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + operatorMsi, StringComparison.Ordinal)));
        Assert.Equal(2, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + gamingPcMsi, StringComparison.Ordinal)));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("operator-app-1.2.3-internal-request.json", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("agent-service-1.2.3-internal-request.json", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("player-shell-1.2.3-internal-request.json", StringComparison.Ordinal));
    }

    [Fact]
    public void PublishClientMsiUpdates_WithHttpPut_UsesArtifactSpecificUrisAndSigningKeyEnvironmentName()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        var operatorMsi = Path.Combine(packageDirectory, "afk4-operator-app-1.2.3-beta.msi");
        var gamingPcMsi = Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.3-beta.msi");
        File.WriteAllText(operatorMsi, "operator");
        File.WriteAllText(gamingPcMsi, "gaming-pc");
        var dotnetArgumentsPath = Path.Combine(tempRoot, "dotnet-http-put-args.log");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(dotnetArgumentsPath);

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "1.2.3",
            "-Channel", "beta",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "http-put",
            "-OperatorArtifactUploadUri", "https://upload.afk4.test/operator",
            "-OperatorArtifactPublicUri", "https://cdn.afk4.test/operator.msi",
            "-GamingPcArtifactUploadUri", "https://upload.afk4.test/gaming-pc",
            "-GamingPcArtifactPublicUri", "https://cdn.afk4.test/gaming-pc.msi",
            "-SigningKeyEnvVar", "AFK4_UPDATE_SIGNING_PRIVATE_KEY",
            "-ReleaseNotes", "Beta MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.Equal(0, result.ExitCode);
        var dotnetInvocations = File.ReadAllLines(dotnetArgumentsPath);
        Assert.Equal(3, dotnetInvocations.Length);
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--artifact-store|http-put", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--signing-key-env-var|AFK4_UPDATE_SIGNING_PRIVATE_KEY", invocation, StringComparison.Ordinal));
        Assert.DoesNotContain(dotnetInvocations, invocation => invocation.Contains("--signing-key|", StringComparison.Ordinal));

        var operatorInvocation = Assert.Single(dotnetInvocations, invocation => invocation.Contains("--component|operator-app", StringComparison.Ordinal));
        Assert.Contains("--artifact-upload-uri|https://upload.afk4.test/operator", operatorInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-public-uri|https://cdn.afk4.test/operator.msi", operatorInvocation, StringComparison.Ordinal);

        foreach (var component in new[] { "agent-service", "player-shell" })
        {
            var invocation = Assert.Single(dotnetInvocations, candidate => candidate.Contains("--component|" + component, StringComparison.Ordinal));
            Assert.Contains("--artifact-upload-uri|https://upload.afk4.test/gaming-pc", invocation, StringComparison.Ordinal);
            Assert.Contains("--artifact-public-uri|https://cdn.afk4.test/gaming-pc.msi", invocation, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishClientMsiUpdates_WithS3_UsesMinioArgumentsForAllComponents()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-operator-app-1.2.4-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.4-internal.msi"), "gaming-pc");
        var dotnetArgumentsPath = Path.Combine(tempRoot, "dotnet-s3-args.log");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(dotnetArgumentsPath);

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "1.2.4",
            "-Channel", "internal",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "s3",
            "-S3Endpoint", "https://updates.afk4.test",
            "-S3Bucket", "afk4-updates-staging",
            "-S3KeyPrefix", "client",
            "-S3AccessKeyEnvVar", "AFK4_STAGING_MINIO_ACCESS_KEY",
            "-S3SecretKeyEnvVar", "AFK4_STAGING_MINIO_SECRET_KEY",
            "-PublicBaseUri", "https://updates.afk4.test/afk4-updates-staging/",
            "-SigningKeyEnvVar", "AFK4_UPDATE_SIGNING_PRIVATE_KEY",
            "-ReleaseNotes", "Internal MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.Equal(0, result.ExitCode);
        var dotnetInvocations = File.ReadAllLines(dotnetArgumentsPath);
        Assert.Equal(3, dotnetInvocations.Length);
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--artifact-store|s3", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--s3-endpoint|https://updates.afk4.test/", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--s3-bucket|afk4-updates-staging", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--s3-key-prefix|client", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--s3-access-key-env-var|AFK4_STAGING_MINIO_ACCESS_KEY", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--s3-secret-key-env-var|AFK4_STAGING_MINIO_SECRET_KEY", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--public-base-uri|https://updates.afk4.test/afk4-updates-staging/", invocation, StringComparison.Ordinal));
    }

    [Fact]
    public void PublishClientMsiUpdates_WhenPublisherFails_ReturnsNonZeroWithComponentMessage()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-operator-app-1.2.3-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.3-internal.msi"), "gaming-pc");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(Path.Combine(tempRoot, "dotnet-failure-args.log"), exitCode: 23);
        var signingKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
        File.WriteAllText(signingKeyPath, "pem");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "1.2.3",
            "-Channel", "internal",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "file-system",
            "-HostingRoot", Path.Combine(tempRoot, "hosted"),
            "-PublicBaseUri", "https://updates.afk4.test/packages/",
            "-SigningKeyPath", signingKeyPath,
            "-ReleaseNotes", "Internal MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("AFK4.Update.Publisher failed for component", result.StandardError + result.StandardOutput);
    }

    [Fact]
    public void PublishClientMsiUpdates_WithUnsafeVersion_FailsBeforePublisher()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        var dotnetArgumentsPath = Path.Combine(tempRoot, "dotnet-unsafe-version-args.log");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(dotnetArgumentsPath);
        var signingKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
        File.WriteAllText(signingKeyPath, "pem");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "..\\1.2.3",
            "-Channel", "internal",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "file-system",
            "-HostingRoot", Path.Combine(tempRoot, "hosted"),
            "-PublicBaseUri", "https://updates.afk4.test/packages/",
            "-SigningKeyPath", signingKeyPath,
            "-ReleaseNotes", "Internal MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Version must be filename-safe", result.StandardError + result.StandardOutput);
        Assert.False(File.Exists(dotnetArgumentsPath));
    }

    [Fact]
    public void PublishClientMsiUpdates_WithRelativePublicBaseUri_FailsWithClearMessage()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-operator-app-1.2.3-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.3-internal.msi"), "gaming-pc");
        var dotnetArgumentsPath = Path.Combine(tempRoot, "dotnet-relative-uri-args.log");
        var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(dotnetArgumentsPath);
        var signingKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
        File.WriteAllText(signingKeyPath, "pem");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
            "-Version", "1.2.3",
            "-Channel", "internal",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-PackageDirectory", packageDirectory,
            "-OutputDirectory", outputDirectory,
            "-ArtifactStore", "file-system",
            "-HostingRoot", Path.Combine(tempRoot, "hosted"),
            "-PublicBaseUri", "packages/",
            "-SigningKeyPath", signingKeyPath,
            "-ReleaseNotes", "Internal MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("PublicBaseUri must be an absolute URI.", result.StandardError + result.StandardOutput);
        Assert.False(File.Exists(dotnetArgumentsPath));
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

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var startIndex = 0;

        while (true)
        {
            var index = value.IndexOf(needle, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            startIndex = index + needle.Length;
        }
    }

    private static string ExtractWorkflowStep(string workflow, string stepName)
    {
        var marker = "      - name: " + stepName;
        var start = workflow.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Workflow step '{stepName}' was not found.");

        var next = workflow.IndexOf("\n      - name: ", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? workflow[start..] : workflow[start..next];
    }

    private static string ExtractWorkflowRunBlock(string workflowStep)
    {
        const string marker = "        run: |";
        var start = workflowStep.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Workflow step run block was not found.");

        return workflowStep[start..];
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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

    private string CreateFakeDotnetThatRecordsArguments(string capturePath, int exitCode = 0)
    {
        var fakeDotnetPath = Path.Combine(tempRoot, $"fake-dotnet-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(
            fakeDotnetPath,
            "@echo off" + Environment.NewLine +
            "setlocal EnableDelayedExpansion" + Environment.NewLine +
            "set \"line=\"" + Environment.NewLine +
            ":capture" + Environment.NewLine +
            "if \"%~1\"==\"\" goto done" + Environment.NewLine +
            "if defined line (" + Environment.NewLine +
            "  set \"line=!line!^|%~1\"" + Environment.NewLine +
            ") else (" + Environment.NewLine +
            "  set \"line=%~1\"" + Environment.NewLine +
            ")" + Environment.NewLine +
            "shift" + Environment.NewLine +
            "goto capture" + Environment.NewLine +
            ":done" + Environment.NewLine +
            ">>\"" + capturePath + "\" echo(!line!" + Environment.NewLine +
            "exit /b " + exitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
        return fakeDotnetPath;
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

    private sealed record CapturedHttpRequest(string Method, string Path, string Authorization, string Body);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
