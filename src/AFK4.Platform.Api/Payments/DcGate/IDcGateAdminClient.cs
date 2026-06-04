namespace AFK4.Platform.Api.Payments.DcGate;

public interface IDcGateAdminClient
{
    // Phase 1: provision a dcgate project (= one card). externalId lets dcgate dedupe replays.
    Task<DcGateAdminProjectResult> CreateProjectAsync(
        DcGateCreateProjectRequest request,
        CancellationToken cancellationToken);

    // Phase 2 attach proxy.
    Task<DcGateTelegramStartResult> StartTelegramAsync(
        string dcgateProjectId,
        string phone,
        CancellationToken cancellationToken);

    Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(
        string dcgateProjectId,
        string loginAttemptId,
        string code,
        CancellationToken cancellationToken);

    Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(
        string dcgateProjectId,
        string loginAttemptId,
        string password,
        CancellationToken cancellationToken);

    Task<DcGateProjectStatusResult> GetStatusAsync(
        string dcgateProjectId,
        CancellationToken cancellationToken);
}

public sealed record DcGateCreateProjectRequest(
    string Name,
    string CardNumber,
    string WebhookUrl,
    int PaymentExpiresInMinutes,
    string ExternalId);

// apiKey + webhookSecret are present only on the FIRST (non-replay) response.
public sealed record DcGateAdminProjectResult(
    string Id,
    string Status,
    string CardLast4,
    string? ApiKey,
    string? WebhookSecret,
    bool IdempotentReplay);

public sealed record DcGateTelegramStartResult(
    string LoginAttemptId,
    string State);

public sealed record DcGateTelegramVerifyResult(
    string State);

public sealed record DcGateProjectStatusResult(
    string SessionHealth,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastMessageAt,
    int TelegramMessagesCount);
