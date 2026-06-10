# Phase A: SMS Infrastructure (payom.tj) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an SMS sending capability for payom.tj that plugs into the existing notification pipeline as a new `INotificationChannel`, so later phases can send OTP/reset messages via `INotificationService`.

**Architecture:** Mirror the existing email stack exactly: `ISmsTransport` + `PayomSmsTransport` (HTTP, like the SMTP `ISmtpTransport`/`MailKitSmtpTransport` pair and the typed `DcGateAdminClient`), and `SmsChannel : INotificationChannel` (`Channel => Sms`) that reads a notification outbox row and delegates to the transport (like `SmtpEmailChannel`). The dispatcher already routes rows whose `Channel == "Sms"` to any registered SMS channel, so no dispatcher/service changes are needed.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, `IHttpClientFactory` typed clients, `IOptions<T>` config, xUnit (`tests/AFK4.Platform.Api.Tests`).

**Scope note:** Phase A delivers the transport + channel + config + tests only. OTP/reset **templates** (`NotificationTemplateKeys` + embedded JSON) and actual OTP sending are **Phase B** — adding template keys here would trip the startup check `ITemplateProvider.EnsureKeysPresent(NotificationTemplateKeys.All)` before any template files exist.

---

## File structure

- **Create** `src/AFK4.Platform.Api/Notifications/SmsOptions.cs` — options POCO (`"Sms"` section) + `SmsClientRegistration` HttpClient-name constant.
- **Create** `src/AFK4.Platform.Api/Notifications/ISmsTransport.cs` — transport interface + `SmsMessage` record + `SmsTransportException`.
- **Create** `src/AFK4.Platform.Api/Notifications/PayomSmsTransport.cs` — HTTP implementation against payom.tj.
- **Create** `src/AFK4.Platform.Api/Notifications/SmsChannel.cs` — `INotificationChannel` for SMS.
- **Modify** `src/AFK4.Platform.Api/Program.cs` — DI registration (after the notification block, ~line 216).
- **Modify** `src/AFK4.Platform.Api/appsettings.json` — add the `"Sms"` section.
- **Create** `tests/AFK4.Platform.Api.Tests/Notifications/PayomSmsTransportTests.cs`.
- **Create** `tests/AFK4.Platform.Api.Tests/Notifications/SmsChannelTests.cs`.

Reference (do not modify): `Notifications/ISmtpTransport.cs`, `MailKitSmtpTransport.cs`, `SmtpEmailChannel.cs`, `INotificationChannel.cs`, `ChannelResult.cs`, `Data/NotificationOutboxEntity.cs`, `Payments/DcGate/DcGateAdminClient.cs`, `tests/.../Notifications/SmtpEmailChannelTests.cs`.

---

### Task 1: SMS transport (`ISmsTransport` + `PayomSmsTransport`)

**Files:**
- Create: `src/AFK4.Platform.Api/Notifications/ISmsTransport.cs`
- Create: `src/AFK4.Platform.Api/Notifications/PayomSmsTransport.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Notifications/PayomSmsTransportTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Notifications/PayomSmsTransportTests.cs`:

```csharp
using System.Net;
using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class PayomSmsTransportTests
{
    [Fact]
    public async Task SendAsync_PostsExpectedRequest()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"deliveryStatus\":\"ACCEPTED\"}"),
        });
        var transport = CreateTransport(handler);

        await transport.SendAsync(new SmsMessage("+992937380070", "код 123456"), CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://gateway.payom.tj/api/message", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer test-token", handler.AuthorizationRaw);
        Assert.Contains("\"telephone\":\"+992937380070\"", handler.Body);
        Assert.Contains("\"senderName\":\"AFK4.NET\"", handler.Body);
        Assert.Contains("\"type\":\"SMS\"", handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData((HttpStatusCode)429, false)]
    public async Task SendAsync_MapsStatusToPermanence(HttpStatusCode status, bool expectedPermanent)
    {
        var handler = new CapturingHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent("error"),
        });
        var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(new SmsMessage("+992900000000", "x"), CancellationToken.None));
        Assert.Equal(expectedPermanent, exception.IsPermanent);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransient()
    {
        var transport = CreateTransport(new ThrowingHandler(new HttpRequestException("boom")));

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(new SmsMessage("+992900000000", "x"), CancellationToken.None));
        Assert.False(exception.IsPermanent);
    }

    private static PayomSmsTransport CreateTransport(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gateway.payom.tj") },
            apiToken: "test-token",
            senderName: "AFK4.NET");

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationRaw { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            AuthorizationRaw = request.Headers.TryGetValues("Authorization", out var values)
                ? string.Join(",", values)
                : null;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw exception;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PayomSmsTransportTests`
Expected: FAIL — compile error, `ISmsTransport`/`SmsMessage`/`SmsTransportException`/`PayomSmsTransport` do not exist.

- [ ] **Step 3: Create the interface + records**

Create `src/AFK4.Platform.Api/Notifications/ISmsTransport.cs`:

```csharp
namespace AFK4.Platform.Api.Notifications;

public interface ISmsTransport
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public sealed record SmsMessage(string ToPhoneNumber, string Text);

public sealed class SmsTransportException(bool isPermanent, string message) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}
```

- [ ] **Step 4: Create the transport implementation**

Create `src/AFK4.Platform.Api/Notifications/PayomSmsTransport.cs`:

```csharp
using System.Net.Http.Json;

namespace AFK4.Platform.Api.Notifications;

public sealed class PayomSmsTransport : ISmsTransport
{
    private readonly HttpClient httpClient;
    private readonly string apiToken;
    private readonly string senderName;

    public PayomSmsTransport(HttpClient httpClient, string apiToken, string senderName)
    {
        this.httpClient = httpClient;
        this.apiToken = apiToken;
        this.senderName = senderName;
    }

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/message");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiToken}");
        request.Content = JsonContent.Create(new
        {
            telephone = message.ToPhoneNumber,
            text = message.Text,
            senderName,
            type = "SMS",
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new SmsTransportException(isPermanent: false, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmsTransportException(isPermanent: false, exception.Message);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;
            var transient = status >= 500 || status == 429;
            throw new SmsTransportException(isPermanent: !transient, $"{status} {body}");
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PayomSmsTransportTests`
Expected: PASS (all 7 cases: 1 request-shape + 5 status mappings + 1 network).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Notifications/ISmsTransport.cs src/AFK4.Platform.Api/Notifications/PayomSmsTransport.cs tests/AFK4.Platform.Api.Tests/Notifications/PayomSmsTransportTests.cs
git commit -m "feat(notifications): add payom.tj SMS transport"
```

---

### Task 2: SMS notification channel (`SmsChannel`)

**Files:**
- Create: `src/AFK4.Platform.Api/Notifications/SmsChannel.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Notifications/SmsChannelTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Notifications/SmsChannelTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class SmsChannelTests
{
    private static NotificationOutboxEntity Row(string phone = "+992937380070", string body = "код 123456") => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        Channel = "Sms",
        RecipientAddress = phone,
        BodyText = body,
    };

    [Fact]
    public void Channel_IsSms()
    {
        var channel = new SmsChannel(new StubSmsTransport());
        Assert.Equal(NotificationChannel.Sms, channel.Channel);
    }

    [Fact]
    public async Task SendAsync_DeliversTextToTransport()
    {
        var transport = new StubSmsTransport();
        var channel = new SmsChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("+992937380070", sent.ToPhoneNumber);
        Assert.Equal("код 123456", sent.Text);
    }

    [Fact]
    public async Task SendAsync_MissingPhone_IsPermanentFailure()
    {
        var channel = new SmsChannel(new StubSmsTransport());

        var result = await channel.SendAsync(Row(phone: ""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_PermanentTransportError_IsPermanent()
    {
        var channel = new SmsChannel(new StubSmsTransport(new SmsTransportException(isPermanent: true, "bad")));

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_TransientTransportError_IsRetryable()
    {
        var channel = new SmsChannel(new StubSmsTransport(new SmsTransportException(isPermanent: false, "later")));

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
    }

    private sealed class StubSmsTransport(Exception? throwOnSend = null) : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            if (throwOnSend is not null)
            {
                throw throwOnSend;
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SmsChannelTests`
Expected: FAIL — compile error, `SmsChannel` does not exist.

- [ ] **Step 3: Create the channel**

Create `src/AFK4.Platform.Api/Notifications/SmsChannel.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

public sealed class SmsChannel(ISmsTransport transport) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.RecipientAddress))
        {
            return ChannelResult.PermanentFailure("SMS recipient phone number is missing.");
        }

        if (string.IsNullOrWhiteSpace(row.BodyText))
        {
            return ChannelResult.PermanentFailure("SMS body is empty.");
        }

        try
        {
            await transport.SendAsync(new SmsMessage(row.RecipientAddress, row.BodyText), cancellationToken);
            return ChannelResult.Sent();
        }
        catch (SmsTransportException exception)
        {
            return exception.IsPermanent
                ? ChannelResult.PermanentFailure(exception.Message)
                : ChannelResult.TransientFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return ChannelResult.TransientFailure(exception.Message);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter SmsChannelTests`
Expected: PASS (5 cases).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Notifications/SmsChannel.cs tests/AFK4.Platform.Api.Tests/Notifications/SmsChannelTests.cs
git commit -m "feat(notifications): add SMS notification channel"
```

---

### Task 3: Options + DI registration + config

**Files:**
- Create: `src/AFK4.Platform.Api/Notifications/SmsOptions.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (after notification registrations, ~line 216)
- Modify: `src/AFK4.Platform.Api/appsettings.json`

- [ ] **Step 1: Create the options class + HttpClient-name constant**

Create `src/AFK4.Platform.Api/Notifications/SmsOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Notifications;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string BaseUrl { get; set; } = "https://gateway.payom.tj";
    public string ApiToken { get; set; } = string.Empty;
    public string SenderName { get; set; } = "AFK4.NET";
    public int TimeoutSeconds { get; set; } = 15;
}

public static class SmsClientRegistration
{
    public const string HttpClientName = "payom-sms";
}
```

- [ ] **Step 2: Register in DI**

In `src/AFK4.Platform.Api/Program.cs`, immediately AFTER the line
`builder.Services.AddSingleton<INotificationChannel, SmtpEmailChannel>();` (the notification block ~line 211), add:

```csharp
builder.Services.Configure<SmsOptions>(
    builder.Configuration.GetSection(SmsOptions.SectionName));
builder.Services.AddHttpClient(SmsClientRegistration.HttpClientName, (provider, http) =>
{
    var smsOptions = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(smsOptions.BaseUrl))
    {
        http.BaseAddress = new Uri(smsOptions.BaseUrl);
    }

    http.Timeout = TimeSpan.FromSeconds(smsOptions.TimeoutSeconds);
});
builder.Services.AddSingleton<ISmsTransport>(provider =>
{
    var smsOptions = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    return new PayomSmsTransport(
        factory.CreateClient(SmsClientRegistration.HttpClientName),
        smsOptions.ApiToken,
        smsOptions.SenderName);
});
builder.Services.AddSingleton<INotificationChannel, SmsChannel>();
```

Note: `IOptions<>` (`Microsoft.Extensions.Options`) and `IHttpClientFactory` are already used in this file (DcGate registration) — no new usings needed. If the build reports `AFK4.Platform.Api.Notifications` is not in scope, add `using AFK4.Platform.Api.Notifications;` at the top of Program.cs (it is already imported for the email channel).

- [ ] **Step 3: Add the config section**

In `src/AFK4.Platform.Api/appsettings.json`, add a `"Sms"` section as a sibling of `"Notifications"` and `"DcGate"`:

```jsonc
"Sms": {
  "BaseUrl": "https://gateway.payom.tj",
  "ApiToken": "",
  "SenderName": "AFK4.NET",
  "TimeoutSeconds": 15
},
```

The real `ApiToken` is supplied per environment via env var (e.g. `Sms__ApiToken`) on staging/prod — never committed.

- [ ] **Step 4: Verify the build and full test suite**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter Notifications`
Expected: PASS — all `PayomSmsTransportTests` + `SmsChannelTests` + existing `SmtpEmailChannelTests` green (proves the new SMS channel registration didn't disturb the email channel).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Notifications/SmsOptions.cs src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.Api/appsettings.json
git commit -m "feat(notifications): wire SMS channel and Sms config section"
```

---

## Self-review

**1. Spec coverage (§5 Phase A of the design):**
- `SmsOptions` (`"Sms"` section: BaseUrl/ApiToken/SenderName/TimeoutSeconds) → Task 3 ✓
- `ISmsTransport` + `PayomSmsTransport` (POST /api/message, Bearer, body shape, 201 ok, 401/403/422 permanent, 5xx/network transient) → Task 1 ✓
- `SmsNotificationChannel : INotificationChannel (Sms)` mapping transport errors → ChannelResult → Task 2 (named `SmsChannel`) ✓
- DI registration as singletons → Task 3 ✓
- Secrets from env, not committed → Task 3 note ✓
- **Deferred to Phase B (documented):** SMS template keys + embedded templates + `SendNowAsync` OTP wiring. Not a gap — intentional scope split to avoid the `EnsureKeysPresent` startup failure.

**2. Placeholder scan:** No TBD/TODO; every code step contains complete code; commands have expected output. ✓

**3. Type consistency:** `ISmsTransport.SendAsync(SmsMessage, ct)`, `SmsMessage(ToPhoneNumber, Text)`, `SmsTransportException(bool isPermanent, string)`, `PayomSmsTransport(HttpClient, string apiToken, string senderName)`, `SmsChannel(ISmsTransport)`, `SmsClientRegistration.HttpClientName` — names identical across Tasks 1–3 and tests. `ChannelResult.Sent()/PermanentFailure()/TransientFailure()` and `NotificationOutboxEntity.RecipientAddress/BodyText/Channel/NotificationOutboxId` match the verified existing definitions. ✓
