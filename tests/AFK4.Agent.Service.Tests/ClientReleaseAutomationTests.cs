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
        AssertParameter(ast, "OrganizationAdminArtifactUploadUri");
        AssertParameter(ast, "OrganizationAdminArtifactPublicUri");
        AssertParameter(ast, "AgentArtifactUploadUri");
        AssertParameter(ast, "AgentArtifactPublicUri");
        AssertParameter(ast, "PlayerShellArtifactUploadUri");
        AssertParameter(ast, "PlayerShellArtifactPublicUri");
        AssertParameter(ast, "S3Endpoint");
        AssertParameter(ast, "S3Bucket");
        AssertParameter(ast, "S3KeyPrefix");
        AssertParameter(ast, "S3AccessKeyEnvVar");
        AssertParameter(ast, "S3SecretKeyEnvVar");
        AssertParameter(ast, "S3Region");
        AssertParameter(ast, "OrganizationAdminStableAliasObjectKey");
        AssertParameter(ast, "SigningKeyPath");
        AssertParameter(ast, "SigningKeyEnvVar");
        AssertParameter(ast, "ReleaseNotes");
        AssertParameter(ast, "DotnetPath");
        AssertParameter(ast, "PublishMaxAttempts");
    }

    [Fact]
    public void RegisterUpdatePackageRequestsScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/register-update-package-requests.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "PlatformBaseUrl");
        AssertParameter(ast, "OrganizationId");
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

    [WindowsOnlyFact]
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
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
        Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequest.Path);
        Assert.Equal("Bearer test-token", capturedRequest.Authorization);
        Assert.Equal(requestBody, capturedRequest.Body);
    }

    [WindowsOnlyFact]
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
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
        Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequests[0].Path);
        Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts", capturedRequests[1].Path);
        Assert.Contains("\"updatePackageId\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"targetKind\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"device\"", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("0588fb59-3edb-4704-bbdb-094e12417cf1", capturedRequests[1].Body, StringComparison.Ordinal);
    }

    [WindowsOnlyFact]
    public async Task RegisterUpdatePackageRequests_WithBranchRollout_PostsRolloutWithoutDeviceTargets()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "organization-admin-1.2.3-internal-request.json");
        const string requestBody = """{"organizationId":"0c04d6c0-bfa8-4e26-9263-fc0d307d0f08","component":"organization-admin","channel":"internal"}""";
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token",
            "-CreateRollouts",
            "-RolloutComponent", "organization-admin",
            "-RolloutTargetKind", "branch",
            "-RolloutReason", "Automated smoke operator rollout.");

        if (result.ExitCode != 0)
        {
            listener.Stop();
        }

        Assert.Equal(0, result.ExitCode);
        var capturedRequests = await capturedRequestsTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequests[0].Path);
        Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts", capturedRequests[1].Path);
        Assert.Contains("\"targetKind\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"branch\"", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"targetDeviceIds\":", capturedRequests[1].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("0588fb59-3edb-4704-bbdb-094e12417cf1", capturedRequests[1].Body, StringComparison.Ordinal);
    }

    [WindowsOnlyTheory]
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
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

    [WindowsOnlyFact]
    public void RegisterUpdatePackageRequests_WithoutAccessTokenSource_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "http://127.0.0.1:9",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one access token source", result.StandardError + result.StandardOutput);
    }

    [WindowsOnlyFact]
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token",
            "-AccessTokenEnvVar", "AFK4_TEST_REGISTRATION_TOKEN");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one access token source", result.StandardError + result.StandardOutput);
    }

    [WindowsOnlyFact]
    public async Task RegisterUpdatePackageRequests_WithRequestDirectory_PostsRequestJsonInNameOrder()
    {
        var requestDirectory = Path.Combine(tempRoot, "requests");
        Directory.CreateDirectory(requestDirectory);
        var firstBody = """{"component":"agent-service","order":1}""";
        var secondBody = """{"component":"organization-admin","order":2}""";
        var thirdBody = """{"component":"player-shell","order":3}""";
        await File.WriteAllTextAsync(Path.Combine(requestDirectory, "02-organization-admin-request.json"), secondBody);
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
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
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
        Assert.All(capturedRequests, request => Assert.Equal("/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", request.Path));
        Assert.All(capturedRequests, request => Assert.Equal("Bearer test-token", request.Authorization));
    }

    [WindowsOnlyFact]
    public void RegisterUpdatePackageRequests_WithNonRequestJsonPath_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "http://127.0.0.1:9",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
            "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
            "-RequestPath", requestPath,
            "-AccessToken", "test-token");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("RequestPath must reference *-request.json files.", result.StandardError + result.StandardOutput);
    }

    [WindowsOnlyFact]
    public void RegisterUpdatePackageRequests_WithNonHttpPlatformBaseUrl_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
        File.WriteAllText(requestPath, """{"component":"agent-service"}""");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
            "-PlatformBaseUrl", "file:///tmp/afk4",
            "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
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
        Assert.Contains("http_put_artifact_uris_json:", workflow, StringComparison.Ordinal);
        Assert.Contains("Stable releases require signing and update metadata publishing.", workflow, StringComparison.Ordinal);
        Assert.Contains("Backend registration requires publish_update_metadata=true.", workflow, StringComparison.Ordinal);
        Assert.Contains("http_put_artifact_uris_json is required when artifact_store=http-put.", workflow, StringComparison.Ordinal);
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
        Assert.Contains("INPUT_HTTP_PUT_ARTIFACT_URIS_JSON: ${{ inputs.http_put_artifact_uris_json }}", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_VERSION", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_CHANNEL", publishStep, StringComparison.Ordinal);
        Assert.Contains("$env:INPUT_ORGANIZATION_ID", publishStep, StringComparison.Ordinal);
        Assert.Contains("-SigningKeyEnvVar', 'AFK4_UPDATE_SIGNING_KEY_PEM'", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.operator.uploadUri", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.operator.publicUri", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.agent.uploadUri", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.agent.publicUri", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.playerShell.uploadUri", publishStep, StringComparison.Ordinal);
        Assert.Contains("$httpPutUris.playerShell.publicUri", publishStep, StringComparison.Ordinal);
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
    public void ClientPackagesWorkflow_StaysUnderGitHubDispatchInputLimit()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml")));
        var inputsStart = workflow.IndexOf("    inputs:\n", StringComparison.Ordinal);
        Assert.True(inputsStart >= 0, "workflow_dispatch inputs block was not found.");
        var permissionsStart = workflow.IndexOf("\npermissions:", inputsStart, StringComparison.Ordinal);
        Assert.True(permissionsStart > inputsStart, "permissions block was not found after workflow_dispatch inputs.");

        var inputsBlock = workflow[inputsStart..permissionsStart];
        var inputCount = inputsBlock
            .Split('\n')
            .Count(line =>
                line.StartsWith("      ", StringComparison.Ordinal) &&
                !line.StartsWith("        ", StringComparison.Ordinal) &&
                line.TrimEnd().EndsWith(":", StringComparison.Ordinal));

        Assert.True(inputCount <= 25, $"workflow_dispatch supports at most 25 top-level inputs, but client-packages.yml defines {inputCount}.");
    }

    [Fact]
    public void PrVerificationWorkflow_UsesCostAwareRequiredResultGate()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/pr-verification.yml")));

        Assert.Contains("name: PR Verification", workflow, StringComparison.Ordinal);
        Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
        // No branch filter anywhere: work that reaches main through a design branch has to be
        // verified on the way in, and a filter here is what used to let it through unchecked.
        Assert.DoesNotContain("branches:", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("group: pr-verification-${{ github.workflow }}-${{ github.event.pull_request.number }}", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("paths-ignore:", workflow, StringComparison.Ordinal);

        Assert.Contains("changes:", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 5", workflow, StringComparison.Ordinal);
        Assert.Contains("run_dotnet: ${{ steps.filter.outputs.run_dotnet }}", workflow, StringComparison.Ordinal);
        Assert.Contains("git diff --name-only \"$base...$head\"", workflow, StringComparison.Ordinal);
        Assert.Contains(".config/dotnet-tools.json", workflow, StringComparison.Ordinal);
        Assert.Contains("run_dotnet=$($runDotnet.ToString().ToLowerInvariant())", workflow, StringComparison.Ordinal);
        Assert.Contains("$env:GITHUB_OUTPUT", workflow, StringComparison.Ordinal);

        Assert.Contains("build-test-windows:", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ needs.changes.outputs.run_dotnet == 'true' }}", workflow, StringComparison.Ordinal);
        Assert.Contains("runs-on: windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("timeout-minutes: 45", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore AFK4.sln", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);

        // Postgres-backed tests skip themselves without a database, and GitHub service containers
        // are Linux-only — so this job is the only place they actually run.
        Assert.Contains("test-postgres:", workflow, StringComparison.Ordinal);
        Assert.Contains("image: postgres:16", workflow, StringComparison.Ordinal);
        Assert.Contains("--health-cmd pg_isready", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_REQUIRE_POSTGRES_TESTS: '1'", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test tests/AFK4.Platform.Api.Tests -p:NuGetAudit=false -v minimal", workflow, StringComparison.Ordinal);
        foreach (var variable in new[]
                 {
                     "AFK4_POS_POSTGRES_TEST_CONNECTION_STRING",
                     "AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING",
                     "AFK4_COMMERCE_TEST_POSTGRES",
                     "AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING"
                 })
        {
            // The database name has to end in _test; every attribute refuses anything else.
            Assert.Contains($"{variable}: Host=127.0.0.1;Port=5432;Database=afk4_ci_test;", workflow, StringComparison.Ordinal);
        }

        Assert.Contains("pr-verification-result:", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ always() }}", workflow, StringComparison.Ordinal);
        Assert.Contains("Windows build/test gate did not pass.", workflow, StringComparison.Ordinal);
        Assert.Contains("PostgreSQL test gate did not pass.", workflow, StringComparison.Ordinal);
        Assert.Contains("No .NET-relevant changes detected; skipping the paid Windows runner and the PostgreSQL job.", workflow, StringComparison.Ordinal);

        // The Flutter gate has to be wired the same way as the others: a job, a change filter that
        // includes the string catalog it generates from, and a throw in the result gate. A job that
        // nothing depends on is a job nobody notices failing.
        Assert.Contains("build-test-flutter:", workflow, StringComparison.Ordinal);
        Assert.Contains("run_flutter: ${{ steps.filter.outputs.run_flutter }}", workflow, StringComparison.Ordinal);
        Assert.Contains("\"src/afk4_customer_app/\"", workflow, StringComparison.Ordinal);
        // ARB files are generated from locales/*.json, so a catalog edit must retrigger Flutter.
        Assert.Contains("\"locales/\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- build-test-flutter", workflow, StringComparison.Ordinal);
        Assert.Contains("Flutter build/test gate did not pass.", workflow, StringComparison.Ordinal);
        // Regenerating and diffing is what stops a stale translation from shipping to a store.
        Assert.Contains("git diff --exit-code -- locales src/afk4_customer_app/lib/l10n packages/i18n/src", workflow, StringComparison.Ordinal);
        Assert.Contains("flutter analyze", workflow, StringComparison.Ordinal);
        Assert.Contains("flutter test", workflow, StringComparison.Ordinal);
        // integration_test/ is skipped by a plain `flutter test`, and without -d the tool looks
        // for a connected device the runner does not have. Either omission silently drops the
        // only test that covers the whole journey.
        Assert.Contains("flutter test integration_test -d flutter-tester", workflow, StringComparison.Ordinal);
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
        Assert.Contains("- \"src/AFK4.OrganizationAdmin.Web/**\"", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"NuGet.config\"", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-node@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("node-version: 24", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: oven-sh/setup-bun@v2", workflow, StringComparison.Ordinal);
        Assert.Contains("bun-version: 1.3.14", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_PACKAGE_VERSION=$version", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"scripts/publish-client-msi-updates.ps1\"", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-organization-admin-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-agent-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.Contains("afk4-player-shell-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("scripts/publish-staging-bootstrapper.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("LeaseSigningPublicKeyPath deploy/coolify/staging-session-signing-public.pem", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSigningPublicKeyPath deploy/coolify/staging-update-signing-public.pem", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("afk4-gaming-pc-$env:AFK4_PACKAGE_VERSION-internal.msi", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("afk4-gaming-pc-setup-$env:AFK4_PACKAGE_VERSION-internal.exe", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("$env:GITHUB_RUN_NUMBER-ci", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/publish-client-msi-updates.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("- \"scripts/register-update-package-requests.ps1\"", workflow, StringComparison.Ordinal);
        Assert.Contains("ArtifactStore s3", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Publish staging bootstrapper to MinIO", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts/bootstrapper", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("afk4-package-smoke-bootstrapper-0.1.${{ github.run_number }}-internal", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_STAGING_UPDATE_STAFF_USERNAME", workflow, StringComparison.Ordinal);
        Assert.Contains("AFK4_STAGING_UPDATE_STAFF_PASSWORD", workflow, StringComparison.Ordinal);
        Assert.Contains("/api/organizations/$($env:AFK4_STAGING_ORGANIZATION_ID)/auth/staff/sign-in", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/register-update-package-requests.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("-CreateRollouts", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 3", workflow, StringComparison.Ordinal);
        Assert.Contains("-OrganizationAdminStableAliasObjectKey organization-admin/internal/latest/afk4-organization-admin-internal.msi", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Verify stable Organization Admin installer", workflow, StringComparison.Ordinal);
        Assert.Contains("organization_admin_upgrade_required", workflow, StringComparison.Ordinal);
        Assert.Contains("downloadUrl", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("COOLIFY_API_TOKEN", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/applications/", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/deploy", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageSmokeWorkflow_CreatesAgentDeviceAndOperatorBranchRollouts()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/package-smoke.yml")));
        var registrationStep = ExtractWorkflowStep(workflow, "Register staging update packages and create rollouts");

        Assert.Contains("organization-admin-$env:AFK4_PACKAGE_VERSION-internal-request.json", registrationStep, StringComparison.Ordinal);
        Assert.Contains("agent-service-$env:AFK4_PACKAGE_VERSION-internal-request.json", registrationStep, StringComparison.Ordinal);
        Assert.Contains("player-shell-$env:AFK4_PACKAGE_VERSION-internal-request.json", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-RolloutComponent organization-admin", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-RolloutTargetKind branch", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-RolloutComponent agent-service", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-RolloutTargetKind device", registrationStep, StringComparison.Ordinal);
        Assert.Contains("-RolloutTargetDeviceId $env:AFK4_STAGING_UPDATE_TARGET_DEVICE_ID", registrationStep, StringComparison.Ordinal);
    }

    [Fact]
    public void CoolifyStagingDeployWorkflow_TriggersDeployAndReportsMigrations()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/coolify-staging-deploy.yml")));

        Assert.Contains("name: Coolify Staging Deploy", workflow, StringComparison.Ordinal);
        Assert.Contains("push:", workflow, StringComparison.Ordinal);
        Assert.Contains("- main", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("force_rebuild:", workflow, StringComparison.Ordinal);
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

        // Ручное подтверждение убрано намеренно: миграции применяет pre-deployment command
        // контейнера, а блокирующий гард ровно один раз оставил staging на старой сборке
        // на две недели — про ручной шаг забыли, а падение выглядело как здоровый сервис.
        Assert.Contains("Report EF migrations in this deploy", workflow, StringComparison.Ordinal);
        Assert.Contains("git diff --name-only \"$before...$head\"", workflow, StringComparison.Ordinal);
        Assert.Contains("src/AFK4.Platform.Api/Data/Migrations/", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm_migrations_applied", workflow, StringComparison.Ordinal);

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
    public void BuildClientPackagesScript_MapsChannelToPlatformBaseUrl()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/build-client-packages.ps1")));

        Assert.Contains("$platformBaseUrlByChannel = @{", script, StringComparison.Ordinal);
        Assert.Contains("'internal' = 'https://afk4.staging.mubi.dev'", script, StringComparison.Ordinal);
        Assert.Contains("'beta' = 'https://afk4.staging.mubi.dev'", script, StringComparison.Ordinal);
        Assert.Contains("'stable' = 'https://app.afk4.net'", script, StringComparison.Ordinal);
        Assert.Contains("$platformBaseUrl = $platformBaseUrlByChannel[$Channel]", script, StringComparison.Ordinal);
        Assert.Contains("No platform base URL is configured for channel", script, StringComparison.Ordinal);
        Assert.Contains("-p:AFK4PlatformBaseUrl=\"$platformBaseUrl\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildClientPackagesScript_CarriesRuntimeInBundleExeAndMovesAgentMsiToIntermediates()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/build-client-packages.ps1")));

        // Runtime pin lives next to the build-time download/verify.
        Assert.Contains("$runtimeVersion = '10.0.9'", script, StringComparison.Ordinal);
        // URL is built from $runtimeVersion (single source of truth) — not a hardcoded version,
        // so bumping the pin can't silently leave the URL on the old runtime.
        Assert.Contains("windowsdesktop-runtime-$runtimeVersion-win-x64.exe", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -Algorithm SHA512", script, StringComparison.Ordinal);
        Assert.Contains("Runtime installer SHA-512 mismatch", script, StringComparison.Ordinal);

        // The WiX BootstrapperApplications (the v7 rename of Bal) + Netfx extensions are required
        // for the bundle, and `wix extension add` must accept the v7 OSMF EULA non-interactively.
        Assert.Contains("wix extension add -acceptEula wix7", script, StringComparison.Ordinal);
        Assert.Contains("WixToolset.BootstrapperApplications.wixext", script, StringComparison.Ordinal);
        Assert.Contains("WixToolset.Netfx.wixext", script, StringComparison.Ordinal);

        // The bundle is built from the single Bundle.wxs and carries the runtime + agent MSI.
        // Bundle.wxs references $(var.RuntimeVersion), $(var.RuntimeInstallerPath), $(var.AgentMsiPath),
        // so the build must -d all three or `wix build` fails with an undefined preprocessor variable.
        Assert.Contains("installers/bundle/Bundle.wxs", script, StringComparison.Ordinal);
        Assert.Contains("RuntimeVersion=$runtimeVersion", script, StringComparison.Ordinal);
        Assert.Contains("RuntimeInstallerPath=", script, StringComparison.Ordinal);
        Assert.Contains("AgentMsiPath=", script, StringComparison.Ordinal);
        Assert.Contains("afk4-client-$Version-$Channel.exe", script, StringComparison.Ordinal);

        // The bundle is the deliverable; the agent MSI becomes a build input in intermediates\.
        var bundleIndex = script.IndexOf("afk4-client-$Version-$Channel.exe", StringComparison.Ordinal);
        var agentToIntermediatesIndex = script.IndexOf("# The agent MSI is now a build input to the bundle", StringComparison.Ordinal);
        Assert.True(agentToIntermediatesIndex > bundleIndex, "Agent MSI must be moved to intermediates only after the bundle that embeds it is built.");
    }

    [Fact]
    public void BuildClientPackagesScript_BrandsBundleAndBuildsBAFunctionsForZeroClickInstall()
    {
        var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/build-client-packages.ps1")));

        // The native BAFunctions DLL (auto-start install + auto-close) must be built before the
        // bundle and located via vswhere/MSBuild so it works wherever the C++ toolset is installed.
        Assert.Contains("AFK4.BAFunctions.vcxproj", script, StringComparison.Ordinal);
        Assert.Contains("vswhere.exe", script, StringComparison.Ordinal);
        Assert.Contains("-restore -p:Configuration=Release -p:Platform=x64", script, StringComparison.Ordinal);

        // Bundle.wxs references $(var.BAFunctionsPath)/$(var.BrandIconPath)/$(var.BrandLogoPath),
        // so the build must -d all three or `wix build` fails with an undefined preprocessor variable.
        Assert.Contains("BAFunctionsPath=", script, StringComparison.Ordinal);
        Assert.Contains("BrandIconPath=", script, StringComparison.Ordinal);
        Assert.Contains("BrandLogoPath=", script, StringComparison.Ordinal);

        // The DLL is built before the bundle consumes it as a payload.
        var msbuildIndex = script.IndexOf("AFK4.BAFunctions.vcxproj", StringComparison.Ordinal);
        var bundleConsumesIndex = script.IndexOf("BAFunctionsPath=$baFunctionsPath", StringComparison.Ordinal);
        Assert.True(msbuildIndex >= 0 && bundleConsumesIndex > msbuildIndex, "BAFunctions DLL must be built before the bundle that embeds it as a payload.");
    }

    [Fact]
    public void BundleWxs_BrandsTheInstallerAndLoadsBAFunctions()
    {
        var bundle = NormalizeLineEndings(File.ReadAllText(ScriptPath("installers/bundle/Bundle.wxs")));

        // Brand the .exe icon and the bootstrapper window logo.
        Assert.Contains("IconSourceFile=\"$(var.BrandIconPath)\"", bundle, StringComparison.Ordinal);
        Assert.Contains("LogoFile=\"$(var.BrandLogoPath)\"", bundle, StringComparison.Ordinal);

        // The BAFunctions DLL is loaded as a BootstrapperApplication payload that drives the
        // zero-click experience (skip Install button, auto-close on success).
        Assert.Contains("SourceFile=\"$(var.BAFunctionsPath)\"", bundle, StringComparison.Ordinal);
        Assert.Contains("bal:BAFunctions=\"yes\"", bundle, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackagesWorkflow_UsesCostControlsForManualReleaseRuns()
    {
        var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml")));

        Assert.Contains("timeout-minutes: 90", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/setup-node@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("node-version: 24", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: oven-sh/setup-bun@v2", workflow, StringComparison.Ordinal);
        Assert.Contains("bun-version: 1.3.14", workflow, StringComparison.Ordinal);
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

    [WindowsOnlyFact]
    public void PublishClientMsiUpdates_InvokesPublisherForOperatorAgentAndPlayerShell()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        var organizationAdminMsi = Path.Combine(packageDirectory, "afk4-organization-admin-1.2.3-internal.msi");
        var agentMsi = Path.Combine(packageDirectory, "afk4-agent-1.2.3-internal.msi");
        var playerShellMsi = Path.Combine(packageDirectory, "afk4-player-shell-1.2.3-internal.msi");
        File.WriteAllText(organizationAdminMsi, "operator");
        File.WriteAllText(agentMsi, "agent");
        File.WriteAllText(playerShellMsi, "player-shell");
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
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|organization-admin", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|agent-service", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|player-shell", StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--organization-id|0c04d6c0-bfa8-4e26-9263-fc0d307d0f08", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--artifact-store|file-system", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--hosting-root|" + Path.Combine(tempRoot, "hosted"), invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--public-base-uri|https://updates.afk4.test/packages/", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--signing-key|" + signingKeyPath, invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--release-notes|Internal MSI release.", invocation, StringComparison.Ordinal));
        Assert.Equal(1, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + organizationAdminMsi, StringComparison.Ordinal)));
        Assert.Equal(1, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + agentMsi, StringComparison.Ordinal)));
        Assert.Equal(1, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + playerShellMsi, StringComparison.Ordinal)));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("organization-admin-1.2.3-internal-request.json", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("agent-service-1.2.3-internal-request.json", StringComparison.Ordinal));
        Assert.Contains(dotnetInvocations, invocation => invocation.Contains("player-shell-1.2.3-internal-request.json", StringComparison.Ordinal));
    }

    [WindowsOnlyFact]
    public void PublishClientMsiUpdates_WithHttpPut_UsesArtifactSpecificUrisAndSigningKeyEnvironmentName()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        var organizationAdminMsi = Path.Combine(packageDirectory, "afk4-organization-admin-1.2.3-beta.msi");
        var agentMsi = Path.Combine(packageDirectory, "afk4-agent-1.2.3-beta.msi");
        var playerShellMsi = Path.Combine(packageDirectory, "afk4-player-shell-1.2.3-beta.msi");
        File.WriteAllText(organizationAdminMsi, "operator");
        File.WriteAllText(agentMsi, "agent");
        File.WriteAllText(playerShellMsi, "player-shell");
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
            "-OrganizationAdminArtifactUploadUri", "https://upload.afk4.test/operator",
            "-OrganizationAdminArtifactPublicUri", "https://cdn.afk4.test/operator.msi",
            "-AgentArtifactUploadUri", "https://upload.afk4.test/agent",
            "-AgentArtifactPublicUri", "https://cdn.afk4.test/agent.msi",
            "-PlayerShellArtifactUploadUri", "https://upload.afk4.test/player-shell",
            "-PlayerShellArtifactPublicUri", "https://cdn.afk4.test/player-shell.msi",
            "-SigningKeyEnvVar", "AFK4_UPDATE_SIGNING_PRIVATE_KEY",
            "-ReleaseNotes", "Beta MSI release.",
            "-DotnetPath", fakeDotnetPath);

        Assert.Equal(0, result.ExitCode);
        var dotnetInvocations = File.ReadAllLines(dotnetArgumentsPath);
        Assert.Equal(3, dotnetInvocations.Length);
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--artifact-store|http-put", invocation, StringComparison.Ordinal));
        Assert.All(dotnetInvocations, invocation => Assert.Contains("--signing-key-env-var|AFK4_UPDATE_SIGNING_PRIVATE_KEY", invocation, StringComparison.Ordinal));
        Assert.DoesNotContain(dotnetInvocations, invocation => invocation.Contains("--signing-key|", StringComparison.Ordinal));

        var organizationAdminInvocation = Assert.Single(dotnetInvocations, invocation => invocation.Contains("--component|organization-admin", StringComparison.Ordinal));
        Assert.Contains("--artifact-upload-uri|https://upload.afk4.test/operator", organizationAdminInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-public-uri|https://cdn.afk4.test/operator.msi", organizationAdminInvocation, StringComparison.Ordinal);

        var agentInvocation = Assert.Single(dotnetInvocations, candidate => candidate.Contains("--component|agent-service", StringComparison.Ordinal));
        Assert.Contains("--artifact|" + agentMsi, agentInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-upload-uri|https://upload.afk4.test/agent", agentInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-public-uri|https://cdn.afk4.test/agent.msi", agentInvocation, StringComparison.Ordinal);

        var playerShellInvocation = Assert.Single(dotnetInvocations, candidate => candidate.Contains("--component|player-shell", StringComparison.Ordinal));
        Assert.Contains("--artifact|" + playerShellMsi, playerShellInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-upload-uri|https://upload.afk4.test/player-shell", playerShellInvocation, StringComparison.Ordinal);
        Assert.Contains("--artifact-public-uri|https://cdn.afk4.test/player-shell.msi", playerShellInvocation, StringComparison.Ordinal);
    }

    [WindowsOnlyFact]
    public void PublishClientMsiUpdates_WithS3_UsesMinioArgumentsForAllComponents()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-organization-admin-1.2.4-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-agent-1.2.4-internal.msi"), "agent");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-player-shell-1.2.4-internal.msi"), "player-shell");
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
            "-OrganizationAdminStableAliasObjectKey", "organization-admin/internal/latest/afk4-organization-admin-internal.msi",
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
        var organizationAdminInvocation = Assert.Single(dotnetInvocations, invocation => invocation.Contains("--component|organization-admin", StringComparison.Ordinal));
        Assert.Contains("--s3-stable-alias-object-key|organization-admin/internal/latest/afk4-organization-admin-internal.msi", organizationAdminInvocation, StringComparison.Ordinal);
        Assert.DoesNotContain(
            dotnetInvocations.Where(invocation => !invocation.Contains("--component|organization-admin", StringComparison.Ordinal)),
            invocation => invocation.Contains("--s3-stable-alias-object-key", StringComparison.Ordinal));
    }

    [WindowsOnlyFact]
    public void PublishClientMsiUpdates_WhenPublisherFails_ReturnsNonZeroWithComponentMessage()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-organization-admin-1.2.3-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-agent-1.2.3-internal.msi"), "agent");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-player-shell-1.2.3-internal.msi"), "player-shell");
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

    [WindowsOnlyFact]
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

    [WindowsOnlyFact]
    public void PublishClientMsiUpdates_WithRelativePublicBaseUri_FailsWithClearMessage()
    {
        Directory.CreateDirectory(tempRoot);
        var packageDirectory = Path.Combine(tempRoot, "client-packages");
        var outputDirectory = Path.Combine(tempRoot, "update-packages");
        Directory.CreateDirectory(packageDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-organization-admin-1.2.3-internal.msi"), "operator");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-agent-1.2.3-internal.msi"), "agent");
        File.WriteAllText(Path.Combine(packageDirectory, "afk4-player-shell-1.2.3-internal.msi"), "player-shell");
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

    [WindowsOnlyFact]
    public void SignClientPackages_WithPfxSource_InvokesSigntoolForExplicitPackage()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-organization-admin-1.2.3-internal.msi");
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

    [WindowsOnlyFact]
    public void SignClientPackages_WithCertificateStoreSource_InvokesSigntoolWithStoreArguments()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-agent-1.2.3-internal.msi");
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

    [WindowsOnlyFact]
    public void SignClientPackages_WhenSigntoolReturnsNonZero_FailsPackage()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-organization-admin-1.2.3-internal.msi");
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

    [WindowsOnlyFact]
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

    [WindowsOnlyFact]
    public void SignClientPackages_WithCertificatePathDirectory_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-organization-admin-1.2.3-internal.msi");
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

    [WindowsOnlyFact]
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

    [WindowsOnlyFact]
    public void SignClientPackages_WithoutExactlyOneSigningSource_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-organization-admin-1.2.3-internal.msi");
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
