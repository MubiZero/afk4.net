# Customer-Shell News/Banners Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the network owner publish news/announcements (optional banner image, optional show window, optional single-branch targeting) that a seated player reads in the WebView2 React shell.

**Architecture:** A dedicated collection entity `NewsItemEntity` + a thin `EfNewsService` (validation + CRUD + the player visibility query) + owner CRUD endpoints under `/api/owner/news` + one player read endpoint `GET /api/me/news`. Player shell gets a `NewsScreen`; the owner web app gets a `NewsWorkspace`. This is "shop minus money/ledger/SignalR/status-transitions". Server is authoritative for content and visibility.

**Tech Stack:** .NET 10 minimal API + EF Core (PostgreSQL, InMemory for tests) + xUnit; React + TypeScript + Vite + `bun test` (happy-dom); `@afk4/i18n` for the operator app; raw Russian strings in the shell.

**Spec:** `docs/superpowers/specs/2026-06-10-customer-shell-unit-f-news-design.md`

**Conventions / gotchas (carried from shop + loyalty cycles):**
- `bun` is at `/home/fedya/.bun/bin/bun`. Shell tests: run from `src/AFK4.Player.Shell.Web`. Operator tests: from `src/AFK4.Operator.App.Web`. i18n codegen: from `packages/i18n` run `/home/fedya/.bun/bin/bun run gen`.
- Owner mutations use **POST/PATCH/DELETE** — `PlatformApiClient` has `get/post/patch/delete` but **no `put`**.
- Audit action-name constants live in `AFK4.Platform.Api.Audit.AuditActionNames` (NOT Shared.Contracts). Audit writes use `AuditOutcome.Succeeded` + `SourceApp: "PlatformApi"` (NOT string literals).
- `RequireOrganizationPermission(...)` is **synchronous** and returns `{ IsAuthenticated, IsAllowed, StaffContext }`.
- The player token (`PlayerContext`) carries only `PlayerAccountId`, `OrganizationId`, `PhoneVerified`. `HomeBranchId` is loaded from `PlayerAccountEntity` (type `Guid`, `Guid.Empty` if unset).
- Operator workspace MUST memoize its client via the `backend` prop + `useMemo` (else realtime re-renders wipe unsaved edits).
- No Moq — hand-rolled fakes. Tests that need transactions guard against the InMemory provider, but this cycle has no transactions.
- snake_case table names. Migrations live in `src/AFK4.Platform.Api/Data/Migrations`.
- TodoWrite → use `TaskCreate`/`TaskUpdate`/`TaskList`.

---

## Task N1: Shared contracts + `ManageNews` permission

**Files:**
- Create: `src/AFK4.Shared.Contracts/News/NewsItemDto.cs`
- Create: `src/AFK4.Shared.Contracts/News/CreateNewsItemRequest.cs`
- Create: `src/AFK4.Shared.Contracts/News/UpdateNewsItemRequest.cs`
- Create: `src/AFK4.Shared.Contracts/News/PlayerNewsItemDto.cs`
- Create: `src/AFK4.Shared.Contracts/News/OwnerBranchSummaryDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`

- [ ] **Step 1: Create the DTOs**

`src/AFK4.Shared.Contracts/News/NewsItemDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.News;

public sealed record NewsItemDto(
    Guid Id,
    Guid? BranchId,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
```

`src/AFK4.Shared.Contracts/News/CreateNewsItemRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.News;

public sealed record CreateNewsItemRequest(
    Guid? BranchId,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? ExpiresAtUtc);
```

`src/AFK4.Shared.Contracts/News/UpdateNewsItemRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.News;

public sealed record UpdateNewsItemRequest(
    Guid? BranchId,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? ExpiresAtUtc);
```

`src/AFK4.Shared.Contracts/News/PlayerNewsItemDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.News;

public sealed record PlayerNewsItemDto(
    Guid Id,
    string Title,
    string Body,
    string? ImageUrl,
    DateTimeOffset PublishedAtUtc);
```

`src/AFK4.Shared.Contracts/News/OwnerBranchSummaryDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.News;

public sealed record OwnerBranchSummaryDto(Guid BranchId, string Name);
```

- [ ] **Step 2: Add the permission constant**

In `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`, next to `ManageLoyaltySettings`:
```csharp
    public const string ManageNews = "news.manage";
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/AFK4.Shared.Contracts`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Shared.Contracts/News src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs
git commit -m "feat(contracts): news DTOs + ManageNews permission"
```

---

## Task N2: `NewsItemEntity` + EF config + DbSet + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/NewsItemEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create (via EF tool): `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddNewsItems.cs`

- [ ] **Step 1: Create the entity**

`src/AFK4.Platform.Api/Data/NewsItemEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class NewsItemEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Register the DbSet**

In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, next to the `OrganizationLoyaltySettings` DbSet (~line 9):
```csharp
    public DbSet<NewsItemEntity> NewsItems => Set<NewsItemEntity>();
```

- [ ] **Step 3: Configure the entity**

In `OnModelCreating`, right after the `OrganizationLoyaltySettingsEntity` block (~line 169):
```csharp
        modelBuilder.Entity<NewsItemEntity>(entity =>
        {
            entity.ToTable("news_items");
            entity.HasKey(news => news.Id);
            entity.Property(news => news.Title).HasMaxLength(200).IsRequired();
            entity.Property(news => news.Body).HasMaxLength(4000).IsRequired();
            entity.Property(news => news.ImageUrl).HasMaxLength(2048);
            entity.HasIndex(news => news.OrganizationId);
        });
```

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddNewsItems \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations
```
Expected: a new `<timestamp>_AddNewsItems.cs` + `.Designer.cs` + an updated model snapshot. The `Up` must `CreateTable("news_items", ...)` with an `IX_news_items_OrganizationId` index.

If `dotnet ef` is unavailable, hand-write `src/AFK4.Platform.Api/Data/Migrations/20260610120000_AddNewsItems.cs` (the entity + config still compile and all InMemory tests pass without it; the migration is only needed for real Postgres):
```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    public partial class AddNewsItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_news_items_OrganizationId",
                table: "news_items",
                column: "OrganizationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "news_items");
        }
    }
}
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Data
git commit -m "feat(api): NewsItemEntity + EF config + AddNewsItems migration"
```

---

## Task N3: `INewsService` + `EfNewsService` (validation + CRUD + player query)

**Files:**
- Create: `src/AFK4.Platform.Api/News/INewsService.cs`
- Create: `src/AFK4.Platform.Api/News/NewsMutationResult.cs`
- Create: `src/AFK4.Platform.Api/News/EfNewsService.cs`
- Create: `tests/AFK4.Platform.Api.Tests/News/EfNewsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/AFK4.Platform.Api.Tests/News/EfNewsServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.News;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.News;

public sealed class EfNewsServiceTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private static EfNewsService NewService(PlatformDbContext db) =>
        new(db, new FakeTimeProvider(Now));

    private static async Task SeedBranchAsync(PlatformDbContext db, Guid org, Guid branch)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "b",
            Name = "Branch",
            City = "Dushanbe",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static CreateNewsItemRequest ValidCreate(Guid? branchId = null) =>
        new(branchId, "Title", "Body", null, true, null, null);

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsItem()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal("Title", result.Item!.Title);
        Assert.Equal(1, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_TrimsAndRejectsEmptyTitle()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate() with { Title = "   " }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsNonHttpImageUrl()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate() with { ImageUrl = "ftp://x/y.png" }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvertedWindow()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org,
            ValidCreate() with { PublishAtUtc = Now.AddHours(2), ExpiresAtUtc = Now.AddHours(1) }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_RejectsForeignBranch()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate(branchId: Branch), default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_AcceptsOwnBranch()
    {
        using var db = NewDb();
        await SeedBranchAsync(db, Org, Branch);
        var result = await NewService(db).CreateAsync(Org, ValidCreate(branchId: Branch), default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal(Branch, result.Item!.BranchId);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundForForeignOrg()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        var update = new UpdateNewsItemRequest(null, "X", "Y", null, false, null, null);
        var result = await NewService(db).UpdateAsync(OtherOrg, created.Item!.Id, update, default);
        Assert.Equal(NewsMutationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesFields()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        var update = new UpdateNewsItemRequest(null, "New", "NewBody", "https://x/y.png", false, null, null);
        var result = await NewService(db).UpdateAsync(Org, created.Item!.Id, update, default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal("New", result.Item!.Title);
        Assert.False(result.Item.IsPublished);
        Assert.Equal("https://x/y.png", result.Item.ImageUrl);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwnAndRejectsForeign()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        Assert.Equal(NewsMutationOutcome.NotFound, await NewService(db).DeleteAsync(OtherOrg, created.Item!.Id, default));
        Assert.Equal(NewsMutationOutcome.Success, await NewService(db).DeleteAsync(Org, created.Item!.Id, default));
        Assert.Equal(0, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task ListForPlayer_AppliesPublishedWindowAndBranchFilters()
    {
        using var db = NewDb();
        var svc = NewService(db);
        // visible: published, no window, org-wide
        await svc.CreateAsync(Org, ValidCreate(), default);
        // hidden: not published
        await svc.CreateAsync(Org, ValidCreate() with { IsPublished = false }, default);
        // hidden: window not yet started
        await svc.CreateAsync(Org, ValidCreate() with { PublishAtUtc = Now.AddHours(1) }, default);
        // hidden: window already expired
        await svc.CreateAsync(Org, ValidCreate() with { ExpiresAtUtc = Now.AddHours(-1) }, default);
        await SeedBranchAsync(db, Org, Branch);
        // visible only to Branch players
        await svc.CreateAsync(Org, ValidCreate(branchId: Branch), default);

        var orgWide = await svc.ListForPlayerAsync(Org, homeBranchId: null, default);
        Assert.Single(orgWide);

        var branchPlayer = await svc.ListForPlayerAsync(Org, homeBranchId: Branch, default);
        Assert.Equal(2, branchPlayer.Count);
    }

    [Fact]
    public async Task ListForPlayer_PublishedAtUtcFallsBackToCreatedAt()
    {
        using var db = NewDb();
        var svc = NewService(db);
        await svc.CreateAsync(Org, ValidCreate(), default);
        var items = await svc.ListForPlayerAsync(Org, null, default);
        Assert.Equal(Now, items[0].PublishedAtUtc);
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfNewsServiceTests`
Expected: FAIL — `INewsService`/`EfNewsService`/`NewsMutationResult` do not exist.

- [ ] **Step 3: Create the result type**

`src/AFK4.Platform.Api/News/NewsMutationResult.cs`:
```csharp
using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.News;

public enum NewsMutationOutcome
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record NewsMutationResult(NewsMutationOutcome Outcome, NewsItemDto? Item, string? Error)
{
    public static NewsMutationResult Ok(NewsItemDto item) => new(NewsMutationOutcome.Success, item, null);
    public static NewsMutationResult Invalid(string error) => new(NewsMutationOutcome.ValidationFailed, null, error);
    public static readonly NewsMutationResult Missing = new(NewsMutationOutcome.NotFound, null, null);
}
```

- [ ] **Step 4: Create the interface**

`src/AFK4.Platform.Api/News/INewsService.cs`:
```csharp
using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.News;

public interface INewsService
{
    Task<IReadOnlyList<NewsItemDto>> ListForOwnerAsync(Guid orgId, CancellationToken ct);
    Task<NewsMutationResult> CreateAsync(Guid orgId, CreateNewsItemRequest request, CancellationToken ct);
    Task<NewsMutationResult> UpdateAsync(Guid orgId, Guid id, UpdateNewsItemRequest request, CancellationToken ct);
    Task<NewsMutationOutcome> DeleteAsync(Guid orgId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<PlayerNewsItemDto>> ListForPlayerAsync(Guid orgId, Guid? homeBranchId, CancellationToken ct);
}
```

- [ ] **Step 5: Implement the service**

`src/AFK4.Platform.Api/News/EfNewsService.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.News;

public sealed class EfNewsService(PlatformDbContext db, TimeProvider timeProvider) : INewsService
{
    private const int TitleMax = 200;
    private const int BodyMax = 4000;
    private const int ImageUrlMax = 2048;

    public async Task<IReadOnlyList<NewsItemDto>> ListForOwnerAsync(Guid orgId, CancellationToken ct)
    {
        var rows = await db.NewsItems.AsNoTracking()
            .Where(news => news.OrganizationId == orgId)
            .OrderByDescending(news => news.PublishAtUtc ?? news.CreatedAtUtc)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<NewsMutationResult> CreateAsync(Guid orgId, CreateNewsItemRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(orgId, request.Title, request.Body, request.ImageUrl,
            request.BranchId, request.PublishAtUtc, request.ExpiresAtUtc, ct);
        if (error is not null) return NewsMutationResult.Invalid(error);

        var now = timeProvider.GetUtcNow();
        var entity = new NewsItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = request.BranchId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            ImageUrl = NormalizeImageUrl(request.ImageUrl),
            IsPublished = request.IsPublished,
            PublishAtUtc = request.PublishAtUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.NewsItems.Add(entity);
        await db.SaveChangesAsync(ct);
        return NewsMutationResult.Ok(ToDto(entity));
    }

    public async Task<NewsMutationResult> UpdateAsync(Guid orgId, Guid id, UpdateNewsItemRequest request, CancellationToken ct)
    {
        var entity = await db.NewsItems.SingleOrDefaultAsync(news => news.Id == id && news.OrganizationId == orgId, ct);
        if (entity is null) return NewsMutationResult.Missing;

        var error = await ValidateAsync(orgId, request.Title, request.Body, request.ImageUrl,
            request.BranchId, request.PublishAtUtc, request.ExpiresAtUtc, ct);
        if (error is not null) return NewsMutationResult.Invalid(error);

        entity.BranchId = request.BranchId;
        entity.Title = request.Title.Trim();
        entity.Body = request.Body.Trim();
        entity.ImageUrl = NormalizeImageUrl(request.ImageUrl);
        entity.IsPublished = request.IsPublished;
        entity.PublishAtUtc = request.PublishAtUtc;
        entity.ExpiresAtUtc = request.ExpiresAtUtc;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return NewsMutationResult.Ok(ToDto(entity));
    }

    public async Task<NewsMutationOutcome> DeleteAsync(Guid orgId, Guid id, CancellationToken ct)
    {
        var entity = await db.NewsItems.SingleOrDefaultAsync(news => news.Id == id && news.OrganizationId == orgId, ct);
        if (entity is null) return NewsMutationOutcome.NotFound;
        db.NewsItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NewsMutationOutcome.Success;
    }

    public async Task<IReadOnlyList<PlayerNewsItemDto>> ListForPlayerAsync(Guid orgId, Guid? homeBranchId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await db.NewsItems.AsNoTracking()
            .Where(news => news.OrganizationId == orgId
                && news.IsPublished
                && (news.BranchId == null || news.BranchId == homeBranchId)
                && (news.PublishAtUtc == null || news.PublishAtUtc <= now)
                && (news.ExpiresAtUtc == null || news.ExpiresAtUtc > now))
            .OrderByDescending(news => news.PublishAtUtc ?? news.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return rows
            .Select(news => new PlayerNewsItemDto(news.Id, news.Title, news.Body, news.ImageUrl,
                news.PublishAtUtc ?? news.CreatedAtUtc))
            .ToList();
    }

    private async Task<string?> ValidateAsync(Guid orgId, string title, string body, string? imageUrl,
        Guid? branchId, DateTimeOffset? publishAt, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Title is required.";
        if (title.Trim().Length > TitleMax) return $"Title must be at most {TitleMax} characters.";
        if (string.IsNullOrWhiteSpace(body)) return "Body is required.";
        if (body.Trim().Length > BodyMax) return $"Body must be at most {BodyMax} characters.";
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (imageUrl.Length > ImageUrlMax) return $"Image URL must be at most {ImageUrlMax} characters.";
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "Image URL must be an absolute http or https address.";
            }
        }
        if (publishAt is not null && expiresAt is not null && publishAt >= expiresAt)
        {
            return "PublishAtUtc must be earlier than ExpiresAtUtc.";
        }
        if (branchId is not null)
        {
            var exists = await db.Branches.AsNoTracking()
                .AnyAsync(branch => branch.BranchId == branchId && branch.OrganizationId == orgId, ct);
            if (!exists) return "BranchId does not belong to this organization.";
        }
        return null;
    }

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

    private static NewsItemDto ToDto(NewsItemEntity news) =>
        new(news.Id, news.BranchId, news.Title, news.Body, news.ImageUrl, news.IsPublished,
            news.PublishAtUtc, news.ExpiresAtUtc, news.CreatedAtUtc, news.UpdatedAtUtc);
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfNewsServiceTests`
Expected: PASS (all green).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/News tests/AFK4.Platform.Api.Tests/News
git commit -m "feat(api): EfNewsService with validation, CRUD, and player visibility query"
```

---

## Task N4: Audit names + permission grant + owner endpoints + DI registration

**Files:**
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/NewsEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/OwnerNewsEndpointsTests.cs`

- [ ] **Step 1: Add audit action names**

In `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`, with the other constants:
```csharp
    public const string CreateNews = "news.create";
    public const string UpdateNews = "news.update";
    public const string DeleteNews = "news.delete";
```

- [ ] **Step 2: Grant the permission to Owner**

In `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`, inside the `StaffRoleNames.Owner` set, next to `StaffPermissionNames.ManageLoyaltySettings`:
```csharp
                StaffPermissionNames.ManageNews,
```

- [ ] **Step 3: Register the service**

In `src/AFK4.Platform.Api/Program.cs`, next to the other `AddScoped` registrations:
```csharp
builder.Services.AddScoped<INewsService, EfNewsService>();
```
(Add `using AFK4.Platform.Api.News;` if the file uses explicit usings.)

- [ ] **Step 4: Write the owner endpoints**

`src/AFK4.Platform.Api/Endpoints/NewsEndpoints.cs`:
```csharp
using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.News;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class NewsEndpoints
{
    public static void MapNewsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/owner/news", async (
            StaffAuthorizationService authorizationService,
            INewsService news,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var items = await news.ListForOwnerAsync(authorization.StaffContext!.OrganizationId, ct);
            return Results.Ok(items);
        });

        app.MapGet("/api/owner/branches", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var branches = await db.Branches.AsNoTracking()
                .Where(branch => branch.OrganizationId == authorization.StaffContext!.OrganizationId)
                .OrderBy(branch => branch.Name)
                .Select(branch => new OwnerBranchSummaryDto(branch.BranchId, branch.Name))
                .ToListAsync(ct);
            return Results.Ok(branches);
        });

        app.MapPost("/api/owner/news", async (
            CreateNewsItemRequest request,
            StaffAuthorizationService authorizationService,
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await news.CreateAsync(staff.OrganizationId, request, ct);
            if (result.Outcome == NewsMutationOutcome.ValidationFailed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["news"] = [result.Error!] });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.CreateNews,
                TargetType: "NewsItem",
                TargetId: result.Item!.Id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Item);
        });

        app.MapPatch("/api/owner/news/{id:guid}", async (
            Guid id,
            UpdateNewsItemRequest request,
            StaffAuthorizationService authorizationService,
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var result = await news.UpdateAsync(staff.OrganizationId, id, request, ct);
            if (result.Outcome == NewsMutationOutcome.NotFound) return Results.NotFound();
            if (result.Outcome == NewsMutationOutcome.ValidationFailed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["news"] = [result.Error!] });
            }

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateNews,
                TargetType: "NewsItem",
                TargetId: id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: JsonSerializer.Serialize(request)), ct);

            return Results.Ok(result.Item);
        });

        app.MapDelete("/api/owner/news/{id:guid}", async (
            Guid id,
            StaffAuthorizationService authorizationService,
            INewsService news,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageNews);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var staff = authorization.StaffContext!;
            var outcome = await news.DeleteAsync(staff.OrganizationId, id, ct);
            if (outcome == NewsMutationOutcome.NotFound) return Results.NotFound();

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                staff.OrganizationId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.DeleteNews,
                TargetType: "NewsItem",
                TargetId: id.ToString("N"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: null), ct);

            return Results.NoContent();
        });
    }
}
```

- [ ] **Step 5: Register the endpoints**

In `src/AFK4.Platform.Api/Program.cs`, next to `app.MapLoyaltySettingsEndpoints();`:
```csharp
app.MapNewsEndpoints();
```

- [ ] **Step 6: Write the owner integration tests**

`tests/AFK4.Platform.Api.Tests/OwnerNewsEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.News;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerNewsEndpointsTests
{
    private static CreateNewsItemRequest Valid() =>
        new(null, "Hello", "World", null, true, null, null);

    [Fact]
    public async Task CreateListPatchDelete_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var create = await owner.PostAsJsonAsync("/api/owner/news", Valid());
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<NewsItemDto>();

        var list = await owner.GetFromJsonAsync<NewsItemDto[]>("/api/owner/news");
        Assert.Single(list!);

        var patch = await owner.PatchAsJsonAsync($"/api/owner/news/{created!.Id}",
            new UpdateNewsItemRequest(null, "Edited", "Body2", null, false, null, null));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var edited = await patch.Content.ReadFromJsonAsync<NewsItemDto>();
        Assert.Equal("Edited", edited!.Title);
        Assert.False(edited.IsPublished);

        var delete = await owner.DeleteAsync($"/api/owner/news/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await owner.GetFromJsonAsync<NewsItemDto[]>("/api/owner/news");
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task Create_RejectsEmptyTitle()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var create = await owner.PostAsJsonAsync("/api/owner/news",
            new CreateNewsItemRequest(null, "   ", "Body", null, true, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Patch_ReturnsNotFoundForUnknownId()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var patch = await owner.PatchAsJsonAsync($"/api/owner/news/{Guid.NewGuid()}",
            new UpdateNewsItemRequest(null, "X", "Y", null, true, null, null));
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task Create_ForbiddenForNonOwner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var create = await nonOwner.PostAsJsonAsync("/api/owner/news", Valid());
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Branches_ReturnsOwnOrgBranches()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var branches = await owner.GetFromJsonAsync<OwnerBranchSummaryDto[]>("/api/owner/branches");
        Assert.NotNull(branches);
    }
}
```

> Note: `PatchAsJsonAsync` lives in `System.Net.Http.Json`. If `OwnerTestAuth.SignInOwnerAsync` seeds at least one branch, `Branches_ReturnsOwnOrgBranches` can assert `Assert.NotEmpty(branches!)`; if it seeds none, keep `Assert.NotNull`. Check `OwnerTestAuth` and tighten the assertion accordingly.

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~OwnerNewsEndpointsTests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests/OwnerNewsEndpointsTests.cs
git commit -m "feat(api): owner news CRUD endpoints + branches list + audit + permission"
```

---

## Task N5: Player news endpoint

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlayerNewsEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/PlayerNewsEndpointsTests.cs`

- [ ] **Step 1: Write the endpoint**

`src/AFK4.Platform.Api/Endpoints/PlayerNewsEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.News;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerNewsEndpoints
{
    public static void MapPlayerNewsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/news", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext db,
            INewsService news,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var homeBranchId = await db.PlayerAccounts.AsNoTracking()
                .Where(account => account.PlayerAccountId == player.PlayerAccountId)
                .Select(account => (Guid?)account.HomeBranchId)
                .FirstOrDefaultAsync(ct);
            var effectiveBranch = homeBranchId == Guid.Empty ? null : homeBranchId;

            var items = await news.ListForPlayerAsync(player.OrganizationId, effectiveBranch, ct);
            return Results.Ok(items);
        }).RequireRateLimiting("player-me");
    }
}
```

- [ ] **Step 2: Register the endpoint**

In `src/AFK4.Platform.Api/Program.cs`, next to `app.MapPlayerLoyaltyEndpoints();`:
```csharp
app.MapPlayerNewsEndpoints();
```

- [ ] **Step 3: Write the integration test**

`tests/AFK4.Platform.Api.Tests/PlayerNewsEndpointsTests.cs` — reuse the player-seeding pattern from `PlayerLoyaltyEndpointsTests.cs` (copy `SeededPlayer`, `SeedPlayerAsync`, `AuthenticateAsync` verbatim into this file), then:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.News;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerNewsEndpointsTests
{
    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    // --- copy SeedPlayerAsync + AuthenticateAsync from PlayerLoyaltyEndpointsTests.cs ---

    private static async Task SeedNewsAsync(PlatformApiFactory factory, Guid orgId, Guid? branchId,
        bool published, DateTimeOffset? publishAt, DateTimeOffset? expiresAt, string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.NewsItems.Add(new NewsItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            Title = title,
            Body = "Body",
            IsPublished = published,
            PublishAtUtc = publishAt,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetNews_ReturnsOrgWideAndOwnBranchPublishedItems()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");

        await SeedNewsAsync(factory, player.OrgId, null, true, null, null, "OrgWide");
        await SeedNewsAsync(factory, player.OrgId, player.BranchId, true, null, null, "MyBranch");
        await SeedNewsAsync(factory, player.OrgId, Guid.NewGuid(), true, null, null, "OtherBranch");
        await SeedNewsAsync(factory, player.OrgId, null, false, null, null, "Draft");
        await SeedNewsAsync(factory, player.OrgId, null, true, DateTimeOffset.UtcNow.AddHours(1), null, "Future");

        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");
        var items = await client.GetFromJsonAsync<PlayerNewsItemDto[]>("/api/me/news");

        Assert.Equal(2, items!.Length);
        Assert.Contains(items, news => news.Title == "OrgWide");
        Assert.Contains(items, news => news.Title == "MyBranch");
    }

    [Fact]
    public async Task GetNews_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/news");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 4: Run the test**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlayerNewsEndpointsTests`
Expected: PASS.

- [ ] **Step 5: Run the full server test suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (all green; ~1157 + the new news tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests/PlayerNewsEndpointsTests.cs
git commit -m "feat(api): player GET /api/me/news endpoint"
```

---

## Task N6: Shell API mirror + `getNews`

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/apiTypes.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.ts`

- [ ] **Step 1: Add the DTO mirror**

In `src/AFK4.Player.Shell.Web/src/apiTypes.ts`, append:
```ts
export interface PlayerNewsItemDto {
  id: string;
  title: string;
  body: string;
  imageUrl: string | null;
  publishedAtUtc: string;
}
```

- [ ] **Step 2: Add the API method**

In `src/AFK4.Player.Shell.Web/src/shellApi.ts`:
- add `PlayerNewsItemDto` to the type import from `'./apiTypes'`;
- add this method to the returned object (after `getLoyalty`):
```ts
    getNews: () => call<PlayerNewsItemDto[]>('/api/me/news')
```
(Add a comma after the previous `getLoyalty` line.)

- [ ] **Step 3: Type-check**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds (tsc + vite), 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/apiTypes.ts src/AFK4.Player.Shell.Web/src/shellApi.ts
git commit -m "feat(shell): PlayerNewsItemDto mirror + getNews api method"
```

---

## Task N7: Shell `NewsScreen`

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/NewsScreen.tsx`
- Create: `src/AFK4.Player.Shell.Web/src/screens/NewsScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/AFK4.Player.Shell.Web/src/screens/NewsScreen.test.tsx`:
```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { NewsScreen } from './NewsScreen';
import type { ShellApi } from '../shellApi';

function api(over: Partial<ShellApi>): ShellApi {
  return {
    getNews: async () => [
      { id: '1', title: 'Турнир в субботу', body: 'Призовой фонд 1000', imageUrl: null, publishedAtUtc: '2026-06-09T10:00:00Z' }
    ],
    ...over
  } as unknown as ShellApi;
}

describe('NewsScreen', () => {
  it('renders news cards from the api', async () => {
    render(<NewsScreen api={api({})} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/Турнир в субботу/));
    expect(screen.getByText(/Призовой фонд 1000/)).toBeInTheDocument();
  });

  it('shows an empty state when there is no news', async () => {
    render(<NewsScreen api={api({ getNews: async () => [] })} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/новостей пока нет/i));
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test NewsScreen`
Expected: FAIL — `NewsScreen` does not exist.

- [ ] **Step 3: Implement the screen**

`src/AFK4.Player.Shell.Web/src/screens/NewsScreen.tsx`:
```tsx
import { useEffect, useMemo, useState } from 'react';
import type { ShellApi } from '../shellApi';
import { OfflineError } from '../shellApi';
import type { PlayerNewsItemDto } from '../apiTypes';
import { createCachedLoader, indexedDbStore } from '../idbCache';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('ru-RU');
}

export function NewsScreen({ api, onDone }: { api: ShellApi; onDone: () => void }) {
  const [items, setItems] = useState<PlayerNewsItemDto[] | null>(null);
  const [offline, setOffline] = useState(false);
  const load = useMemo(
    () => createCachedLoader<PlayerNewsItemDto[]>(indexedDbStore(), 'news', () => api.getNews()),
    [api]
  );

  useEffect(() => {
    let active = true;
    load().then(
      (data) => { if (active) setItems(data); },
      (error) => {
        if (!active) return;
        if (error instanceof OfflineError) setOffline(true);
        setItems([]);
      }
    );
    return () => { active = false; };
  }, [load]);

  if (items === null) {
    return <section><h2>Новости</h2><p>Загрузка…</p></section>;
  }

  return (
    <section>
      <h2>Новости</h2>
      {offline && <p>Нет связи — показаны последние сохранённые новости.</p>}
      {items.length === 0 && <p>Новостей пока нет.</p>}
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            {item.imageUrl && (
              <img
                src={item.imageUrl}
                alt=""
                onError={(event) => { event.currentTarget.style.display = 'none'; }}
              />
            )}
            <h3>{item.title}</h3>
            <p>{item.body}</p>
            <small>{formatDate(item.publishedAtUtc)}</small>
          </li>
        ))}
      </ul>
      <button type="button" onClick={onDone}>Назад</button>
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test NewsScreen`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/screens/NewsScreen.tsx src/AFK4.Player.Shell.Web/src/screens/NewsScreen.test.tsx
git commit -m "feat(shell): NewsScreen with offline cache + empty state"
```

---

## Task N8: Wire `NewsScreen` into `SelfServiceMenu`

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx`

- [ ] **Step 1: Add the import**

After the `LoyaltyScreen` import:
```ts
import { NewsScreen } from './NewsScreen';
```

- [ ] **Step 2: Extend the View union**

```ts
type View = 'menu' | 'extend' | 'topup' | 'shop' | 'loyalty' | 'news';
```

- [ ] **Step 3: Add the conditional render**

After the `view === 'loyalty'` block:
```tsx
  if (view === 'news') {
    return <NewsScreen api={api} onDone={() => { setView('menu'); onReloadState(); }} />;
  }
```

- [ ] **Step 4: Add the menu button**

In the `<nav aria-label="self-service">`, after the "Кэшбэк" button (NOT session-gated — like loyalty):
```tsx
      <button type="button" onClick={() => setView('news')}>Новости</button>
```

- [ ] **Step 5: Run the full shell test suite + build**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build`
Expected: all bun tests PASS, build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx
git commit -m "feat(shell): wire NewsScreen into SelfServiceMenu"
```

---

## Task N9: Operator news API client + branches client

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`

- [ ] **Step 1: Add the DTOs + client factory**

In `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`, near the loyalty client (~line 594):
```ts
export interface NewsItemDto {
  id: string;
  branchId: string | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: string | null;
  expiresAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface OwnerBranchSummaryDto {
  branchId: string;
  name: string;
}

export interface NewsItemInput {
  branchId: string | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: string | null;
  expiresAtUtc: string | null;
}

export function createNewsClient(api: PlatformApiClient) {
  return {
    list(): Promise<NewsItemDto[]> {
      return api.get<NewsItemDto[]>('/api/owner/news');
    },
    listBranches(): Promise<OwnerBranchSummaryDto[]> {
      return api.get<OwnerBranchSummaryDto[]>('/api/owner/branches');
    },
    create(request: NewsItemInput): Promise<NewsItemDto> {
      return api.post<NewsItemDto, NewsItemInput>('/api/owner/news', request);
    },
    update(id: string, request: NewsItemInput): Promise<NewsItemDto> {
      return api.patch<NewsItemDto, NewsItemInput>(`/api/owner/news/${id}`, request);
    },
    remove(id: string): Promise<void> {
      return api.delete<void>(`/api/owner/news/${id}`);
    }
  };
}
```

- [ ] **Step 2: Register the client**

In `createOperatorApiClients`, after `loyaltySettings: createLoyaltySettingsClient(api)` (add a comma):
```ts
    news: createNewsClient(api)
```

- [ ] **Step 3: Type-check**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds (this build will fail later only if a workspace references missing i18n keys — keys come in N11; for now the client compiles standalone).

> If `bun run build` here trips on the not-yet-added `NewsWorkspace`, that's fine — this task only needs `tsc` to accept the client. Run instead: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test operatorApiClients` if such a test exists, else proceed to commit (the build gate runs at N11).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts
git commit -m "feat(operator): news api client + owner branches list"
```

---

## Task N10: Operator `NewsWorkspace`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/NewsWorkspace.test.tsx`

- [ ] **Step 1: Write the failing test**

`src/AFK4.Operator.App.Web/src/NewsWorkspace.test.tsx`:
```tsx
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { NewsWorkspace } from './NewsWorkspace';
import type { NewsItemDto, NewsItemInput, OwnerBranchSummaryDto } from './operatorApiClients';

function client(initial: NewsItemDto[] = []) {
  const created: NewsItemInput[] = [];
  const removed: string[] = [];
  let store = [...initial];
  return {
    created,
    removed,
    list: async () => store,
    listBranches: async (): Promise<OwnerBranchSummaryDto[]> => [{ branchId: 'b1', name: 'Центр' }],
    create: async (req: NewsItemInput) => {
      created.push(req);
      const dto: NewsItemDto = {
        id: 'new', branchId: req.branchId, title: req.title, body: req.body, imageUrl: req.imageUrl,
        isPublished: req.isPublished, publishAtUtc: req.publishAtUtc, expiresAtUtc: req.expiresAtUtc,
        createdAtUtc: '2026-06-10T00:00:00Z', updatedAtUtc: '2026-06-10T00:00:00Z'
      };
      store = [dto, ...store];
      return dto;
    },
    update: async (_id: string, req: NewsItemInput) => ({
      id: _id, branchId: req.branchId, title: req.title, body: req.body, imageUrl: req.imageUrl,
      isPublished: req.isPublished, publishAtUtc: req.publishAtUtc, expiresAtUtc: req.expiresAtUtc,
      createdAtUtc: '2026-06-10T00:00:00Z', updatedAtUtc: '2026-06-10T00:00:00Z'
    }),
    remove: async (id: string) => { removed.push(id); store = store.filter((n) => n.id !== id); }
  };
}

function renderWorkspace(c: ReturnType<typeof client>) {
  render(<I18nProvider><NewsWorkspace backend={null} client={c as never} /></I18nProvider>);
}

describe('NewsWorkspace', () => {
  afterEach(() => cleanup());

  it('creates a news item from the form', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/заголовок/i));
    fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
    fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.created).toHaveLength(1));
    expect(c.created[0].title).toBe('Турнир');
  });

  it('rejects an empty title', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/заголовок/i));
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => screen.getByText(/заголовок и текст обязательны/i));
    expect(c.created).toHaveLength(0);
  });

  it('lists existing items and deletes one', async () => {
    const c = client([{
      id: 'x1', branchId: null, title: 'Старая', body: 'B', imageUrl: null,
      isPublished: true, publishAtUtc: null, expiresAtUtc: null,
      createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
    }]);
    renderWorkspace(c);
    await waitFor(() => screen.getByText(/Старая/));
    fireEvent.click(screen.getByRole('button', { name: /удалить/i }));
    await waitFor(() => expect(c.removed).toEqual(['x1']));
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test NewsWorkspace`
Expected: FAIL — `NewsWorkspace` does not exist (and the `op.news.*` i18n keys it uses are added in N11; the test asserts against the rendered Russian default strings, so it needs the keys present — **do N11's locale edits before re-running if the i18n provider throws on missing keys**; otherwise the fallback renders the key name. Add the keys in N11, then this test goes green).

> Pragmatic ordering note: the `op.news.*` keys this workspace references are added in Task N11. If `@afk4/i18n` throws on unknown keys, complete N11 Step 1–2 (locale entries + `bun run gen`) **before** running this test. The two tasks are coupled; commit them together if needed.

- [ ] **Step 3: Implement the workspace**

`src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx`:
```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext } from './operatorTypes';
import type { NewsItemDto, NewsItemInput, OwnerBranchSummaryDto } from './operatorApiClients';

interface NewsClient {
  list(): Promise<NewsItemDto[]>;
  listBranches(): Promise<OwnerBranchSummaryDto[]>;
  create(request: NewsItemInput): Promise<NewsItemDto>;
  update(id: string, request: NewsItemInput): Promise<NewsItemDto>;
  remove(id: string): Promise<void>;
}

const EMPTY = {
  id: null as string | null,
  branchId: '',
  title: '',
  body: '',
  imageUrl: '',
  isPublished: true,
  publishAt: '',
  expiresAt: ''
};

function toIsoOrNull(localValue: string): string | null {
  if (!localValue) return null;
  return new Date(localValue).toISOString();
}

function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function NewsWorkspace({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: NewsClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).news : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;

  const [items, setItems] = useState<NewsItemDto[]>([]);
  const [branches, setBranches] = useState<OwnerBranchSummaryDto[]>([]);
  const [form, setForm] = useState({ ...EMPTY });
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    Promise.all([client.list(), client.listBranches()]).then(([list, branchList]) => {
      if (!active) return;
      setItems(list);
      setBranches(branchList);
      setReady(true);
    });
    return () => { active = false; };
  }, [client]);

  const reload = async () => {
    if (client === null) return;
    setItems(await client.list());
  };

  const edit = (item: NewsItemDto) => {
    setError(null);
    setForm({
      id: item.id,
      branchId: item.branchId ?? '',
      title: item.title,
      body: item.body,
      imageUrl: item.imageUrl ?? '',
      isPublished: item.isPublished,
      publishAt: toLocalInput(item.publishAtUtc),
      expiresAt: toLocalInput(item.expiresAtUtc)
    });
  };

  const save = async () => {
    if (client === null) return;
    if (!form.title.trim() || !form.body.trim()) {
      setError(t('op.news.errorRequired'));
      return;
    }
    const publishAtUtc = toIsoOrNull(form.publishAt);
    const expiresAtUtc = toIsoOrNull(form.expiresAt);
    if (publishAtUtc !== null && expiresAtUtc !== null && publishAtUtc >= expiresAtUtc) {
      setError(t('op.news.errorWindow'));
      return;
    }
    setError(null);
    const request: NewsItemInput = {
      branchId: form.branchId === '' ? null : form.branchId,
      title: form.title.trim(),
      body: form.body.trim(),
      imageUrl: form.imageUrl.trim() === '' ? null : form.imageUrl.trim(),
      isPublished: form.isPublished,
      publishAtUtc,
      expiresAtUtc
    };
    if (form.id === null) {
      await client.create(request);
    } else {
      await client.update(form.id, request);
    }
    setForm({ ...EMPTY });
    await reload();
  };

  const remove = async (id: string) => {
    if (client === null) return;
    await client.remove(id);
    await reload();
  };

  if (!ready) {
    return (
      <main className="workspace-screen news-screen">
        <section className="screen-head"><h1>{t('op.news.title')}</h1></section>
        <p>…</p>
      </main>
    );
  }

  return (
    <main className="workspace-screen news-screen">
      <section className="screen-head"><h1>{t('op.news.title')}</h1></section>

      <form onSubmit={(event) => { event.preventDefault(); void save(); }}>
        <label>
          {t('op.news.fieldTitle')}
          <input value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldBody')}
          <textarea value={form.body} onChange={(event) => setForm({ ...form, body: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldImage')}
          <input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldBranch')}
          <select value={form.branchId} onChange={(event) => setForm({ ...form, branchId: event.target.value })}>
            <option value="">{t('op.news.allBranches')}</option>
            {branches.map((branch) => (
              <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
            ))}
          </select>
        </label>
        <label>
          <input
            type="checkbox"
            checked={form.isPublished}
            onChange={(event) => setForm({ ...form, isPublished: event.target.checked })}
          />
          {t('op.news.published')}
        </label>
        <label>
          {t('op.news.publishAt')}
          <input type="datetime-local" value={form.publishAt} onChange={(event) => setForm({ ...form, publishAt: event.target.value })} />
        </label>
        <label>
          {t('op.news.expiresAt')}
          <input type="datetime-local" value={form.expiresAt} onChange={(event) => setForm({ ...form, expiresAt: event.target.value })} />
        </label>
        {error && <p role="alert">{error}</p>}
        <button type="submit">{t('op.news.save')}</button>
        {form.id !== null && (
          <button type="button" onClick={() => { setForm({ ...EMPTY }); setError(null); }}>{t('op.news.cancel')}</button>
        )}
      </form>

      {items.length === 0 && <p>{t('op.news.empty')}</p>}
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            <strong>{item.title}</strong>
            {!item.isPublished && <em> ({t('op.news.draftTag')})</em>}
            <button type="button" onClick={() => edit(item)}>{t('op.news.edit')}</button>
            <button type="button" onClick={() => void remove(item.id)}>{t('op.news.delete')}</button>
          </li>
        ))}
      </ul>
    </main>
  );
}
```

- [ ] **Step 4: Run the test (after N11 keys exist) to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test NewsWorkspace`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx src/AFK4.Operator.App.Web/src/NewsWorkspace.test.tsx
git commit -m "feat(operator): NewsWorkspace list + create/edit/delete form"
```

---

## Task N11: Nav wiring + i18n

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorTypes.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/SummarySidePanel.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts`

- [ ] **Step 1: Add i18n keys**

Add this block to each locale file alongside the existing `op.loyalty.*` keys.

`locales/ru.json`:
```json
"op.news.nav": "Новости",
"op.news.title": "Новости",
"op.news.fieldTitle": "Заголовок",
"op.news.fieldBody": "Текст",
"op.news.fieldImage": "URL картинки",
"op.news.fieldBranch": "Филиал",
"op.news.allBranches": "Все филиалы",
"op.news.published": "Опубликовано",
"op.news.publishAt": "Показывать с",
"op.news.expiresAt": "Скрыть после",
"op.news.save": "Сохранить",
"op.news.cancel": "Отмена",
"op.news.edit": "Изменить",
"op.news.delete": "Удалить",
"op.news.empty": "Новостей пока нет",
"op.news.draftTag": "черновик",
"op.news.errorRequired": "Заголовок и текст обязательны",
"op.news.errorWindow": "Дата начала должна быть раньше даты конца",
```

`locales/en.json`:
```json
"op.news.nav": "News",
"op.news.title": "News",
"op.news.fieldTitle": "Title",
"op.news.fieldBody": "Text",
"op.news.fieldImage": "Image URL",
"op.news.fieldBranch": "Branch",
"op.news.allBranches": "All branches",
"op.news.published": "Published",
"op.news.publishAt": "Show from",
"op.news.expiresAt": "Hide after",
"op.news.save": "Save",
"op.news.cancel": "Cancel",
"op.news.edit": "Edit",
"op.news.delete": "Delete",
"op.news.empty": "No news yet",
"op.news.draftTag": "draft",
"op.news.errorRequired": "Title and text are required",
"op.news.errorWindow": "Start must be earlier than end",
```

`locales/tg.json`:
```json
"op.news.nav": "Хабарҳо",
"op.news.title": "Хабарҳо",
"op.news.fieldTitle": "Сарлавҳа",
"op.news.fieldBody": "Матн",
"op.news.fieldImage": "URL-и сурат",
"op.news.fieldBranch": "Филиал",
"op.news.allBranches": "Ҳамаи филиалҳо",
"op.news.published": "Нашр шуд",
"op.news.publishAt": "Нишон додан аз",
"op.news.expiresAt": "Пинҳон кардан баъд аз",
"op.news.save": "Нигоҳ доштан",
"op.news.cancel": "Бекор кардан",
"op.news.edit": "Тағйир",
"op.news.delete": "Нест кардан",
"op.news.empty": "Ҳоло хабар нест",
"op.news.draftTag": "сиёҳнавис",
"op.news.errorRequired": "Сарлавҳа ва матн ҳатмӣ",
"op.news.errorWindow": "Санаи оғоз бояд пеш аз санаи анҷом бошад",
```

- [ ] **Step 2: Regenerate the message types**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `packages/i18n/src/messages.ts` updated with the new keys; exit 0.

- [ ] **Step 3: Add the WorkspaceId**

In `src/AFK4.Operator.App.Web/src/operatorTypes.ts`, append `| 'news'` to the `WorkspaceId` union (last member after `'loyalty'`).

- [ ] **Step 4: Add the nav item**

In `src/AFK4.Operator.App.Web/src/operatorData.ts`:
- add `Newspaper` to the `lucide-react` import (keep alphabetical: it goes between `Monitor` and `ReceiptText`);
- append to `navItems` (after the loyalty entry — this keeps it index-aligned with `workspaceIds`):
```ts
  { labelKey: 'op.news.nav', icon: Newspaper }
```

- [ ] **Step 5: Add permission + workspace id + rule**

In `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`:
- append `'news'` to the `workspaceIds` array (after `'loyalty'`);
- add to `permissionNames` (after `manageLoyaltySettings`):
```ts
  manageNews: 'news.manage'
```
- add to `workspacePermissionRules` (after the `loyalty` entry):
```ts
  news: [permissionNames.manageNews]
```

- [ ] **Step 6: Wire the render block + side-panel exclusion**

In `src/AFK4.Operator.App.Web/src/App.tsx`:
- import the workspace:
```tsx
import { NewsWorkspace } from './NewsWorkspace';
```
- add the render block next to the loyalty one:
```tsx
    {workspace === 'news' && backendContext !== null && (
      <NewsWorkspace backend={backendContext} />
    )}
```
- extend the side-panel exclusion expression with `&& workspace !== 'news'` (so `SummarySidePanel` is suppressed for news, matching loyalty).

- [ ] **Step 7: Add the SummarySidePanel title entry**

In `src/AFK4.Operator.App.Web/src/SummarySidePanel.tsx`, add to the `title` lookup map (after the `loyalty` entry):
```tsx
    news: t('op.news.title'),
```

- [ ] **Step 8: Run the full operator suite + build**

Run:
```bash
cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build
```
Expected: all bun tests PASS (incl. NewsWorkspace), build succeeds. Also run i18n: `cd packages/i18n && /home/fedya/.bun/bin/bun test` — PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Operator.App.Web/src locales packages/i18n/src/messages.ts
git commit -m "feat(operator): wire NewsWorkspace nav + permissions + i18n"
```

---

## Final verification (after all tasks)

- [ ] **Server:** `dotnet test tests/AFK4.Platform.Api.Tests` — all green.
- [ ] **Shell:** `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build` — all green.
- [ ] **Operator:** `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build` — all green.
- [ ] **i18n:** `cd packages/i18n && /home/fedya/.bun/bin/bun test` — all green.
- [ ] Dispatch a final code-review subagent over the whole branch, then use `superpowers:finishing-a-development-branch`.

## Self-review notes (spec coverage)

- Org-wide owner authorship + optional `BranchId` target → N1 (DTOs), N3 (`BranchId` filter), N4 (owner endpoints), N10 (branch dropdown). ✓
- Title + body + optional image URL, no CTA → N1/N2 fields, N3 validation, N7/N10 render (no links). ✓
- `IsPublished` + optional `PublishAtUtc`/`ExpiresAtUtc`, query-time filter → N2 columns, N3 `ListForPlayerAsync`. ✓
- Player "Новости" menu → list, no read/unread → N7, N8. ✓
- Validation (empty, lengths, scheme, inverted window, foreign branch) → N3 `ValidateAsync` + tests; org-scoped 404 → N3/N4. ✓
- Audit on writes, Owner-only permission → N4. ✓
- Player visibility incl. no-`HomeBranchId` → N3 test + N5 endpoint (`Guid.Empty` → null). ✓
- Hard delete → N3 `DeleteAsync`. ✓
