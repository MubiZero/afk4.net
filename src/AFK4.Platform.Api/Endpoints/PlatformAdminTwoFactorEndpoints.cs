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
            var (session, recoveryCodes, resolvedUserId, error) = await twoFactorService.CompleteSetupAsync(
                request.ChallengeToken, request.Code, cancellationToken);

            if (error != TwoFactorError.None)
            {
                // resolvedUserId is populated whenever the challenge itself resolved to a real
                // account, even though the code check then failed — that is exactly the case the
                // audit trail needs to distinguish "someone typo'd" from "this account is being
                // brute-forced".
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: resolvedUserId,
                    action: AuditActionNames.PlatformAdminTwoFactorConfigured,
                    targetType: "PlatformAdminUser",
                    targetId: resolvedUserId?.ToString("D"),
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
            var (session, resolvedUserId, error) = await twoFactorService.VerifyAsync(request.ChallengeToken, request.Code, cancellationToken);

            if (error != TwoFactorError.None)
            {
                // See the /2fa/setup/confirm handler above: resolvedUserId is set whenever the
                // challenge resolved to a real account, so a wrong-code Denied entry still says
                // which account was being probed, not just "someone, somewhere, failed".
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: resolvedUserId,
                    action: AuditActionNames.PlatformAdminTwoFactorVerified,
                    targetType: "PlatformAdminUser",
                    targetId: resolvedUserId?.ToString("D"),
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
