using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Games;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Каталог игр платформы (Platform Control), библиотека филиала (Панель) и выдача её агенту по
/// ключу устройства (спека оболочки, §6.6).
/// </summary>
internal static class GameLibraryEndpoints
{
    private const string CatalogTarget = "CatalogGame";
    private const string LibraryTarget = "BranchGame";

    public static void MapGameLibraryEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        MapCatalog(app);
        MapLibrary(organizations);
        MapDevice(app);
    }

    private static void MapCatalog(WebApplication app)
    {
        app.MapGet("/api/platform/games", async (
            PlatformAdminAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageGameCatalog);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty, authorization.PlatformAdminContext?.PlatformAdminUserId,
                    AuditActionNames.UpdateCatalogGame, CatalogTarget, null, AuditOutcome.Denied, new { Read = true }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var games = await dbContext.CatalogGames.AsNoTracking().OrderBy(game => game.Name).ToListAsync(cancellationToken);
            return Results.Ok(games.Select(GameLibrary.ToDto).ToList());
        });

        app.MapPost("/api/platform/games", async (
            UpsertCatalogGameRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageGameCatalog);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            var actor = authorization.PlatformAdminContext?.PlatformAdminUserId;
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty, actor,
                    AuditActionNames.CreateCatalogGame, CatalogTarget, null, AuditOutcome.Denied, new { request.Name }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (GameLibrary.Validate(request) is { } error)
            {
                return Invalid(error);
            }

            var now = timeProvider.GetUtcNow();
            var entity = new CatalogGameEntity { CatalogGameId = Guid.NewGuid(), CreatedAtUtc = now };
            Apply(entity, request, now, actor);
            dbContext.CatalogGames.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty, actor,
                AuditActionNames.CreateCatalogGame, CatalogTarget, entity.CatalogGameId.ToString("D"), AuditOutcome.Succeeded,
                new { entity.Name, entity.LaunchKind, entity.IsPublished }, cancellationToken);
            return Results.Ok(GameLibrary.ToDto(entity));
        });

        app.MapPut("/api/platform/games/{catalogGameId:guid}", async (
            Guid catalogGameId,
            UpsertCatalogGameRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageGameCatalog);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            var actor = authorization.PlatformAdminContext?.PlatformAdminUserId;
            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty, actor,
                    AuditActionNames.UpdateCatalogGame, CatalogTarget, catalogGameId.ToString("D"), AuditOutcome.Denied, new { request.Name }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (GameLibrary.Validate(request) is { } error)
            {
                return Invalid(error);
            }

            var entity = await dbContext.CatalogGames.SingleOrDefaultAsync(game => game.CatalogGameId == catalogGameId, cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            Apply(entity, request, now, actor);
            // Обложку и возраст клубы берут из каталога — их ПК должны перечитать библиотеку.
            await GameLibrary.BumpVersionsUsingAsync(dbContext, catalogGameId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WritePlatformAuditAsync(auditRecordWriter, Guid.Empty, actor,
                AuditActionNames.UpdateCatalogGame, CatalogTarget, catalogGameId.ToString("D"), AuditOutcome.Succeeded,
                new { entity.Name, entity.LaunchKind, entity.IsPublished }, cancellationToken);
            return Results.Ok(GameLibrary.ToDto(entity));
        });
    }

    private static void MapLibrary(IEndpointRouteBuilder organizations)
    {
        // Каталог для клуба — только опубликованное.
        organizations.MapGet("game-catalog", async (
            string? query,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ManageGameLibrary);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var games = dbContext.CatalogGames.AsNoTracking().Where(game => game.IsPublished);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var needle = query.Trim().ToUpperInvariant();
                games = games.Where(game => game.Name.ToUpper().Contains(needle));
            }

            var list = await games.OrderBy(game => game.Name).Take(200).ToListAsync(cancellationToken);
            return Results.Ok(list.Select(GameLibrary.ToDto).ToList());
        });

        organizations.MapGet("branches/{branchId:guid}/games", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageGameLibrary, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await GameLibrary.ListAsync(dbContext, authorization.StaffContext!.OrganizationId, branchId, cancellationToken));
        }).AllowPlatformSupportAccess(OrganizationPermissionNames.ManageGameLibrary);

        organizations.MapPost("branches/{branchId:guid}/games", async (
            Guid branchId,
            UpsertBranchGameRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageGameLibrary, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.AddBranchGame, LibraryTarget, null, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            if (request.OrganizationId != staff.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var catalog = await FindCatalogAsync(dbContext, request.CatalogGameId, cancellationToken);
            if (request.CatalogGameId is not null && catalog is null)
            {
                return Results.BadRequest(new { Error = "Catalog game was not found.", Code = GameLibraryErrorCodeNames.CatalogGameNotFound });
            }

            if (GameLibrary.Validate(request, catalog?.LaunchTarget) is { } error)
            {
                return Invalid(error);
            }

            var count = await dbContext.BranchGames.CountAsync(game => game.BranchId == branchId, cancellationToken);
            if (count >= GameLibraryLimits.MaxGamesPerBranch)
            {
                return Results.Conflict(new { Error = "The branch library is full.", Code = GameLibraryErrorCodeNames.LibraryFull });
            }

            var now = timeProvider.GetUtcNow();
            var entity = new BranchGameEntity
            {
                BranchGameId = Guid.NewGuid(),
                OrganizationId = staff.OrganizationId,
                BranchId = branchId,
                CatalogGameId = catalog?.CatalogGameId,
                SortOrder = count,
                CreatedAtUtc = now
            };
            Apply(entity, request, now);
            dbContext.BranchGames.Add(entity);
            await GameLibrary.BumpVersionAsync(dbContext, staff.OrganizationId, branchId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.AddBranchGame, LibraryTarget, entity.BranchGameId.ToString("D"), AuditOutcome.Succeeded,
                new { entity.Name, entity.LaunchKind, entity.CatalogGameId }, cancellationToken);
            return Results.Ok(GameLibrary.ToDto(entity, catalog));
        });

        organizations.MapPut("branches/{branchId:guid}/games/{branchGameId:guid}", async (
            Guid branchId,
            Guid branchGameId,
            UpsertBranchGameRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageGameLibrary, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdateBranchGame, LibraryTarget, branchGameId.ToString("D"), AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            if (request.OrganizationId != staff.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var entity = await dbContext.BranchGames.SingleOrDefaultAsync(
                game => game.OrganizationId == staff.OrganizationId && game.BranchId == branchId && game.BranchGameId == branchGameId,
                cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            // Игра из каталога остаётся игрой из каталога: связь меняет не правка, а новая игра.
            var catalog = await FindCatalogAsync(dbContext, entity.CatalogGameId, cancellationToken);
            if (GameLibrary.Validate(request, catalog?.LaunchTarget) is { } error)
            {
                return Invalid(error);
            }

            var now = timeProvider.GetUtcNow();
            Apply(entity, request, now);
            await GameLibrary.BumpVersionAsync(dbContext, staff.OrganizationId, branchId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.UpdateBranchGame, LibraryTarget, branchGameId.ToString("D"), AuditOutcome.Succeeded,
                new { entity.Name, entity.LaunchKind, entity.IsEnabled }, cancellationToken);
            return Results.Ok(GameLibrary.ToDto(entity, catalog));
        });

        organizations.MapDelete("branches/{branchId:guid}/games/{branchGameId:guid}", async (
            Guid branchId,
            Guid branchGameId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageGameLibrary, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.RemoveBranchGame, LibraryTarget, branchGameId.ToString("D"), AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var entity = await dbContext.BranchGames.SingleOrDefaultAsync(
                game => game.OrganizationId == staff.OrganizationId && game.BranchId == branchId && game.BranchGameId == branchGameId,
                cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            dbContext.BranchGames.Remove(entity);
            await GameLibrary.BumpVersionAsync(dbContext, staff.OrganizationId, branchId, timeProvider.GetUtcNow(), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.RemoveBranchGame, LibraryTarget, branchGameId.ToString("D"), AuditOutcome.Succeeded,
                new { entity.Name }, cancellationToken);
            return Results.NoContent();
        });

        organizations.MapPut("branches/{branchId:guid}/games/order", async (
            Guid branchId,
            ReorderBranchGamesRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageGameLibrary, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            if (request.OrganizationId != staff.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var games = await dbContext.BranchGames
                .Where(game => game.OrganizationId == staff.OrganizationId && game.BranchId == branchId)
                .ToListAsync(cancellationToken);
            // Порядок — всех игр сразу и без повторов: иначе две правки подряд перемешали бы список.
            if (request.BranchGameIds.Count != games.Count ||
                request.BranchGameIds.Distinct().Count() != games.Count ||
                games.Any(game => !request.BranchGameIds.Contains(game.BranchGameId)))
            {
                return Invalid("BranchGameIds must list every game of the branch exactly once.");
            }

            var now = timeProvider.GetUtcNow();
            foreach (var game in games)
            {
                game.SortOrder = request.BranchGameIds.ToList().IndexOf(game.BranchGameId);
                game.UpdatedAtUtc = now;
            }

            await GameLibrary.BumpVersionAsync(dbContext, staff.OrganizationId, branchId, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.ReorderBranchGames, LibraryTarget, null, AuditOutcome.Succeeded, new { games.Count }, cancellationToken);
            return Results.Ok(await GameLibrary.ListAsync(dbContext, staff.OrganizationId, branchId, cancellationToken));
        });
    }

    private static void MapDevice(WebApplication app)
    {
        app.MapGet("/api/devices/{deviceId:guid}/games", async (
            Guid deviceId,
            Guid organizationId,
            Guid branchId,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IOrganizationStatusGuard organizationStatusGuard,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var credentialSecret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(organizationId, branchId, deviceId, credentialSecret))
            {
                return Results.Unauthorized();
            }

            var suspended = await organizationStatusGuard.RequireActiveAsync(organizationId, cancellationToken);
            if (suspended is not null)
            {
                return suspended;
            }

            return Results.Ok(await GameLibrary.ForDeviceAsync(dbContext, organizationId, branchId, cancellationToken));
        });
    }

    private static IResult Invalid(string error) =>
        Results.BadRequest(new { Error = error, Code = GameLibraryErrorCodeNames.InvalidGame });

    private static async Task<CatalogGameEntity?> FindCatalogAsync(
        PlatformDbContext dbContext, Guid? catalogGameId, CancellationToken cancellationToken) =>
        catalogGameId is { } id
            ? await dbContext.CatalogGames.AsNoTracking().SingleOrDefaultAsync(game => game.CatalogGameId == id, cancellationToken)
            : null;

    private static void Apply(CatalogGameEntity entity, UpsertCatalogGameRequest request, DateTimeOffset now, Guid? actor)
    {
        entity.Name = request.Name.Trim();
        entity.Description = GameLibrary.Blank(request.Description);
        entity.Genre = GameLibrary.Blank(request.Genre);
        entity.MinAge = request.MinAge;
        entity.LaunchKind = request.LaunchKind;
        entity.LaunchTarget = GameLibrary.Blank(request.LaunchTarget);
        entity.CoverUrl = GameLibrary.Blank(request.CoverUrl);
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAtUtc = now;
        entity.UpdatedByPlatformAdminUserId = actor;
    }

    private static void Apply(BranchGameEntity entity, UpsertBranchGameRequest request, DateTimeOffset now)
    {
        entity.Name = request.Name.Trim();
        entity.Genre = GameLibrary.Blank(request.Genre);
        entity.MinAge = request.MinAge;
        entity.LaunchKind = request.LaunchKind;
        entity.LaunchTarget = GameLibrary.Blank(request.LaunchTarget);
        entity.ExecutablePath = GameLibrary.Blank(request.ExecutablePath);
        entity.Arguments = GameLibrary.Blank(request.Arguments);
        entity.AvailableWithoutSession = request.AvailableWithoutSession;
        entity.IsEnabled = request.IsEnabled;
        entity.LaunchOnSessionStart = request.LaunchOnSessionStart;
        entity.UpdatedAtUtc = now;
    }
}
