# B2-1 Backend (Floor-plan geometry) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a spatial floor model (seat coordinates/type/rotation, zone geometry, walls) to the Platform API so the operator «План» view and editor (B2-2/B2-3) have data to render and save.

**Architecture:** Extend the existing floor-map stack — entities (`SeatEntity`/`ZoneEntity` + new `WallEntity`), shared contracts (`SeatStatusDto`/`FloorMapDto`/`FloorMapBulkUpdateRequest`), the optimistic-concurrency `FloorMapEtag`, and the read/edit services — additively. All new columns are nullable / defaulted so existing clubs read fine and fall back to the abstract grid. The bulk-update endpoint already exists and uses full-replace semantics; walls follow the same replace pattern.

**Tech Stack:** C# / .NET 10, EF Core + PostgreSQL (InMemory provider for tests), xUnit. Migration via `dotnet ef` on Linux (see env-quirks recipe).

**Spec:** `docs/superpowers/specs/2026-06-16-operator-map-plan-editor-design.md` (section «Бэкенд (B1-геометрия)»).

---

## File Structure

**Modify:**
- `src/AFK4.Platform.Api/Data/SeatEntity.cs` — add `PosX/PosY/Rotation/SeatType`.
- `src/AFK4.Platform.Api/Data/ZoneEntity.cs` — add `GeoX/GeoY/GeoWidth/GeoHeight/Color/ZoneType`.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — `DbSet<WallEntity>`, model config for new columns + walls table.
- `src/AFK4.Shared.Contracts/FloorMap/SeatStatusDto.cs` — add geometry fields.
- `src/AFK4.Shared.Contracts/FloorMap/FloorMapDto.cs` — zone geometry + `Walls` list + `FloorMapWallDto`.
- `src/AFK4.Shared.Contracts/FloorMap/FloorMapBulkUpdateRequest.cs` — geometry on seat/zone requests + `Walls`.
- `src/AFK4.Platform.Api/FloorMap/FloorMapEtag.cs` — fold geometry + walls into the hash.
- `src/AFK4.Platform.Api/FloorMap/EfFloorMapReadService.cs` — read + project geometry + walls.
- `src/AFK4.Platform.Api/FloorMap/EfFloorMapEditService.cs` — persist geometry + replace walls.

**Create:**
- `src/AFK4.Platform.Api/Data/WallEntity.cs` — new entity.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddFloorPlanGeometry.cs` (+ `.Designer.cs`) — generated.
- `tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs` — DTO/request serialization round-trips.

**Extend (existing test files):**
- `tests/AFK4.Platform.Api.Tests/EfFloorMapReadServiceTests.cs` — geometry + walls read-back.
- `tests/AFK4.Platform.Api.Tests/FloorMapBulkUpdateEndpointTests.cs` — geometry + walls save/replace + tenant isolation. (If the test better fits a service-level file, add it here; this file already exercises bulk update.)

**Conventions confirmed from the codebase:**
- Tables are lowercased (`zones`, `seats`); entities map via `OnModelCreating` blocks (`PlatformDbContext.cs:303-317`).
- Tenant scope on Seat/Zone is `OrganizationId + BranchId`; walls mirror this.
- `SeatStatusDto` is a positional record whose tail params have defaults — new fields append at the tail with defaults (back-compat for serialization + call sites).
- Bulk-update request records are positional without defaults; new fields append at the tail **with defaults** so existing construction sites (tests) keep compiling.

---

## Task 1: Add geometry fields to entities

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/SeatEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/ZoneEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/WallEntity.cs`

- [ ] **Step 1: Add coordinate/type fields to `SeatEntity`**

Insert after `SortOrder` (line 15), before `CreatedAtUtc`:

```csharp
    // Floor-plan layout (null until the branch is arranged in the «План» editor; grid view ignores these).
    public int? PosX { get; set; }

    public int? PosY { get; set; }

    public int Rotation { get; set; }

    public string SeatType { get; set; } = "pc";
```

- [ ] **Step 2: Add geometry fields to `ZoneEntity`**

Insert after `SortOrder` (line 13), before `CreatedAtUtc`:

```csharp
    // Floor-plan rectangle in grid cells (null until arranged).
    public int? GeoX { get; set; }

    public int? GeoY { get; set; }

    public int? GeoWidth { get; set; }

    public int? GeoHeight { get; set; }

    public string? Color { get; set; }

    public string? ZoneType { get; set; }
```

- [ ] **Step 3: Create `WallEntity`**

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class WallEntity
{
    public Guid WallId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public int X1 { get; set; }

    public int Y1 { get; set; }

    public int X2 { get; set; }

    public int Y2 { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

- [ ] **Step 4: Build to confirm entities compile**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/SeatEntity.cs src/AFK4.Platform.Api/Data/ZoneEntity.cs src/AFK4.Platform.Api/Data/WallEntity.cs
git commit -m "feat(platform): add floor-plan geometry fields to seat/zone + wall entity"
```

---

## Task 2: Register walls + new columns in the DbContext

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EfFloorMapReadServiceTests.cs` (round-trip added in Task 5; here we only wire the model)

- [ ] **Step 1: Add the `Walls` DbSet**

After the `Seats` DbSet (`PlatformDbContext.cs:29`), add:

```csharp
    public DbSet<WallEntity> Walls => Set<WallEntity>();
```

- [ ] **Step 2: Configure the new seat/zone columns + walls table in `OnModelCreating`**

Replace the Zone and Seat config blocks (`PlatformDbContext.cs:303-317`) with:

```csharp
        modelBuilder.Entity<ZoneEntity>(entity =>
        {
            entity.ToTable("zones");
            entity.HasKey(zone => zone.ZoneId);
            entity.Property(zone => zone.Name).HasMaxLength(120).IsRequired();
            entity.Property(zone => zone.Color).HasMaxLength(32);
            entity.Property(zone => zone.ZoneType).HasMaxLength(32);
            entity.HasIndex(zone => new { zone.OrganizationId, zone.BranchId, zone.SortOrder });
        });

        modelBuilder.Entity<SeatEntity>(entity =>
        {
            entity.ToTable("seats");
            entity.HasKey(seat => seat.SeatId);
            entity.Property(seat => seat.Name).HasMaxLength(80).IsRequired();
            entity.Property(seat => seat.SeatType).HasMaxLength(32).HasDefaultValue("pc").IsRequired();
            entity.Property(seat => seat.Rotation).HasDefaultValue(0);
            entity.HasIndex(seat => new { seat.OrganizationId, seat.BranchId, seat.ZoneId, seat.SortOrder });
        });

        modelBuilder.Entity<WallEntity>(entity =>
        {
            entity.ToTable("walls");
            entity.HasKey(wall => wall.WallId);
            entity.HasIndex(wall => new { wall.OrganizationId, wall.BranchId });
        });
```

- [ ] **Step 3: Build to confirm the model compiles**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PlatformDbContext.cs
git commit -m "feat(platform): register walls table and floor-plan columns in the db context"
```

---

## Task 3: Extend the shared contracts

**Files:**
- Modify: `src/AFK4.Shared.Contracts/FloorMap/SeatStatusDto.cs`
- Modify: `src/AFK4.Shared.Contracts/FloorMap/FloorMapDto.cs`
- Modify: `src/AFK4.Shared.Contracts/FloorMap/FloorMapBulkUpdateRequest.cs`
- Test: `tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs`

- [ ] **Step 1: Write the failing contract test**

Create `tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorPlanGeometryContractTests
{
    [Fact]
    public void SeatStatusDto_RoundTripsGeometryFields()
    {
        var seat = new SeatStatusDto(
            SeatId: Guid.NewGuid(),
            SeatName: "PC-001",
            ZoneId: Guid.NewGuid(),
            ZoneName: "Main Hall",
            SortOrder: 10,
            State: "Free",
            DeviceId: null,
            DeviceName: null,
            IsDeviceOnline: null,
            IsDeviceLocked: null,
            LastHeartbeatAtUtc: null,
            AgentVersion: null,
            ShellVersion: null,
            ActiveSessionId: null,
            RemainingSeconds: null,
            PosX: 3,
            PosY: 5,
            Rotation: 90,
            SeatType: "console");

        var roundTripped = JsonSerializer.Deserialize<SeatStatusDto>(JsonSerializer.Serialize(seat))!;

        Assert.Equal(3, roundTripped.PosX);
        Assert.Equal(5, roundTripped.PosY);
        Assert.Equal(90, roundTripped.Rotation);
        Assert.Equal("console", roundTripped.SeatType);
    }

    [Fact]
    public void FloorMapDto_RoundTripsZoneGeometryAndWalls()
    {
        var dto = new FloorMapDto(Guid.NewGuid(), "Branch", [])
        {
            Zones =
            [
                new FloorMapZoneDto(Guid.NewGuid(), "VIP", 1)
                {
                    GeoX = 1, GeoY = 2, GeoWidth = 4, GeoHeight = 3, Color = "#22c55e", ZoneType = "lounge"
                }
            ],
            Walls = [new FloorMapWallDto(Guid.NewGuid(), 0, 0, 10, 0)]
        };

        var roundTripped = JsonSerializer.Deserialize<FloorMapDto>(JsonSerializer.Serialize(dto))!;

        var zone = Assert.Single(roundTripped.Zones);
        Assert.Equal(4, zone.GeoWidth);
        Assert.Equal("lounge", zone.ZoneType);
        var wall = Assert.Single(roundTripped.Walls);
        Assert.Equal(10, wall.X2);
    }

    [Fact]
    public void BulkUpdateRequest_DefaultsWallsToEmptyAndGeometryToNull()
    {
        var request = new FloorMapBulkUpdateRequest(
            Guid.NewGuid(),
            [new FloorMapBulkZoneRequest(null, "z1", "Hall", 1)],
            [new FloorMapBulkSeatRequest(null, "s1", "z1", "PC-1", 1)]);

        Assert.Null(request.Walls);
        Assert.Null(request.Seats[0].PosX);
        Assert.Equal("pc", request.Seats[0].SeatType);
        Assert.Equal(0, request.Seats[0].Rotation);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FloorPlanGeometryContractTests`
Expected: FAIL — compile errors (`PosX`, `FloorMapWallDto`, `Walls` not defined).

- [ ] **Step 3: Add geometry fields to `SeatStatusDto`**

Append to the positional record parameter list, after `SessionStartedAtUtc` (`SeatStatusDto.cs:33`):

```csharp
    DateTimeOffset? SessionStartedAtUtc = null,
    // Floor-plan layout: grid cell + orientation + host type. Null/default until the branch is
    // arranged in the «План» editor (B2); the abstract grid view ignores these.
    int? PosX = null,
    int? PosY = null,
    int Rotation = 0,
    string SeatType = "pc");
```

(Remove the old closing `);` from the previous last line.)

- [ ] **Step 4: Extend `FloorMapDto.cs` with zone geometry + walls**

Replace the file body with:

```csharp
namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<SeatStatusDto> Seats)
{
    public IReadOnlyList<FloorMapZoneDto> Zones { get; init; } = [];

    public IReadOnlyList<FloorMapWallDto> Walls { get; init; } = [];
}

public sealed record FloorMapZoneDto(
    Guid ZoneId,
    string Name,
    int SortOrder)
{
    public int? GeoX { get; init; }

    public int? GeoY { get; init; }

    public int? GeoWidth { get; init; }

    public int? GeoHeight { get; init; }

    public string? Color { get; init; }

    public string? ZoneType { get; init; }
}

public sealed record FloorMapWallDto(
    Guid WallId,
    int X1,
    int Y1,
    int X2,
    int Y2);
```

- [ ] **Step 5: Extend `FloorMapBulkUpdateRequest.cs`**

Replace the file body with:

```csharp
namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapBulkUpdateRequest(
    Guid OrganizationId,
    IReadOnlyList<FloorMapBulkZoneRequest> Zones,
    IReadOnlyList<FloorMapBulkSeatRequest> Seats,
    IReadOnlyList<FloorMapBulkWallRequest>? Walls = null);

public sealed record FloorMapBulkZoneRequest(
    Guid? ZoneId,
    string ClientId,
    string Name,
    int SortOrder,
    int? GeoX = null,
    int? GeoY = null,
    int? GeoWidth = null,
    int? GeoHeight = null,
    string? Color = null,
    string? ZoneType = null);

public sealed record FloorMapBulkSeatRequest(
    Guid? SeatId,
    string ClientId,
    string ZoneClientId,
    string Name,
    int SortOrder,
    int? PosX = null,
    int? PosY = null,
    int Rotation = 0,
    string SeatType = "pc");

public sealed record FloorMapBulkWallRequest(
    int X1,
    int Y1,
    int X2,
    int Y2);
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FloorPlanGeometryContractTests`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/FloorMap tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs
git commit -m "feat(contracts): add floor-plan geometry + walls to floor-map dtos and bulk request"
```

---

## Task 4: Fold geometry + walls into the ETag

The ETag drives optimistic concurrency (If-Match). If geometry/walls are not hashed, two managers editing layout concurrently won't get a 412 — the second silently overwrites. So the hash must cover them.

**Files:**
- Modify: `src/AFK4.Platform.Api/FloorMap/FloorMapEtag.cs`
- Modify: `src/AFK4.Platform.Api/FloorMap/EfFloorMapReadService.cs:139` (call site)
- Modify: `src/AFK4.Platform.Api/FloorMap/EfFloorMapEditService.cs:60,241` (call sites)
- Test: `tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs` (add etag cases)

- [ ] **Step 1: Write the failing ETag test**

Append to `FloorPlanGeometryContractTests.cs` (add `using AFK4.Platform.Api.Data;` and `using AFK4.Platform.Api.FloorMap;` to the file's usings):

```csharp
    [Fact]
    public void Etag_ChangesWhenSeatPositionChanges()
    {
        var seat = new SeatEntity { SeatId = Guid.NewGuid(), Name = "PC", ZoneId = Guid.NewGuid(), SortOrder = 1 };
        var before = FloorMapEtag.Compute([], [seat], []);

        seat.PosX = 7;
        var after = FloorMapEtag.Compute([], [seat], []);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Etag_ChangesWhenWallAdded()
    {
        var before = FloorMapEtag.Compute([], [], []);
        var after = FloorMapEtag.Compute([], [], [new WallEntity { WallId = Guid.NewGuid(), X2 = 5 }]);

        Assert.NotEqual(before, after);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FloorPlanGeometryContractTests`
Expected: FAIL — `Compute` has no 3-arg overload.

- [ ] **Step 3: Extend `FloorMapEtag.Compute`**

Replace the method signature and body in `FloorMapEtag.cs`. Change the signature to take walls, and append geometry to each line:

```csharp
    public static string Compute(
        IEnumerable<ZoneEntity> zones,
        IEnumerable<SeatEntity> seats,
        IEnumerable<WallEntity> walls)
    {
        var builder = new StringBuilder();
        foreach (var zone in zones.OrderBy(zone => zone.ZoneId))
        {
            builder.Append("z|")
                .Append(zone.ZoneId.ToString("D"))
                .Append('|')
                .Append(zone.SortOrder.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(zone.Name)
                .Append('|')
                .Append(Coord(zone.GeoX)).Append(',').Append(Coord(zone.GeoY)).Append(',')
                .Append(Coord(zone.GeoWidth)).Append(',').Append(Coord(zone.GeoHeight))
                .Append('|')
                .Append(zone.Color ?? string.Empty)
                .Append('|')
                .Append(zone.ZoneType ?? string.Empty)
                .Append('\n');
        }

        foreach (var seat in seats.OrderBy(seat => seat.SeatId))
        {
            builder.Append("s|")
                .Append(seat.SeatId.ToString("D"))
                .Append('|')
                .Append(seat.ZoneId.ToString("D"))
                .Append('|')
                .Append(seat.SortOrder.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(seat.Name)
                .Append('|')
                .Append(Coord(seat.PosX)).Append(',').Append(Coord(seat.PosY)).Append(',')
                .Append(seat.Rotation.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(seat.SeatType)
                .Append('\n');
        }

        foreach (var wall in walls.OrderBy(wall => wall.WallId))
        {
            builder.Append("w|")
                .Append(wall.WallId.ToString("D"))
                .Append('|')
                .Append(wall.X1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.Y1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.X2.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(wall.Y2.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "\"" + Convert.ToHexString(hashBytes).ToLowerInvariant() + "\"";
    }

    private static string Coord(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
```

- [ ] **Step 4: Update the read-service call site**

In `EfFloorMapReadService.cs`, walls are loaded in Task 5. For now (to keep the build green between tasks), update line 139 to pass an empty wall list:

```csharp
        return new FloorMapReadResult(dto, FloorMapEtag.Compute(zones, seats, []));
```

(Task 5 replaces `[]` with the real `walls` list.)

- [ ] **Step 5: Update the edit-service call sites**

In `EfFloorMapEditService.cs`, walls are handled in Task 6. For now pass empty lists at both call sites (lines 60 and 241):

```csharp
        var currentEtag = FloorMapEtag.Compute(existingZones, existingSeats, []);
```
```csharp
        var freshEtag = FloorMapEtag.Compute(freshZones, freshSeats, []);
```

(Task 6 replaces `[]` with the real wall lists.)

- [ ] **Step 6: Run tests + build**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FloorPlanGeometryContractTests`
Expected: PASS (5 tests).
Run: `dotnet build src/AFK4.Platform.Api`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/FloorMap/FloorMapEtag.cs src/AFK4.Platform.Api/FloorMap/EfFloorMapReadService.cs src/AFK4.Platform.Api/FloorMap/EfFloorMapEditService.cs tests/AFK4.Platform.Api.Tests/FloorPlanGeometryContractTests.cs
git commit -m "feat(platform): fold floor-plan geometry and walls into the floor-map etag"
```

---

## Task 5: Read service projects geometry + walls

**Files:**
- Modify: `src/AFK4.Platform.Api/FloorMap/EfFloorMapReadService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EfFloorMapReadServiceTests.cs`

- [ ] **Step 1: Write the failing read test**

Add to `EfFloorMapReadServiceTests.cs`:

```csharp
    [Fact]
    public async Task GetFloorMapAsync_ProjectsSeatGeometryZoneGeometryAndWalls()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");

        await using (var db = new PlatformDbContext(options))
        {
            db.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Branch",
                CreatedAtUtc = now
            });
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = zoneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "VIP",
                SortOrder = 1,
                GeoX = 1, GeoY = 2, GeoWidth = 4, GeoHeight = 3,
                Color = "#22c55e", ZoneType = "lounge",
                CreatedAtUtc = now
            });
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-1",
                SortOrder = 1,
                PosX = 3, PosY = 5, Rotation = 90, SeatType = "console",
                CreatedAtUtc = now
            });
            db.Walls.Add(new WallEntity
            {
                WallId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                X1 = 0, Y1 = 0, X2 = 10, Y2 = 0,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfFloorMapReadService(db);
            var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

            Assert.NotNull(result);
            var seat = Assert.Single(result!.FloorMap.Seats);
            Assert.Equal(3, seat.PosX);
            Assert.Equal(5, seat.PosY);
            Assert.Equal(90, seat.Rotation);
            Assert.Equal("console", seat.SeatType);

            var zone = Assert.Single(result.FloorMap.Zones);
            Assert.Equal(4, zone.GeoWidth);
            Assert.Equal("lounge", zone.ZoneType);

            var wall = Assert.Single(result.FloorMap.Walls);
            Assert.Equal(10, wall.X2);
        }
    }
```

> Note: confirm the `FloorMapReadResult` property name for the DTO (the file returns `new FloorMapReadResult(dto, etag)`). If the property is not `FloorMap`, match the record's actual first member — check `IFloorMapReadService.cs`.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter GetFloorMapAsync_ProjectsSeatGeometry`
Expected: FAIL — `seat.PosX` is null, `zone.GeoWidth` is null, `result.FloorMap.Walls` is empty.

- [ ] **Step 3: Load walls and project geometry**

In `EfFloorMapReadService.cs`, after the seats load (`seats = await ... line 50-53`), add a walls load:

```csharp
        var walls = await dbContext.Walls
            .AsNoTracking()
            .Where(wall => wall.BranchId == branchId)
            .ToListAsync(cancellationToken);
```

In `CreateSeatStatus`'s returned `SeatStatusDto` (after `SessionStartedAtUtc: activeSession?.StartedAtUtc`), add:

```csharp
            SessionStartedAtUtc: activeSession?.StartedAtUtc,
            PosX: seat.PosX,
            PosY: seat.PosY,
            Rotation: seat.Rotation,
            SeatType: seat.SeatType);
```

Replace the `zoneStatuses` projection (`lines 122-129`) to carry geometry:

```csharp
        var zoneStatuses = zones
            .OrderBy(zone => zone.SortOrder)
            .ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
            .Select(zone => new FloorMapZoneDto(
                ZoneId: zone.ZoneId,
                Name: zone.Name,
                SortOrder: zone.SortOrder)
            {
                GeoX = zone.GeoX,
                GeoY = zone.GeoY,
                GeoWidth = zone.GeoWidth,
                GeoHeight = zone.GeoHeight,
                Color = zone.Color,
                ZoneType = zone.ZoneType
            })
            .ToList();
        var wallStatuses = walls
            .OrderBy(wall => wall.WallId)
            .Select(wall => new FloorMapWallDto(wall.WallId, wall.X1, wall.Y1, wall.X2, wall.Y2))
            .ToList();
```

Update the `FloorMapDto` initializer (`lines 131-137`) to include walls, and the etag call (`line 139`) to pass real walls:

```csharp
        var dto = new FloorMapDto(
            BranchId: branch.BranchId,
            BranchName: branch.Name,
            Seats: seatStatuses)
        {
            Zones = zoneStatuses,
            Walls = wallStatuses
        };

        return new FloorMapReadResult(dto, FloorMapEtag.Compute(zones, seats, walls));
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter GetFloorMapAsync_ProjectsSeatGeometry`
Expected: PASS.

- [ ] **Step 5: Run the full read-service suite to confirm no regressions**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter EfFloorMapReadServiceTests`
Expected: PASS (all).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/FloorMap/EfFloorMapReadService.cs tests/AFK4.Platform.Api.Tests/EfFloorMapReadServiceTests.cs
git commit -m "feat(platform): project seat/zone geometry and walls from the floor-map read service"
```

---

## Task 6: Edit service persists geometry + replaces walls

The bulk update is full-replace. Seats/zones already upsert by id; we add geometry assignment. Walls have no client id — they are replaced wholesale: delete all existing branch walls, insert the request's walls.

**Files:**
- Modify: `src/AFK4.Platform.Api/FloorMap/EfFloorMapEditService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/FloorMapBulkUpdateEndpointTests.cs`

- [ ] **Step 1: Write the failing edit test**

Add to `FloorMapBulkUpdateEndpointTests.cs` a service-level test (mirror the file's existing DbContext setup pattern; if it only has endpoint-level helpers, construct `EfFloorMapEditService` directly as below):

```csharp
    [Fact]
    public async Task BulkUpdateAsync_PersistsSeatGeometryZoneGeometryAndReplacesWalls()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");
        var timeProvider = new FakeTimeProvider(now); // if unavailable, use TimeProvider.System

        await using (var seed = new PlatformDbContext(options))
        {
            seed.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Branch",
                CreatedAtUtc = now
            });
            seed.Walls.Add(new WallEntity
            {
                WallId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                X1 = 99, Y1 = 99, X2 = 99, Y2 = 99,
                CreatedAtUtc = now
            });
            await seed.SaveChangesAsync();
        }

        string etag;
        await using (var db = new PlatformDbContext(options))
        {
            etag = FloorMapEtag.Compute(
                await db.Zones.ToListAsync(),
                await db.Seats.ToListAsync(),
                await db.Walls.ToListAsync());
        }

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            [new FloorMapBulkZoneRequest(null, "z1", "VIP", 1, GeoX: 1, GeoY: 2, GeoWidth: 4, GeoHeight: 3, Color: "#22c55e", ZoneType: "lounge")],
            [new FloorMapBulkSeatRequest(null, "s1", "z1", "PC-1", 1, PosX: 3, PosY: 5, Rotation: 90, SeatType: "console")],
            [new FloorMapBulkWallRequest(0, 0, 10, 0)]);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfFloorMapEditService(db, timeProvider);
            var result = await service.BulkUpdateAsync(TestIds.OrganizationId, TestIds.BranchId, etag, request, CancellationToken.None);
            Assert.Equal(FloorMapBulkUpdateStatus.Success, result.Status);
        }

        await using (var db = new PlatformDbContext(options))
        {
            var seat = await db.Seats.SingleAsync();
            Assert.Equal(3, seat.PosX);
            Assert.Equal("console", seat.SeatType);
            var zone = await db.Zones.SingleAsync();
            Assert.Equal(4, zone.GeoWidth);
            Assert.Equal("lounge", zone.ZoneType);
            var walls = await db.Walls.ToListAsync();
            var wall = Assert.Single(walls); // old (99,99) wall replaced
            Assert.Equal(10, wall.X2);
        }
    }
```

> Note: match the existing `FloorMapBulkUpdateEndpointTests` setup for `TestIds`, the time provider type, and whether tests go through the endpoint or the service directly. Reuse the file's existing helpers if present rather than the literal scaffold above.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter BulkUpdateAsync_PersistsSeatGeometry`
Expected: FAIL — geometry not persisted, old wall still present.

- [ ] **Step 3: Assign geometry on zones**

In `EfFloorMapEditService.cs`, in the zone loop, for the update branch (after `zone.SortOrder = zoneRequest.SortOrder;`, line 126) and the create branch (in the `new ZoneEntity { ... }` initializer, after `SortOrder = zoneRequest.SortOrder,`), set:

For the update branch add after line 126:
```csharp
                zone.GeoX = zoneRequest.GeoX;
                zone.GeoY = zoneRequest.GeoY;
                zone.GeoWidth = zoneRequest.GeoWidth;
                zone.GeoHeight = zoneRequest.GeoHeight;
                zone.Color = zoneRequest.Color;
                zone.ZoneType = zoneRequest.ZoneType;
```

For the create branch, add inside the initializer:
```csharp
                    GeoX = zoneRequest.GeoX,
                    GeoY = zoneRequest.GeoY,
                    GeoWidth = zoneRequest.GeoWidth,
                    GeoHeight = zoneRequest.GeoHeight,
                    Color = zoneRequest.Color,
                    ZoneType = zoneRequest.ZoneType,
```

- [ ] **Step 4: Assign geometry on seats**

In the seat loop, update branch (after `seat.SortOrder = seatRequest.SortOrder;`, line 165):
```csharp
                seat.PosX = seatRequest.PosX;
                seat.PosY = seatRequest.PosY;
                seat.Rotation = seatRequest.Rotation;
                seat.SeatType = seatRequest.SeatType;
```

Create branch, inside the `new SeatEntity { ... }` initializer (after `SortOrder = seatRequest.SortOrder,`):
```csharp
                    PosX = seatRequest.PosX,
                    PosY = seatRequest.PosY,
                    Rotation = seatRequest.Rotation,
                    SeatType = seatRequest.SeatType,
```

- [ ] **Step 5: Replace walls and feed the etag**

Just before `await dbContext.SaveChangesAsync(cancellationToken);` (line 231), add wall replacement:

```csharp
        var existingWalls = await dbContext.Walls
            .Where(wall => wall.OrganizationId == organizationId && wall.BranchId == branchId)
            .ToListAsync(cancellationToken);
        if (existingWalls.Count > 0)
        {
            dbContext.Walls.RemoveRange(existingWalls);
        }

        var requestedWalls = request.Walls ?? [];
        foreach (var wallRequest in requestedWalls)
        {
            dbContext.Walls.Add(new WallEntity
            {
                WallId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                X1 = wallRequest.X1,
                Y1 = wallRequest.Y1,
                X2 = wallRequest.X2,
                Y2 = wallRequest.Y2,
                CreatedAtUtc = now
            });
        }
```

Update the pre-check etag (line 60) and fresh etag (line 241) to use real walls:

```csharp
        var currentEtag = FloorMapEtag.Compute(existingZones, existingSeats, existingWallsForEtag);
```

where, right after `existingSeats` is loaded (line 56-58), add:
```csharp
        var existingWallsForEtag = await dbContext.Walls
            .AsNoTracking()
            .Where(wall => wall.OrganizationId == organizationId && wall.BranchId == branchId)
            .ToListAsync(cancellationToken);
```

And the fresh read after save (after line 240), add and use:
```csharp
        var freshWalls = await dbContext.Walls
            .AsNoTracking()
            .Where(wall => wall.OrganizationId == organizationId && wall.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var freshEtag = FloorMapEtag.Compute(freshZones, freshSeats, freshWalls);
```

> Implementation note: `existingWallsForEtag` (AsNoTracking, for the concurrency check) is separate from `existingWalls` (tracked, for RemoveRange). Keep both — the tracked load is what EF deletes.

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter BulkUpdateAsync_PersistsSeatGeometry`
Expected: PASS.

- [ ] **Step 7: Add a tenant-isolation test for walls**

Add to the same file:

```csharp
    [Fact]
    public async Task BulkUpdateAsync_DoesNotTouchAnotherBranchWalls()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");
        var otherBranchId = Guid.NewGuid();
        var otherWallId = Guid.NewGuid();

        await using (var seed = new PlatformDbContext(options))
        {
            seed.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Name = "A", CreatedAtUtc = now });
            seed.Walls.Add(new WallEntity { WallId = otherWallId, OrganizationId = TestIds.OrganizationId, BranchId = otherBranchId, X2 = 7, CreatedAtUtc = now });
            await seed.SaveChangesAsync();
        }

        string etag;
        await using (var db = new PlatformDbContext(options))
        {
            etag = FloorMapEtag.Compute(
                await db.Zones.Where(z => z.BranchId == TestIds.BranchId).ToListAsync(),
                await db.Seats.Where(s => s.BranchId == TestIds.BranchId).ToListAsync(),
                await db.Walls.Where(w => w.BranchId == TestIds.BranchId).ToListAsync());
        }

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            [new FloorMapBulkZoneRequest(null, "z1", "Hall", 1)],
            [],
            [new FloorMapBulkWallRequest(0, 0, 1, 1)]);

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfFloorMapEditService(db, TimeProvider.System);
            var result = await service.BulkUpdateAsync(TestIds.OrganizationId, TestIds.BranchId, etag, request, CancellationToken.None);
            Assert.Equal(FloorMapBulkUpdateStatus.Success, result.Status);
        }

        await using (var db = new PlatformDbContext(options))
        {
            Assert.True(await db.Walls.AnyAsync(w => w.WallId == otherWallId));
        }
    }
```

- [ ] **Step 8: Run the full floor-map test suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FloorMap`
Expected: PASS (all floor-map tests).

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Platform.Api/FloorMap/EfFloorMapEditService.cs tests/AFK4.Platform.Api.Tests/FloorMapBulkUpdateEndpointTests.cs
git commit -m "feat(platform): persist floor-plan geometry and replace walls in the edit service"
```

---

## Task 7: EF migration

⚠️ `dotnet ef` traps (env-quirks): build the API FIRST (a stale `--no-build` model yields an EMPTY migration); to undo an unapplied migration just `rm` the two files (never let it connect to DB via `migrations remove`).

**Files:**
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddFloorPlanGeometry.cs` (+ `.Designer.cs`)
- Modify (auto): `src/AFK4.Platform.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`

- [ ] **Step 1: Build the API so the model is fresh**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: Build succeeded.

- [ ] **Step 2: Generate the migration**

Run:
```bash
dotnet ef migrations add AddFloorPlanGeometry \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations \
  --no-build
```
Expected: creates `<timestamp>_AddFloorPlanGeometry.cs` + `.Designer.cs`, updates the snapshot.

- [ ] **Step 3: Verify the migration is NOT empty**

Run: `git status --short src/AFK4.Platform.Api/Data/Migrations`
Open the new `_AddFloorPlanGeometry.cs`. Confirm `Up()` contains:
- `AddColumn` for `PosX`, `PosY`, `Rotation`, `SeatType` on `seats`;
- `AddColumn` for `GeoX/GeoY/GeoWidth/GeoHeight/Color/ZoneType` on `zones`;
- `CreateTable` for `walls` (+ its index).

If `Up()`/`Down()` are blank → the model was stale. `rm` both new files, redo Step 1 then Step 2.

- [ ] **Step 4: Build + run the full Platform.Api test suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (existing ~1162 + new floor-plan tests; no regressions).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/Migrations
git commit -m "feat(platform): EF migration for floor-plan geometry columns and walls table"
```

---

## Task 8: Deploy gate (manual — at merge time, not before)

⚠️ This is a **deploy runbook step**, executed when the PR is ready to merge — NOT during implementation. The `Coolify Staging Deploy` workflow blocks any commit touching `Data/Migrations/**` until the migration is applied by hand and the workflow is re-dispatched with `confirm_migrations_applied=true`.

- [ ] **Step 1: Apply the migration to staging by hand** following the verified runbook in memory `afk4-env-quirks` («Coolify staging deploy + EF migration runbook»): publish the DB port (PATCH `is_public:true` + **restart**), `pg_dump` backup, `dotnet ef migrations script --idempotent` (rebuild API first), apply via docker `postgres:17-alpine` psql `-v ON_ERROR_STOP=1 -f`, verify the new columns + `walls` table + `__EFMigrationsHistory` row, then PATCH `is_public:false` + restart to close the port.
- [ ] **Step 2: Re-dispatch the deploy** `gh workflow run coolify-staging-deploy.yml --ref main -f confirm_migrations_applied=true`; poll the run; confirm `curl https://afk4.staging.mubi.dev/api/health` → `{"status":"ok"}`.
- [ ] **Step 3: Clean up** the /tmp dump (root-owned → `sudo rm`) and any cred JSON; keep the bearer token in process env only.

---

## Self-Review

- **Spec coverage:** entities (Task 1), DbContext (Task 2), contracts incl. walls (Task 3), etag concurrency (Task 4), read projection (Task 5), edit persistence + tenant isolation (Task 6), migration (Task 7), deploy gate (Task 8). Spec's "contract-tests on DTO serialization" → Task 3; "edit-service save/read of full layout + wall tenant isolation" → Task 6. Covered.
- **Type consistency:** `FloorMapEtag.Compute(zones, seats, walls)` — 3 args used consistently in Tasks 4/5/6. `FloorMapWallDto(WallId, X1, Y1, X2, Y2)` and `FloorMapBulkWallRequest(X1, Y1, X2, Y2)` (no id on the request — walls are replaced wholesale). `request.Walls` is nullable (`?? []`).
- **Open verification flagged inline:** `FloorMapReadResult`'s DTO property name (Task 5 note) and `FloorMapBulkUpdateEndpointTests` setup/time-provider helpers (Task 6 note) — match the actual code when implementing.
