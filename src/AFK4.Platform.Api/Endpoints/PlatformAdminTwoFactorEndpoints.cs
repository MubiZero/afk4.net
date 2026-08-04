using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Platform.Auth;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformAdminTwoFactorEndpoints
{
    public static void MapPlatformAdminTwoFactorEndpoints(this WebApplication app)
    {
        // Anonymous by design, like account-activation: the caller only ever holds a challenge
        // token here, never a working session, so there is nothing for authorizationService to
        // check. See PlatformAdminAuthenticationMiddleware — a challenge token doesn't resolve to a
        // PlatformAdminContext there either, so it can't leak into any normal endpoint.
        app.MapPost("/api/platform/auth/2fa/setup", async (
            TwoFactorChallengeRequest request,
            PlatformAdminTwoFactorService twoFactorService,
            CancellationToken cancellationToken) =>
        {
            var (secret, otpAuthUri, error) = await twoFactorService.BeginSetupAsync(request.ChallengeToken, cancellationToken);

            return error != TwoFactorError.None
                ? TwoFactorErrorResult(error)
                : Results.Ok(new TwoFactorSetupResponse(secret!, otpAuthUri!));
        });

        app.MapPost("/api/platform/auth/2fa/setup/confirm", async (
            TwoFactorVerifyRequest request,
            PlatformAdminTwoFactorService twoFactorService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var (session, recoveryCodes, error) = await twoFactorService.CompleteSetupAsync(
                request.ChallengeToken, request.Code, cancellationToken);

            if (error != TwoFactorError.None)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: null,
                    action: AuditActionNames.PlatformAdminTwoFactorConfigured,
                    targetType: "PlatformAdminUser",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Error = error.ToString() },
                    cancellationToken);
                return TwoFactorErrorResult(error);
            }

            // The recovery codes are handed to the caller exactly once here and never logged — the
            // audit trail only records that 2FA was configured, not the codes themselves.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: session!.PlatformAdminId,
                action: AuditActionNames.PlatformAdminTwoFactorConfigured,
                targetType: "PlatformAdminUser",
                targetId: session.PlatformAdminId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { },
                cancellationToken);

            return Results.Ok(new TwoFactorSetupConfirmResponse(session, recoveryCodes));
        });

        app.MapPost("/api/platform/auth/2fa/verify", async (
            TwoFactorVerifyRequest request,
            PlatformAdminTwoFactorService twoFactorService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var (session, error) = await twoFactorService.VerifyAsync(request.ChallengeToken, request.Code, cancellationToken);

            if (error != TwoFactorError.None)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: null,
                    action: AuditActionNames.PlatformAdminTwoFactorVerified,
                    targetType: "PlatformAdminUser",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { Error = error.ToString() },
                    cancellationToken);
                return TwoFactorErrorResult(error);
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: session!.PlatformAdminId,
                action: AuditActionNames.PlatformAdminTwoFactorVerified,
                targetType: "PlatformAdminUser",
                targetId: session.PlatformAdminId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { },
                cancellationToken);

            return Results.Ok(session);
        });

        app.MapPost("/api/platform/admins/{platformAdminUserId:guid}/2fa/reset", async (
            Guid platformAdminUserId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAdminTwoFactorService twoFactorService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.PlatformAdminTwoFactorReset,
                    targetType: "PlatformAdminUser",
                    targetId: platformAdminUserId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            await twoFactorService.ResetAsync(platformAdminUserId, cancellationToken);

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.PlatformAdminTwoFactorReset,
                targetType: "PlatformAdminUser",
                targetId: platformAdminUserId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { },
                cancellationToken);

            return Results.Ok();
        });
    }

    private static IResult TwoFactorErrorResult(TwoFactorError error) => error switch
    {
        TwoFactorError.InvalidChallenge => Results.Unauthorized(),
        TwoFactorError.InvalidCode => Results.Unauthorized(),
        TwoFactorError.LockedOut => Results.StatusCode(StatusCodes.Status429TooManyRequests),
        TwoFactorError.AlreadyConfigured => Results.Conflict(new
        {
            Error = "already_configured",
            Message = "Two-factor authentication is already configured for this account."
        }),
        _ => Results.Problem("Unexpected two-factor error.", statusCode: StatusCodes.Status500InternalServerError)
    };

    private sealed record TwoFactorChallengeRequest(string ChallengeToken);

    private sealed record TwoFactorVerifyRequest(string ChallengeToken, string Code);

    private sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUri);

    private sealed record TwoFactorSetupConfirmResponse(PlatformAdminSignInResponse Session, IReadOnlyList<string> RecoveryCodes);
}
