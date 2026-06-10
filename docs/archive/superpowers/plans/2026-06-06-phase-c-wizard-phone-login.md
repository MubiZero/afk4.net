# Phase C — Wizard login by phone + password Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a staff member install a PC in the Setup Wizard by signing in with their phone + password (Phase B `sign-in-by-phone`), then enroll through authenticated install endpoints; the 8-digit owner code stays as a fallback.

**Architecture:** Add a parallel, authenticated install path. Backend: new `/api/install/auth/{discover,seats,enroll}` endpoints that resolve org/branch/staff from the opaque-token `StaffContext` and gate on the existing `devices.install` permission; the post-resolution enrollment/seat/branch-listing logic is **extracted from `EfInstallService` and shared** with the owner-code path (no duplication). Wizard host (.NET): the `SetupWizardWebHostBridge` gains `wizard:phoneSignIn` (holds the returned `AccessToken` in a private field) plus authenticated `wizard:{discoverAuth,createSeatAuth,enrollAuth}` ops that attach `Authorization: Bearer`. Wizard web (React): a new `PhoneLoginScreen` becomes step 1 with a "войти по коду владельца" fallback link to the unchanged `OwnerCodeScreen`; the PC display name is tightened to 3–32 chars. The owner-code endpoints and screen are untouched.

**Tech Stack:** C# minimal API (`AFK4.Platform.Api`, recently split into `Endpoints/`), EF Core (InMemory in tests, `WebApplicationFactory<Program>`), `AFK4.Shared.Contracts` records; WPF + WebView2 host (`AFK4.SetupWizard` / `AFK4.SetupWizard.Core`, net10.0-windows) tested with a `RecordingHandler` `HttpMessageHandler`; React 19 + TS + Vite (`AFK4.SetupWizard.Web`) tested with `bun test` + happy-dom + `@testing-library/react`; i18n via `locales/{ru,en,tg}.json` → `bun run gen` in `packages/i18n`.

---

## File Structure

**Backend — `src/AFK4.Platform.Api` + `src/AFK4.Shared.Contracts`:**
- Create: `src/AFK4.Shared.Contracts/Install/AuthenticatedInstallRequests.cs` — `AuthenticatedInstallCreateSeatRequest`, `AuthenticatedInstallEnrollRequest` (no `OwnerCode`).
- Modify: `src/AFK4.Platform.Api/Install/IInstallService.cs` — 3 new interface methods + relax `InstallOperationResult<T>` factories to `Guid? ownerCodeId`.
- Modify: `src/AFK4.Platform.Api/Install/EfInstallService.cs` — extract `EnrollResolvedAsync`, `CreateSeatResolvedAsync`, `BuildBranchDtoAsync`; add `DiscoverForStaffAsync`, `CreateSeatForStaffAsync`, `EnrollForStaffAsync`; tighten display name to 3–32.
- Modify: `src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs` — 3 new authenticated endpoints in `MapDeviceEndpoints`.
- Test: `tests/AFK4.Platform.Api.Tests/AuthenticatedInstallEndpointTests.cs` (new), plus fixes in `InstallEndpointTests.cs` if the 3–32 change breaks an assertion.

**Wizard host — `src/AFK4.SetupWizard.Core` + `src/AFK4.SetupWizard`:**
- Modify: `src/AFK4.SetupWizard.Core/SetupWizardContracts.cs` — extend `ISetupWizardApiClient` with 4 methods.
- Modify: `src/AFK4.SetupWizard.Core/SetupWizardApiClient.cs` — implement the 4 methods (Bearer header).
- Modify: `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs` — token field, 4 new ops, payload records, error codes.
- Modify: `src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs` — fake the 4 methods.
- Test: `tests/AFK4.SetupWizard.Tests/SetupWizardApiClientTests.cs` — new cases; update `RecordingSetupWizardApiClient` in `SetupWizardViewModelTests.cs` to implement new members.

**Wizard web — `src/AFK4.SetupWizard.Web`:**
- Create: `src/AFK4.SetupWizard.Web/bunfig.toml`, `src/AFK4.SetupWizard.Web/test-setup.ts`.
- Create: `src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.tsx` + `src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.test.tsx`.
- Modify: `src/AFK4.SetupWizard.Web/src/wizardApi.ts` — `signInByPhone`, `discoverAuthenticated`, `WizardInstallClient` (+ `ownerCodeInstallClient`/`authenticatedInstallClient`).
- Modify: `src/AFK4.SetupWizard.Web/src/App.tsx`, `Stepper.tsx`, `DeviceScreen.tsx`, `OwnerCodeScreen.tsx`.
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` → regenerate `packages/i18n/src/messages.ts`.

**Sequencing:** Part 1 (backend) → Part 2 (wizard host) → Part 3 (wizard web). The web depends on the host ops; the host depends on the contracts + endpoints.

---

# Part 1 — Backend (authenticated install path)

### Task 1: Authenticated install request contracts

**Files:**
- Create: `src/AFK4.Shared.Contracts/Install/AuthenticatedInstallRequests.cs`

- [ ] **Step 1: Create the contracts file**

```csharp
namespace AFK4.Shared.Contracts.Install;

/// <summary>Create-seat request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.</summary>
public sealed record AuthenticatedInstallCreateSeatRequest(
    Guid BranchId,
    Guid ZoneId,
    string Name);

/// <summary>Device-enroll request for the authenticated (phone sign-in) install path. Org/staff come from the bearer token, so there is no owner code.</summary>
public sealed record AuthenticatedInstallEnrollRequest(
    Guid BranchId,
    Guid? SeatId,
    string Role,
    string DisplayName,
    string MachineName,
    string DevicePublicKey);
```

- [ ] **Step 2: Build the contracts project**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Install/AuthenticatedInstallRequests.cs
git commit -m "feat(contracts): add authenticated install request DTOs"
```

---

### Task 2: Relax `InstallOperationResult` factories for the codeless path

The `Success`/`Conflict` factories currently require a non-null `Guid ownerCodeId`. The authenticated path has no owner code, so make it nullable. This is source-compatible (existing callers pass a `Guid`).

**Files:**
- Modify: `src/AFK4.Platform.Api/Install/IInstallService.cs:31-51`

- [ ] **Step 1: Change `ownerCodeId` to `Guid?` in `Success` and `Conflict`**

Replace the two factory methods (lines 31-51) with:

```csharp
    public static InstallOperationResult<T> Success(
        T value,
        Guid organizationId,
        Guid? branchId,
        Guid? ownerCodeId,
        Guid? staffUserId = null) =>
        new(InstallOperationStatus.Succeeded, value, null, organizationId, branchId, ownerCodeId, staffUserId);

    public static InstallOperationResult<T> BadRequest(
        string error,
        Guid? ownerCodeId = null,
        Guid? organizationId = null,
        Guid? branchId = null,
        Guid? staffUserId = null) =>
        new(InstallOperationStatus.BadRequest, default, error, organizationId, branchId, ownerCodeId, staffUserId);

    public static InstallOperationResult<T> NotFound(string error, Guid? ownerCodeId = null) =>
        new(InstallOperationStatus.NotFound, default, error, OwnerCodeId: ownerCodeId);

    public static InstallOperationResult<T> Conflict(string error, Guid organizationId, Guid branchId, Guid? ownerCodeId) =>
        new(InstallOperationStatus.Conflict, default, error, organizationId, branchId, ownerCodeId);
```

- [ ] **Step 2: Build to confirm no regressions**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Install/IInstallService.cs
git commit -m "refactor(install): allow null ownerCodeId on operation results"
```

---

### Task 3: Extract the shared enrollment core (`EnrollResolvedAsync`) and tighten display name to 3–32

Extract everything after org/owner-code resolution (tenant-active check → branch → validations → persist) into a private method keyed on a resolved `organizationId` + a nullable `enrolledViaOwnerCodeId`. Owner-code `EnrollAsync` delegates to it and records owner-code brute-force failures on any non-success result.

> **Intentional, immaterial behavior change:** the owner-code path previously did NOT increment the owner-code failure counter when the *tenant was inactive*; now it does (any non-success increments). This is harmless (a real failed attempt). If a test asserts the old behavior, update it.

**Files:**
- Modify: `src/AFK4.Platform.Api/Install/EfInstallService.cs:25` (constants) and `:108-323` (EnrollAsync)

- [ ] **Step 1: Add display-name bounds constants**

At `src/AFK4.Platform.Api/Install/EfInstallService.cs:25`, replace:

```csharp
    private const int MaxDisplayNameLength = 80;
```

with:

```csharp
    private const int MinDisplayNameLength = 3;
    private const int MaxDisplayNameLength = 32;
```

- [ ] **Step 2: Replace `EnrollAsync` (lines 108-323) with a thin wrapper + extracted core**

```csharp
    public async Task<InstallOperationResult<InstallEnrollResponse>> EnrollAsync(
        InstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = await ownerCodeService.LookupActiveAsync(request.OwnerCode, cancellationToken);
        if (!lookup.Succeeded)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                lookup.Error ?? "Owner code is invalid.",
                lookup.OwnerCodeId);
        }

        var organizationId = lookup.OrganizationId!.Value;
        var ownerCodeId = lookup.OwnerCodeId!.Value;

        var result = await EnrollResolvedAsync(
            organizationId,
            enrolledViaOwnerCodeId: ownerCodeId,
            request.BranchId,
            request.SeatId,
            request.Role,
            request.DisplayName,
            request.MachineName,
            request.DevicePublicKey,
            cancellationToken);

        if (!result.Succeeded)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
        }

        return result;
    }

    public async Task<InstallOperationResult<InstallEnrollResponse>> EnrollForStaffAsync(
        Guid organizationId,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        return await EnrollResolvedAsync(
            organizationId,
            enrolledViaOwnerCodeId: null,
            request.BranchId,
            request.SeatId,
            request.Role,
            request.DisplayName,
            request.MachineName,
            request.DevicePublicKey,
            cancellationToken);
    }

    private async Task<InstallOperationResult<InstallEnrollResponse>> EnrollResolvedAsync(
        Guid organizationId,
        Guid? enrolledViaOwnerCodeId,
        Guid branchId,
        Guid? requestedSeatId,
        string requestedRole,
        string? requestedDisplayName,
        string requestedMachineName,
        string requestedDevicePublicKey,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Tenant is not active.",
                enrolledViaOwnerCodeId);
        }

        var branch = await dbContext.Branches
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
                cancellationToken);
        if (branch is null)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Branch was not found.",
                enrolledViaOwnerCodeId);
        }

        var normalizedRole = requestedRole.Trim();
        if (!IsValidDeviceRole(normalizedRole))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device role is invalid.",
                enrolledViaOwnerCodeId);
        }

        var requiresSeatAssignment = normalizedRole == DeviceRoleNames.GamingPc;

        var machineName = requestedMachineName.Trim();
        if (machineName.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Machine name is required.",
                enrolledViaOwnerCodeId);
        }

        if (machineName.Length > MaxMachineNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Machine name must be {MaxMachineNameLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        var providedDisplayName = (requestedDisplayName ?? string.Empty).Trim();
        if (providedDisplayName.Length > 0 && providedDisplayName.Length < MinDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be at least {MinDisplayNameLength} characters.",
                enrolledViaOwnerCodeId);
        }

        var displayName = providedDisplayName.Length == 0 ? machineName : providedDisplayName;
        if (displayName.Length > MaxDisplayNameLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Display name must be {MaxDisplayNameLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        var devicePublicKey = requestedDevicePublicKey.Trim();
        if (devicePublicKey.Length == 0)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Device public key is required.",
                enrolledViaOwnerCodeId);
        }

        if (devicePublicKey.Length > MaxDevicePublicKeyLength)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                $"Device public key must be {MaxDevicePublicKeyLength} characters or fewer.",
                enrolledViaOwnerCodeId);
        }

        if (requiresSeatAssignment && (requestedSeatId is null || requestedSeatId == Guid.Empty))
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Seat is required for gaming PC enrollment.",
                enrolledViaOwnerCodeId);
        }

        if (!requiresSeatAssignment && requestedSeatId is not null && requestedSeatId != Guid.Empty)
        {
            return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                "Manager workstation enrollment must not target a seat.",
                enrolledViaOwnerCodeId);
        }

        SeatEntity? seat = null;
        if (requiresSeatAssignment)
        {
            var seatId = requestedSeatId!.Value;
            seat = await dbContext.Seats
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.OrganizationId == organizationId &&
                        candidate.BranchId == branchId &&
                        candidate.SeatId == seatId,
                    cancellationToken);
            if (seat is null)
            {
                return InstallOperationResult<InstallEnrollResponse>.BadRequest(
                    "Seat was not found in this branch.",
                    enrolledViaOwnerCodeId);
            }

            var hasActiveAssignment = await dbContext.DeviceSeatAssignments
                .AnyAsync(
                    assignment =>
                        assignment.OrganizationId == organizationId &&
                        assignment.BranchId == branchId &&
                        assignment.SeatId == seatId &&
                        assignment.DetachedAtUtc == null,
                    cancellationToken);
            if (hasActiveAssignment)
            {
                return InstallOperationResult<InstallEnrollResponse>.Conflict(
                    "Seat already has an active device assignment.",
                    organizationId,
                    branchId,
                    enrolledViaOwnerCodeId);
            }
        }

        var now = timeProvider.GetUtcNow();
        var deviceId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var credentialSecret = DeviceCredentialSecrets.CreateCredentialSecret();
        var enrollmentState = branch.RequireManualDeviceApproval
            ? DeviceEnrollmentStateNames.Pending
            : DeviceEnrollmentStateNames.Approved;
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = machineName,
            DisplayName = displayName,
            DevicePublicKey = devicePublicKey,
            Role = normalizedRole,
            EnrollmentState = enrollmentState,
            EnrolledViaOwnerCodeId = enrolledViaOwnerCodeId,
            AgentVersion = string.Empty,
            ShellVersion = string.Empty,
            EnrolledAtUtc = now
        });

        dbContext.DeviceCredentials.Add(new DeviceCredentialEntity
        {
            CredentialId = credentialId,
            OrganizationId = organizationId,
            BranchId = branchId,
            DeviceId = deviceId,
            SecretHash = DeviceCredentialSecrets.HashSecret(credentialSecret),
            CreatedAtUtc = now
        });

        if (requiresSeatAssignment)
        {
            dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                SeatId = seat!.SeatId,
                DeviceId = deviceId,
                AttachedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new InstallEnrollResponse(
            organizationId,
            branchId,
            deviceId,
            credentialId,
            credentialSecret,
            enrollmentState,
            options.Value.ApiBaseUrl.TrimEnd('/'),
            options.Value.UpdateChannel,
            now)
        {
            LeaseSigningPublicKeyPem = ResolveLeaseSigningPublicKeyPem(),
            UpdatePackageSigningPublicKeyPem = options.Value.UpdatePackageSigningPublicKeyPem
        };

        return InstallOperationResult<InstallEnrollResponse>.Success(
            response,
            organizationId,
            branchId,
            enrolledViaOwnerCodeId);
    }
```

- [ ] **Step 3: Add `using AFK4.Shared.Contracts.Install;` is already present** — confirm the file's `using` block (top of `EfInstallService.cs`) includes `AFK4.Shared.Contracts.Install` (it does, line 8). `AuthenticatedInstallEnrollRequest` resolves from it.

- [ ] **Step 4: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors. (`EnrollForStaffAsync` is not yet on the interface — that is fine; it's a public method on the class. It is added to the interface in Task 6.)

- [ ] **Step 5: Run install tests, fix display-name fallout**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Install"`
Expected: PASS. If any test enrolls with a display name >32 chars or asserts the old "80 characters" message, update that test data to ≤32 / the new message — the 3–32 bound is the intended product rule (spec §2, §5 Phase C).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Install/EfInstallService.cs
git commit -m "refactor(install): share enroll core, tighten display name to 3-32"
```

---

### Task 4: Extract `CreateSeatResolvedAsync` + `CreateSeatForStaffAsync`

**Files:**
- Modify: `src/AFK4.Platform.Api/Install/EfInstallService.cs:325-462` (CreateSeatAsync)

- [ ] **Step 1: Replace `CreateSeatAsync` (lines 325-462) with a wrapper + extracted core**

```csharp
    public async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatAsync(
        InstallCreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = await ownerCodeService.LookupActiveAsync(request.OwnerCode, cancellationToken);
        if (!lookup.Succeeded)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                lookup.Error ?? "Owner code is invalid.",
                lookup.OwnerCodeId);
        }

        var organizationId = lookup.OrganizationId!.Value;
        var ownerCodeId = lookup.OwnerCodeId!.Value;
        var staffUserId = lookup.StaffUserId!.Value;

        var result = await CreateSeatResolvedAsync(
            organizationId,
            request.BranchId,
            request.ZoneId,
            request.Name,
            ownerCodeId: ownerCodeId,
            staffUserId: staffUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            await RecordResolvedOwnerCodeFailureAsync(ownerCodeId, cancellationToken);
        }

        return result;
    }

    public async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatForStaffAsync(
        Guid organizationId,
        Guid? staffUserId,
        AuthenticatedInstallCreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        return await CreateSeatResolvedAsync(
            organizationId,
            request.BranchId,
            request.ZoneId,
            request.Name,
            ownerCodeId: null,
            staffUserId: staffUserId,
            cancellationToken);
    }

    private async Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatResolvedAsync(
        Guid organizationId,
        Guid branchId,
        Guid zoneId,
        string name,
        Guid? ownerCodeId,
        Guid? staffUserId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Tenant is not active.",
                ownerCodeId,
                organizationId,
                staffUserId: staffUserId);
        }

        var branchExists = await dbContext.Branches.AnyAsync(
            branch => branch.OrganizationId == organizationId && branch.BranchId == branchId,
            cancellationToken);
        if (!branchExists)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Branch was not found.",
                ownerCodeId,
                organizationId,
                staffUserId: staffUserId);
        }

        var zoneExists = await dbContext.Zones.AnyAsync(
            zone =>
                zone.OrganizationId == organizationId &&
                zone.BranchId == branchId &&
                zone.ZoneId == zoneId,
            cancellationToken);
        if (!zoneExists)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Zone was not found for this branch.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        var seatName = name.Trim();
        if (seatName.Length == 0)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                "Seat name is required.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        if (seatName.Length > MaxSeatNameLength)
        {
            return InstallOperationResult<InstallCreateSeatResponse>.BadRequest(
                $"Seat name must be {MaxSeatNameLength} characters or fewer.",
                ownerCodeId,
                organizationId,
                branchId,
                staffUserId);
        }

        var normalizedName = seatName.ToUpperInvariant();
        var existingSeat = await dbContext.Seats.SingleOrDefaultAsync(
            seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == branchId &&
                seat.ZoneId == zoneId &&
                seat.Name.ToUpper() == normalizedName,
            cancellationToken);
        if (existingSeat is not null)
        {
            var existingResponse = new InstallCreateSeatResponse(
                existingSeat.OrganizationId,
                existingSeat.BranchId,
                existingSeat.ZoneId,
                existingSeat.SeatId,
                existingSeat.Name,
                existingSeat.SortOrder);
            return InstallOperationResult<InstallCreateSeatResponse>.Success(
                existingResponse,
                organizationId,
                branchId,
                ownerCodeId,
                staffUserId);
        }

        var nextSortOrder = await dbContext.Seats
            .Where(seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == branchId &&
                seat.ZoneId == zoneId)
            .Select(seat => (int?)seat.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        var now = timeProvider.GetUtcNow();
        var created = new SeatEntity
        {
            SeatId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            ZoneId = zoneId,
            Name = seatName,
            SortOrder = nextSortOrder + 1,
            CreatedAtUtc = now
        };
        dbContext.Seats.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new InstallCreateSeatResponse(
            created.OrganizationId,
            created.BranchId,
            created.ZoneId,
            created.SeatId,
            created.Name,
            created.SortOrder);
        return InstallOperationResult<InstallCreateSeatResponse>.Success(
            response,
            organizationId,
            branchId,
            ownerCodeId,
            staffUserId);
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run install tests**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Install"`
Expected: PASS. (If a test asserts the old "Branch was not found for this owner code." message, update it to "Branch was not found.".)

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Install/EfInstallService.cs
git commit -m "refactor(install): share create-seat core for authenticated path"
```

---

### Task 5: Extract `BuildBranchDtoAsync` + add `DiscoverForStaffAsync`

`DiscoverForStaffAsync` returns the org's branches **filtered to the staff's assigned `BranchIds`** (there is no separate "org-wide" concept in the auth model — an owner is assigned to their branches via role assignments; the owner code stays as fallback for any gap). It reuses the floor-map build loop, extracted to `BuildBranchDtoAsync`.

**Files:**
- Modify: `src/AFK4.Platform.Api/Install/EfInstallService.cs` — `DiscoverAsync` (lines 29-106) loop extraction + new `DiscoverForStaffAsync`

- [ ] **Step 1: Extract the per-branch DTO build into `BuildBranchDtoAsync`**

In `DiscoverAsync`, replace the `foreach (var branch in branches) { ... }` block (lines 73-98) with:

```csharp
        var branchDtos = new List<InstallBranchDto>(branches.Count);
        foreach (var branch in branches)
        {
            branchDtos.Add(await BuildBranchDtoAsync(branch, cancellationToken));
        }
```

Then add this private helper (place it next to `DiscoverAsync`):

```csharp
    private async Task<InstallBranchDto> BuildBranchDtoAsync(BranchEntity branch, CancellationToken cancellationToken)
    {
        var floorMapResult = await floorMapReadService.GetFloorMapAsync(branch.BranchId, cancellationToken);
        var floorMap = floorMapResult?.FloorMap ?? new FloorMapDto(branch.BranchId, branch.Name, []);

        var occupiedSeatIds = floorMap.Seats
            .Where(seat => seat.DeviceId is not null)
            .Select(seat => seat.SeatId)
            .ToHashSet();
        var freeSeatIds = floorMap.Seats
            .Where(seat => !occupiedSeatIds.Contains(seat.SeatId))
            .Select(seat => seat.SeatId)
            .ToArray();

        return new InstallBranchDto(
            branch.BranchId,
            branch.Slug,
            branch.Name,
            floorMap,
            freeSeatIds);
    }
```

> If `BranchEntity` is not already in scope, it is in `AFK4.Platform.Api.Data` (same namespace family used by `dbContext.Branches`); no new `using` is needed since the file already queries `dbContext.Branches`.

- [ ] **Step 2: Add `DiscoverForStaffAsync` (place after `DiscoverAsync`)**

```csharp
    public async Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverForStaffAsync(
        Guid organizationId,
        IReadOnlySet<Guid> branchIds,
        string ownerDisplayName,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null || organization.Status != TenantStatusNames.Active)
        {
            return InstallOperationResult<InstallDiscoverResponse>.BadRequest(
                "Tenant is not active.",
                organizationId: organizationId);
        }

        var allowedBranchIds = branchIds.ToArray();
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && allowedBranchIds.Contains(branch.BranchId))
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);

        var branchDtos = new List<InstallBranchDto>(branches.Count);
        foreach (var branch in branches)
        {
            branchDtos.Add(await BuildBranchDtoAsync(branch, cancellationToken));
        }

        var response = new InstallDiscoverResponse(ownerDisplayName, branchDtos);
        return InstallOperationResult<InstallDiscoverResponse>.Success(
            response,
            organizationId,
            branchId: null,
            ownerCodeId: null);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run install tests**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Install"`
Expected: PASS (owner-code discover behavior unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Install/EfInstallService.cs
git commit -m "feat(install): add DiscoverForStaffAsync, share branch DTO builder"
```

---

### Task 6: Add the 3 service methods to `IInstallService`

**Files:**
- Modify: `src/AFK4.Platform.Api/Install/IInstallService.cs:5-18`

- [ ] **Step 1: Add the interface members**

Inside `public interface IInstallService { ... }`, after the existing 3 methods, add:

```csharp
    Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverForStaffAsync(
        Guid organizationId,
        IReadOnlySet<Guid> branchIds,
        string ownerDisplayName,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatForStaffAsync(
        Guid organizationId,
        Guid? staffUserId,
        AuthenticatedInstallCreateSeatRequest request,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallEnrollResponse>> EnrollForStaffAsync(
        Guid organizationId,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken);
```

- [ ] **Step 2: Build the whole API project**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors (the `EfInstallService` already implements these public methods from Tasks 3-5).

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Install/IInstallService.cs
git commit -m "feat(install): expose staff-authenticated install methods on interface"
```

---

### Task 7: Add the authenticated install endpoints

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs` — insert after the `/api/install/seats` handler (after line 286)

- [ ] **Step 1: Add the three endpoints**

Insert immediately after the `/api/install/seats` `app.MapPost(...)` block (ends at line 286):

```csharp
        app.MapPost("/api/install/auth/discover", (
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.InstallDevice);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }
            if (!authorization.IsAllowed)
            {
                return Task.FromResult(Results.StatusCode(StatusCodes.Status403Forbidden));
            }

            var staff = authorization.StaffContext!;
            return installService
                .DiscoverForStaffAsync(staff.OrganizationId, staff.BranchIds, staff.DisplayName, cancellationToken)
                .ContinueWith(task => ToInstallHttpResult(task.Result), cancellationToken);
        });

        app.MapPost("/api/install/auth/seats", async (
            AuthenticatedInstallCreateSeatRequest request,
            HttpContext httpContext,
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                request.BranchId, StaffPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }
            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var sourceIp = GetSourceIp(httpContext);
            var result = await installService.CreateSeatForStaffAsync(
                staff.OrganizationId, staff.StaffUserId, request, cancellationToken);
            if (result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    result.OrganizationId!.Value,
                    result.BranchId!.Value,
                    staff.StaffUserId,
                    AuditActionNames.CreateSeat,
                    "Seat",
                    result.Value!.SeatId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { request.ZoneId, request.Name, SourceIp = sourceIp, Via = "phone_auth_install" },
                    cancellationToken);
            }

            return ToInstallHttpResult(result);
        });

        app.MapPost("/api/install/auth/enroll", async (
            AuthenticatedInstallEnrollRequest request,
            HttpContext httpContext,
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                request.BranchId, StaffPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var sourceIp = GetSourceIp(httpContext);
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    request.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.InstallEnrollRejected,
                    "Device",
                    null,
                    AuditOutcome.Denied,
                    new { request.Role, authorization.DenialReason, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var result = await installService.EnrollForStaffAsync(staff.OrganizationId, request, cancellationToken);
            if (result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    result.OrganizationId!.Value,
                    result.BranchId!.Value,
                    staff.StaffUserId,
                    AuditActionNames.InstallEnrollSucceeded,
                    "Device",
                    result.Value!.DeviceId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { request.SeatId, request.Role, request.DisplayName, result.Value.EnrollmentState, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
            }
            else
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    staff.OrganizationId,
                    request.BranchId,
                    staff.StaffUserId,
                    AuditActionNames.InstallEnrollRejected,
                    "Device",
                    null,
                    AuditOutcome.Denied,
                    new { request.BranchId, request.SeatId, request.Role, result.Error, Via = "phone_auth_install", SourceIp = sourceIp },
                    cancellationToken);
            }

            return ToInstallHttpResult(result);
        });
```

> **Note on the discover handler return type:** the other two handlers are `async` and return `IResult`; the discover handler is not `async` because `RequireOrganizationPermission` is synchronous. To keep types uniform and avoid the `.ContinueWith` awkwardness, prefer making it `async` instead:
>
> ```csharp
> app.MapPost("/api/install/auth/discover", async (
>     StaffAuthorizationService authorizationService,
>     IInstallService installService,
>     CancellationToken cancellationToken) =>
> {
>     var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.InstallDevice);
>     if (!authorization.IsAuthenticated) return Results.Unauthorized();
>     if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
>     var staff = authorization.StaffContext!;
>     var result = await installService.DiscoverForStaffAsync(staff.OrganizationId, staff.BranchIds, staff.DisplayName, cancellationToken);
>     return ToInstallHttpResult(result);
> });
> ```
> Use this `async` version (delete the `.ContinueWith` variant above).

- [ ] **Step 2: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors. (`StaffPermissionNames`, `StaffAuthorizationService`, `AuthenticatedInstall*Request`, `WriteAuditAsync`, `ToInstallHttpResult`, `GetSourceIp`, `AuditActionNames`, `AuditOutcome` are all already imported in `DeviceEndpoints.cs`.)

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs
git commit -m "feat(install): add authenticated /api/install/auth endpoints"
```

---

### Task 8: Integration tests for the authenticated install path

Mirror `InstallEndpointTests` + `StaffSignInByPhoneEndpointTests`. Use `PlatformApiFactory` + `StaffAuthTestHelper.AuthorizeAsAsync` (which seeds a staff user with a branch role assignment and sets the Bearer header).

**Files:**
- Create: `tests/AFK4.Platform.Api.Tests/AuthenticatedInstallEndpointTests.cs`
- Read first (to match seeding/floor-map helpers): `tests/AFK4.Platform.Api.Tests/InstallEndpointTests.cs`, `tests/AFK4.Platform.Api.Tests/StaffAuthTestHelper.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using AFK4.Platform.Api.Data;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class AuthenticatedInstallEndpointTests
{
    [Fact]
    public async Task AuthDiscover_AsTechnician_ReturnsAssignedBranches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory); // reuse the same layout seeding InstallEndpointTests uses

        var response = await client.PostAsync("/api/install/auth/discover", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallDiscoverResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Branches);
    }

    [Fact]
    public async Task AuthDiscover_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/install/auth/discover", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthEnroll_GamingPc_AutoApproval_CreatesDeviceCredentialAndSeatAssignment()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var (branchId, seatId) = await FirstBranchAndFreeSeatAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                branchId,
                seatId,
                "GamingPc",
                "Стенд 12",
                "WIN-INSTALL-01",
                "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(d => d.DeviceId == body!.DeviceId);
        Assert.Null(device.EnrolledViaOwnerCodeId);
        Assert.True(await db.DeviceSeatAssignments.AnyAsync(a => a.DeviceId == body!.DeviceId));
    }

    [Fact]
    public async Task AuthEnroll_DisplayNameTooShort_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var (branchId, _) = await FirstBranchAndFreeSeatAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                branchId, null, "ManagerWorkstation", "ab",
                "WIN-INSTALL-02", "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthEnroll_WithoutInstallPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // CashierOperator does NOT have devices.install (see PermissionCatalog).
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory);
        var (branchId, seatId) = await FirstBranchAndFreeSeatAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                branchId, seatId, "GamingPc", "Стенд 1",
                "WIN-INSTALL-03", "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // SeedLayoutAsync + FirstBranchAndFreeSeatAsync: copy/adapt from InstallEndpointTests
    // so the seeded branch matches the StaffAuthTestHelper org/branch (TestIds.BranchId).
    private static Task SeedLayoutAsync(PlatformApiFactory factory) => InstallTestData.SeedLayoutAsync(factory);
    private static Task<(System.Guid BranchId, System.Guid SeatId)> FirstBranchAndFreeSeatAsync(PlatformApiFactory factory)
        => InstallTestData.FirstBranchAndFreeSeatAsync(factory);
}
```

> **Step 1 note:** `InstallEndpointTests` already has working layout/seat seeding. If that seeding is private to that class, extract it into a small `InstallTestData` static helper in the test project (or inline the same EF seeding here, scoped to `TestIds.BranchId` so it lines up with the org/branch that `StaffAuthTestHelper` seeds). The key constraint: the staff user's role assignment `BranchId` must equal the branch you seed seats into, or `RequireBranchPermissionAsync` denies (correctly). Read `StaffAuthTestHelper.cs` for `TestIds`.

- [ ] **Step 2: Run, expect failures, then confirm green**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AuthenticatedInstall"`
Expected: all 5 PASS once seeding lines up. The 403 test pins the permission gate; the `EnrolledViaOwnerCodeId == null` assertion pins the codeless path.

- [ ] **Step 3: Run the full backend suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS (baseline was 1055/1055; new tests add to the count).

- [ ] **Step 4: Commit**

```bash
git add tests/AFK4.Platform.Api.Tests/
git commit -m "test(install): cover authenticated install discover/enroll/permission gate"
```

---

# Part 2 — Wizard host (.NET bridge + API client)

### Task 9: Extend `ISetupWizardApiClient` + implement Bearer-authenticated methods

**Files:**
- Modify: `src/AFK4.SetupWizard.Core/SetupWizardContracts.cs:28-40`
- Modify: `src/AFK4.SetupWizard.Core/SetupWizardApiClient.cs`
- Modify: `tests/AFK4.SetupWizard.Tests/SetupWizardViewModelTests.cs` (the `RecordingSetupWizardApiClient` fake)

- [ ] **Step 1: Add interface members**

In `SetupWizardContracts.cs`, add `using AFK4.Shared.Contracts.Identity;` at the top, and inside `public interface ISetupWizardApiClient { ... }` add:

```csharp
    Task<StaffSignInResponse> SignInByPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken);

    Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken);

    Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken);
```

- [ ] **Step 2: Implement in `SetupWizardApiClient`**

In `SetupWizardApiClient.cs`, add usings `using System.Net.Http.Headers;` and `using AFK4.Shared.Contracts.Identity;`, then add inside the class:

```csharp
    public async Task<StaffSignInResponse> SignInByPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(phoneNumber, password),
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StaffSignInResponse>(response, cancellationToken);
    }

    public async Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/discover");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallDiscoverResponse>(response, cancellationToken);
    }

    public async Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/seats")
        {
            Content = JsonContent.Create(
                new AuthenticatedInstallCreateSeatRequest(branchId, zoneId, name),
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallCreateSeatResponse>(response, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/enroll")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallEnrollResponse>(response, cancellationToken);
    }
```

- [ ] **Step 3: Make the `RecordingSetupWizardApiClient` fake implement the new members**

In `tests/AFK4.SetupWizard.Tests/SetupWizardViewModelTests.cs`, add to the `RecordingSetupWizardApiClient` (or whichever class implements `ISetupWizardApiClient`) — minimal stubs sufficient to compile (the ViewModel tests don't exercise the phone path):

```csharp
    public Task<StaffSignInResponse> SignInByPhoneAsync(string phoneNumber, string password, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(string accessToken, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException();
```

Add `using AFK4.Shared.Contracts.Identity;` and `using AFK4.Shared.Contracts.Install;` to that test file if not present.

- [ ] **Step 4: Build the core + tests projects**

Run: `dotnet build src/AFK4.SetupWizard.Core/AFK4.SetupWizard.Core.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.SetupWizard.Core/ tests/AFK4.SetupWizard.Tests/SetupWizardViewModelTests.cs
git commit -m "feat(setup-wizard): add phone sign-in + authenticated install API client methods"
```

---

### Task 10: API client tests (POST paths + Bearer header)

**Files:**
- Modify: `tests/AFK4.SetupWizard.Tests/SetupWizardApiClientTests.cs`

- [ ] **Step 1: Write failing tests**

Using the existing `RecordingHandler` + `JsonResponse<T>` + `CreateClient` helpers in that file, add:

```csharp
    [Fact]
    public async Task SignInByPhoneAsync_PostsToPhoneEndpoint_ReturnsToken()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), Guid.NewGuid(), "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInByPhoneAsync("+992 93 738-00-70", "Passw0rd!", CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/sign-in-by-phone", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task EnrollAuthenticatedAsync_AttachesBearerToken_PostsToAuthEnroll()
    {
        var expected = new InstallEnrollResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "secret",
            "Approved", "https://api", "stable", DateTimeOffset.UnixEpoch);
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        await client.EnrollAuthenticatedAsync(
            "access-123",
            new AuthenticatedInstallEnrollRequest(Guid.NewGuid(), Guid.NewGuid(), "GamingPc", "Стенд 5", "WIN-1", "pem"),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/install/auth/enroll", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-123", request.Headers.Authorization.Parameter);
    }
```

Add `using AFK4.Shared.Contracts.Identity;` to the test file if missing.

- [ ] **Step 2: Run**

Run: `dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj --filter "FullyQualifiedName~SetupWizardApiClient"`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/AFK4.SetupWizard.Tests/SetupWizardApiClientTests.cs
git commit -m "test(setup-wizard): cover phone sign-in + bearer-authenticated enroll client"
```

---

### Task 11: Bridge — `wizard:phoneSignIn` + authenticated ops

**Files:**
- Modify: `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs`

- [ ] **Step 1: Add a token field + dispatch cases**

Add `using AFK4.Shared.Contracts.Identity;` to the top of the file.

After the `JsonOptions` field (line 20), add:

```csharp
    private string? accessToken;
```

In the `HandleAsync` switch (lines 44-50), add four cases:

```csharp
            object payload = request.Type switch
            {
                "wizard:discover" => await DiscoverAsync(request.Payload, cancellationToken),
                "wizard:createSeat" => await CreateSeatAsync(request.Payload, cancellationToken),
                "wizard:enroll" => await EnrollAsync(request.Payload, cancellationToken),
                "wizard:phoneSignIn" => await PhoneSignInAsync(request.Payload, cancellationToken),
                "wizard:discoverAuth" => await DiscoverAuthenticatedAsync(cancellationToken),
                "wizard:createSeatAuth" => await CreateSeatAuthenticatedAsync(request.Payload, cancellationToken),
                "wizard:enrollAuth" => await EnrollAuthenticatedAsync(request.Payload, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported host bridge request: {request.Type}.")
            };
```

- [ ] **Step 2: Add the four handler methods**

Add after `EnrollAsync` (after line 163):

```csharp
    private async Task<WizardPhoneSignInResult> PhoneSignInAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardPhoneSignInPayload>(payload);
        var phone = (request.Phone ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (phone.Length == 0 || password.Length == 0)
        {
            throw new InvalidOperationException("Phone and password are required.");
        }

        var response = await apiClient.SignInByPhoneAsync(phone, password, cancellationToken);
        accessToken = response.AccessToken;
        return new WizardPhoneSignInResult(response.DisplayName);
    }

    private string RequireAccessToken() =>
        string.IsNullOrEmpty(accessToken)
            ? throw new InvalidOperationException("Sign in with your phone before continuing.")
            : accessToken;

    private async Task<WizardDiscoverResult> DiscoverAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.DiscoverAuthenticatedAsync(RequireAccessToken(), cancellationToken);
        var branches = response.Branches
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapBranch)
            .ToArray();
        return new WizardDiscoverResult(response.OwnerDisplayName, branches);
    }

    private async Task<WizardSeat> CreateSeatAuthenticatedAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardCreateSeatAuthPayload>(payload);
        var branchId = ParseGuid(request.BranchId, nameof(request.BranchId));
        var zoneId = ParseGuid(request.ZoneId, nameof(request.ZoneId));
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Seat name is required.");
        }

        var created = await apiClient.CreateSeatAuthenticatedAsync(
            RequireAccessToken(), branchId, zoneId, name, cancellationToken);
        return new WizardSeat(
            created.SeatId,
            created.Name,
            created.ZoneId,
            ZoneName: request.ZoneName ?? string.Empty,
            created.SortOrder,
            Status: "Free",
            DeviceId: null,
            DeviceName: null,
            IsOnline: null);
    }

    private async Task<WizardEnrollResult> EnrollAuthenticatedAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardEnrollAuthPayload>(payload);
        var branchId = ParseGuid(request.BranchId, nameof(request.BranchId));
        var role = (request.Role ?? string.Empty).Trim();
        if (role is not (DeviceRoleNames.GamingPc or DeviceRoleNames.ManagerWorkstation))
        {
            throw new InvalidOperationException("Role must be GamingPc or ManagerWorkstation.");
        }

        Guid? seatId = null;
        if (role == DeviceRoleNames.GamingPc)
        {
            if (string.IsNullOrWhiteSpace(request.SeatId))
            {
                throw new InvalidOperationException("Seat is required for a gaming PC.");
            }
            seatId = ParseGuid(request.SeatId, nameof(request.SeatId));
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? machineInfo.MachineName
            : request.DisplayName.Trim();

        var publicKey = await deviceKeyStore.GetOrCreatePublicKeyPemAsync(cancellationToken);
        var response = await apiClient.EnrollAuthenticatedAsync(
            RequireAccessToken(),
            new AuthenticatedInstallEnrollRequest(
                branchId,
                seatId,
                role,
                displayName,
                machineInfo.MachineName,
                publicKey),
            cancellationToken);

        bootstrapWriter.Write(new SetupWizardBootstrapConfig(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            response.CredentialId,
            response.CredentialSecret,
            role,
            response.ApiBaseUrl,
            response.UpdateChannel,
            LeaseSigningPublicKeyPem: string.Empty,
            UpdatePackageSigningPublicKeyPem: string.Empty));
        completionAction.Complete();

        return new WizardEnrollResult(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            role,
            displayName,
            machineInfo.MachineName,
            response.EnrollmentState,
            response.ApiBaseUrl,
            response.UpdateChannel);
    }
```

- [ ] **Step 3: Add error codes for the new ops**

In `ErrorCodeFor` (lines 260-266), add cases before the default:

```csharp
    private static string ErrorCodeFor(string? requestType) => requestType switch
    {
        "wizard:discover" => "wizard_discover_failed",
        "wizard:createSeat" => "wizard_create_seat_failed",
        "wizard:enroll" => "wizard_enroll_failed",
        "wizard:phoneSignIn" => "wizard_phone_sign_in_failed",
        "wizard:discoverAuth" => "wizard_discover_failed",
        "wizard:createSeatAuth" => "wizard_create_seat_failed",
        "wizard:enrollAuth" => "wizard_enroll_failed",
        _ => "wizard_request_failed"
    };
```

- [ ] **Step 4: Add the payload + result records**

Next to the existing payload records (after `WizardEnrollPayload`, line 308), add:

```csharp
    private sealed record WizardPhoneSignInPayload(string? Phone, string? Password);

    private sealed record WizardCreateSeatAuthPayload(
        string? BranchId,
        string? ZoneId,
        string? ZoneName,
        string? Name);

    private sealed record WizardEnrollAuthPayload(
        string? BranchId,
        string? SeatId,
        string? Role,
        string? DisplayName);

    private sealed record WizardPhoneSignInResult(string DisplayName);
```

- [ ] **Step 5: Build the wizard app project**

Run: `dotnet build src/AFK4.SetupWizard/AFK4.SetupWizard.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs
git commit -m "feat(setup-wizard): bridge ops for phone sign-in and authenticated install"
```

---

### Task 12: Preview fakes for the phone path

**Files:**
- Modify: `src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs`

- [ ] **Step 1: Implement the four new fake methods**

Add `using AFK4.Shared.Contracts.Identity;` at the top. Inside `FakeApiClient`, add (reusing the existing fake data + `BuildBranch()`):

```csharp
        public Task<StaffSignInResponse> SignInByPhoneAsync(string phoneNumber, string password, CancellationToken cancellationToken)
            => Task.FromResult(new StaffSignInResponse(
                StaffUserId: Guid.NewGuid(),
                OrganizationId: OrgId,
                DisplayName: "Preview Staff",
                AccessToken: "preview-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.UnixEpoch.AddHours(8),
                RefreshToken: "preview-refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.UnixEpoch.AddDays(30),
                BranchIds: [BranchId],
                Permissions: [StaffPermissionNames.InstallDevice]));

        public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(string accessToken, CancellationToken cancellationToken)
            => Task.FromResult(new InstallDiscoverResponse("Preview Staff", [BuildBranch()]));

        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
            => CreateSeatAsync(ownerCode: string.Empty, branchId, zoneId, name, cancellationToken);

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
            => EnrollAsync(
                new InstallEnrollRequest(
                    string.Empty, request.BranchId, request.SeatId, request.Role,
                    request.DisplayName, request.MachineName, request.DevicePublicKey),
                cancellationToken);
```

- [ ] **Step 2: Build (Debug, where `#if DEBUG` preview is active)**

Run: `dotnet build src/AFK4.SetupWizard/AFK4.SetupWizard.csproj -c Debug`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full wizard test suite**

Run: `dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs
git commit -m "feat(setup-wizard): preview fakes for phone sign-in path"
```

---

# Part 3 — Wizard web (React)

### Task 13: i18n keys for phone login + tightened device-name hint

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts`

- [ ] **Step 1: Add the new keys (all three locales, identical key sets)**

Add to each of `locales/{ru,en,tg}.json` (values below show ru / en / tg). Keep copy lowercase-first per the i18n guard (no ALL-CAPS words, no «компьютер»):

```jsonc
// ru.json
"setup.wizard.stepper.signIn": "Вход",
"setup.wizard.phoneLogin.title": "Вход для сотрудника",
"setup.wizard.phoneLogin.subtitle": "Войдите по номеру телефона и паролю, чтобы добавить это устройство в клуб.",
"setup.wizard.phoneLogin.field.phone": "Номер телефона",
"setup.wizard.phoneLogin.field.password": "Пароль",
"setup.wizard.phoneLogin.hint.phone": "Например, +992 93 738-00-70",
"setup.wizard.phoneLogin.hint.password": "Пароль от вашей учётной записи сотрудника.",
"setup.wizard.phoneLogin.action.signIn": "Войти",
"setup.wizard.phoneLogin.action.signingIn": "Входим…",
"setup.wizard.phoneLogin.action.useCode": "Войти по коду владельца",
"setup.wizard.phoneLogin.error.invalidPhone": "Проверьте номер телефона.",
"setup.wizard.phoneLogin.error.signInFailed": "Не удалось войти. Проверьте номер и пароль или войдите по коду владельца.",
"setup.wizard.phoneLogin.error.noBranches": "Для вашей учётной записи нет филиалов с правом установки. Войдите по коду владельца.",
"setup.wizard.phoneLogin.error.bridgeMissing": "Не удалось связаться с приложением установки.",
"setup.wizard.ownerCode.action.usePhone": "Войти по номеру телефона",
"setup.wizard.device.field.nameHint": "От 3 до 32 символов. Так это устройство будет видно в админке.",
```

```jsonc
// en.json
"setup.wizard.stepper.signIn": "Sign in",
"setup.wizard.phoneLogin.title": "Staff sign-in",
"setup.wizard.phoneLogin.subtitle": "Sign in with your phone number and password to add this device to the club.",
"setup.wizard.phoneLogin.field.phone": "Phone number",
"setup.wizard.phoneLogin.field.password": "Password",
"setup.wizard.phoneLogin.hint.phone": "For example, +992 93 738-00-70",
"setup.wizard.phoneLogin.hint.password": "Your staff account password.",
"setup.wizard.phoneLogin.action.signIn": "Sign in",
"setup.wizard.phoneLogin.action.signingIn": "Signing in…",
"setup.wizard.phoneLogin.action.useCode": "Sign in with an owner code",
"setup.wizard.phoneLogin.error.invalidPhone": "Check the phone number.",
"setup.wizard.phoneLogin.error.signInFailed": "Could not sign in. Check your number and password, or sign in with an owner code.",
"setup.wizard.phoneLogin.error.noBranches": "Your account has no branches you can install in. Sign in with an owner code.",
"setup.wizard.phoneLogin.error.bridgeMissing": "Could not reach the setup app.",
"setup.wizard.ownerCode.action.usePhone": "Sign in with a phone number",
"setup.wizard.device.field.nameHint": "3 to 32 characters. This is how the device appears in the admin panel.",
```

```jsonc
// tg.json — translate the same keys to Tajik (mirror the structure; ask for review if unsure).
"setup.wizard.stepper.signIn": "Воридшавӣ",
"setup.wizard.phoneLogin.title": "Воридшавии корманд",
"setup.wizard.phoneLogin.subtitle": "Бо рақами телефон ва парол ворид шавед, то ин дастгоҳро ба клуб илова кунед.",
"setup.wizard.phoneLogin.field.phone": "Рақами телефон",
"setup.wizard.phoneLogin.field.password": "Парол",
"setup.wizard.phoneLogin.hint.phone": "Масалан, +992 93 738-00-70",
"setup.wizard.phoneLogin.hint.password": "Пароли ҳисоби кории шумо.",
"setup.wizard.phoneLogin.action.signIn": "Ворид шудан",
"setup.wizard.phoneLogin.action.signingIn": "Воридшавӣ…",
"setup.wizard.phoneLogin.action.useCode": "Бо рамзи соҳиб ворид шавед",
"setup.wizard.phoneLogin.error.invalidPhone": "Рақами телефонро санҷед.",
"setup.wizard.phoneLogin.error.signInFailed": "Воридшавӣ нашуд. Рақам ва паролро санҷед ё бо рамзи соҳиб ворид шавед.",
"setup.wizard.phoneLogin.error.noBranches": "Ҳисоби шумо филиали дорои ҳуқуқи насб надорад. Бо рамзи соҳиб ворид шавед.",
"setup.wizard.phoneLogin.error.bridgeMissing": "Пайвастшавӣ ба барномаи насб нашуд.",
"setup.wizard.ownerCode.action.usePhone": "Бо рақами телефон ворид шавед",
"setup.wizard.device.field.nameHint": "Аз 3 то 32 аломат. Дастгоҳ дар панели идора ҳамин тавр намоиш дода мешавад.",
```

> `setup.wizard.device.field.nameHint` already exists — this **replaces** its value. The other keys are new. Keep the JSON files sorted/grouped consistently with their existing structure.

- [ ] **Step 2: Regenerate the typed messages**

Run: `cd packages/i18n; bun run gen`
Expected: `packages/i18n/src/messages.ts` updated with the new keys for all three locales.

- [ ] **Step 3: Verify catalog parity + i18n guard**

Run: `cd packages/i18n; bun test`
Expected: PASS (parity test + the copy-voice guard test green).

- [ ] **Step 4: Commit**

```bash
git add locales/ packages/i18n/src/messages.ts
git commit -m "i18n: add wizard phone-login strings, tighten device-name hint to 3-32"
```

---

### Task 14: bun test harness for `AFK4.SetupWizard.Web`

This project has no test harness yet. Mirror `packages/i18n`.

**Files:**
- Create: `src/AFK4.SetupWizard.Web/bunfig.toml`
- Create: `src/AFK4.SetupWizard.Web/test-setup.ts`
- Read first: `src/AFK4.SetupWizard.Web/package.json` (devDependencies), `packages/i18n/test-setup.ts`, `packages/i18n/bunfig.toml`

- [ ] **Step 1: Create `bunfig.toml`**

```toml
[test]
preload = ["./test-setup.ts"]
```

- [ ] **Step 2: Create `test-setup.ts`**

```ts
import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);

const { cleanup } = await import('@testing-library/react');

afterEach(() => {
  cleanup();
  try {
    localStorage.clear();
  } catch {
    // no localStorage in this environment
  }
});
```

- [ ] **Step 3: Ensure test devDependencies exist**

Inspect `src/AFK4.SetupWizard.Web/package.json`. If any of `@happy-dom/global-registrator`, `@testing-library/react`, `@testing-library/jest-dom` are missing from `devDependencies`, add them (match the versions used in `packages/i18n/package.json` for consistency):

Run (only for the missing ones): `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun add -d @happy-dom/global-registrator @testing-library/react @testing-library/jest-dom`

- [ ] **Step 4: Smoke-test the harness with a trivial test**

Create a temporary `src/AFK4.SetupWizard.Web/src/harness.smoke.test.tsx`:

```tsx
import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';

it('renders into happy-dom', () => {
  render(<button>ok</button>);
  expect(screen.getByText('ok')).toBeInTheDocument();
});
```

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun test`
Expected: 1 pass. Then delete the smoke test file.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.SetupWizard.Web/bunfig.toml src/AFK4.SetupWizard.Web/test-setup.ts src/AFK4.SetupWizard.Web/package.json
git commit -m "test(setup-wizard-web): add bun + happy-dom test harness"
```

---

### Task 15: `wizardApi.ts` — phone sign-in, authenticated discover, install client

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/wizardApi.ts`

- [ ] **Step 1: Replace the standalone `createSeat`/`enrollDevice` exports with a client abstraction + add phone functions**

Replace lines 49-75 (the `WizardCreateSeatRequest`/`WizardEnrollRequest` interfaces and `discoverOwner`/`createSeat`/`enrollDevice` functions) with:

```ts
export interface WizardPhoneSignInResult {
  displayName: string;
}

export interface WizardSeatDraft {
  branchId: string;
  zoneId: string;
  zoneName: string;
  name: string;
}

export interface WizardEnrollDraft {
  branchId: string;
  seatId: string | null;
  role: WizardRole;
  displayName: string;
}

/** Install operations bound to an authentication mode. The owner-code path threads
 *  the code into every payload; the authenticated path carries no code — the bearer
 *  token is held by the native host after `signInByPhone`. */
export interface WizardInstallClient {
  createSeat(draft: WizardSeatDraft): Promise<WizardSeat>;
  enrollDevice(draft: WizardEnrollDraft): Promise<WizardEnrollResult>;
}

export function discoverOwner(ownerCode: string): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discover', { ownerCode });
}

export function signInByPhone(phone: string, password: string): Promise<WizardPhoneSignInResult> {
  return postHostRequest<WizardPhoneSignInResult>('wizard:phoneSignIn', { phone, password });
}

export function discoverAuthenticated(): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discoverAuth');
}

export function ownerCodeInstallClient(ownerCode: string): WizardInstallClient {
  return {
    createSeat: (draft) =>
      postHostRequest<WizardSeat>('wizard:createSeat', { ownerCode, ...draft }),
    enrollDevice: (draft) =>
      postHostRequest<WizardEnrollResult>('wizard:enroll', { ownerCode, ...draft }),
  };
}

export function authenticatedInstallClient(): WizardInstallClient {
  return {
    createSeat: (draft) => postHostRequest<WizardSeat>('wizard:createSeatAuth', draft),
    enrollDevice: (draft) => postHostRequest<WizardEnrollResult>('wizard:enrollAuth', draft),
  };
}
```

> `postHostRequest` already accepts an optional payload (signature `postHostRequest<T>(type, payload?, timeoutMs?)`), so `discoverAuthenticated()` calling it with no payload is fine.

- [ ] **Step 2: Type-check**

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun run build`
Expected: `tsc -b` will FAIL here because `OwnerCodeScreen`/`DeviceScreen` still import the removed `createSeat`/`enrollDevice`. That is expected — Tasks 16-18 fix the callers. (If you prefer green-at-every-commit, do Steps 1-2 of Tasks 16-18 before building; otherwise proceed and build at the end of Task 18.)

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.SetupWizard.Web/src/wizardApi.ts
git commit -m "feat(setup-wizard-web): phone sign-in + install client abstraction"
```

---

### Task 16: `Stepper.tsx` — add the phoneLogin step alias

The phone-login and owner-code screens share stepper position 1 ("Вход"). Map both step ids to index 0.

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/Stepper.tsx`

- [ ] **Step 1: Add `phoneLogin` to the step union and relabel step 1**

Replace lines 5-18:

```ts
export type WizardStep =
  | 'phoneLogin'
  | 'ownerCode'
  | 'branchSelection'
  | 'role'
  | 'device'
  | 'finished';

const STEPS: { id: WizardStep; index: number; labelKey: MessageKey }[] = [
  { id: 'phoneLogin', index: 1, labelKey: 'setup.wizard.stepper.signIn' },
  { id: 'branchSelection', index: 2, labelKey: 'setup.wizard.stepper.branch' },
  { id: 'role', index: 3, labelKey: 'setup.wizard.stepper.role' },
  { id: 'device', index: 4, labelKey: 'setup.wizard.stepper.device' },
  { id: 'finished', index: 5, labelKey: 'setup.wizard.stepper.done' },
];

// The owner-code fallback screen shares stepper position 1 with phone login.
const STEP_TO_INDEX: Record<WizardStep, number> = {
  phoneLogin: 0,
  ownerCode: 0,
  branchSelection: 1,
  role: 2,
  device: 3,
  finished: 4,
};
```

- [ ] **Step 2: Use the index map instead of `findIndex`**

Replace line 26:

```ts
  const currentIndex = STEP_TO_INDEX[current];
```

- [ ] **Step 3: Commit** (build deferred to Task 18)

```bash
git add src/AFK4.SetupWizard.Web/src/Stepper.tsx
git commit -m "feat(setup-wizard-web): stepper supports phone-login step"
```

---

### Task 17: `PhoneLoginScreen.tsx` + test

**Files:**
- Create: `src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.tsx`
- Create: `src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const signInByPhone = mock(async () => ({ displayName: 'Сотрудник' }));
const discoverAuthenticated = mock(async () => ({
  ownerName: 'Сотрудник',
  branches: [
    {
      branchId: '11111111-1111-1111-1111-111111111111',
      branchSlug: 'main',
      branchName: 'Главный',
      zones: [],
      seats: [],
      freeSeatIds: [],
    },
  ],
}));

mock.module('./wizardApi', () => ({ signInByPhone, discoverAuthenticated }));

const { PhoneLoginScreen } = await import('./PhoneLoginScreen');

function renderScreen(props: Partial<Parameters<typeof PhoneLoginScreen>[0]> = {}) {
  const onDiscovered = mock(() => {});
  const onUseOwnerCode = mock(() => {});
  render(
    <I18nProvider>
      <PhoneLoginScreen onDiscovered={onDiscovered} onUseOwnerCode={onUseOwnerCode} {...props} />
    </I18nProvider>,
  );
  return { onDiscovered, onUseOwnerCode };
}

describe('PhoneLoginScreen', () => {
  beforeEach(() => {
    signInByPhone.mockClear();
    discoverAuthenticated.mockClear();
  });

  it('signs in then discovers and reports branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/номер телефона/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText(/пароль/i), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('routes to the owner-code fallback', () => {
    const { onUseOwnerCode } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /код владельца/i }));
    expect(onUseOwnerCode).toHaveBeenCalledTimes(1);
  });
});
```

> If `mock.module` hoisting causes the static `await import('./PhoneLoginScreen')` to load the real module, switch to importing the component lazily *inside* each test after `mock.module` runs. Confirm by running the test and reading the failure.

- [ ] **Step 2: Run to confirm it fails (no component yet)**

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun test PhoneLoginScreen`
Expected: FAIL — `Cannot find module './PhoneLoginScreen'`.

- [ ] **Step 3: Implement `PhoneLoginScreen.tsx`** (modeled on `OwnerCodeScreen.tsx`)

```tsx
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowRight, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  discoverAuthenticated,
  signInByPhone,
  type WizardDiscoverResponse,
} from './wizardApi';
import { isHostBridgeUnavailableError } from './hostBridge';

interface PhoneLoginScreenProps {
  onDiscovered(response: WizardDiscoverResponse): void;
  onUseOwnerCode(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// E.164-ish: 9–15 digits after stripping +, spaces and dashes.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

export function PhoneLoginScreen({ onDiscovered, onUseOwnerCode }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [touched, setTouched] = useState(false);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });
  const [showSlowSkeleton, setShowSlowSkeleton] = useState(false);

  useEffect(() => {
    if (request.kind !== 'loading') {
      setShowSlowSkeleton(false);
      return;
    }
    const timer = setTimeout(() => setShowSlowSkeleton(true), 300);
    return () => clearTimeout(timer);
  }, [request.kind]);

  const normalizedPhone = normalizePhone(phone);
  const phoneValid = /^[0-9]{9,15}$/.test(normalizedPhone);
  const canSubmit = phoneValid && password.length > 0 && request.kind !== 'loading';
  const showPhoneHint = touched && phone.length > 0 && !phoneValid;

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setTouched(true);
      if (!phoneValid || password.length === 0 || request.kind === 'loading') {
        return;
      }
      setRequest({ kind: 'loading' });
      try {
        await signInByPhone(normalizedPhone, password);
        const response = await discoverAuthenticated();
        if (response.branches.length === 0) {
          setRequest({ kind: 'error', message: t('setup.wizard.phoneLogin.error.noBranches') });
          return;
        }
        onDiscovered(response);
      } catch (error) {
        setRequest({ kind: 'error', message: messageForError(error, t) });
      }
    },
    [normalizedPhone, onDiscovered, password, phoneValid, request.kind, t],
  );

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">{t('setup.wizard.common.step')} 1</span>
        <h1>{t('setup.wizard.phoneLogin.title')}</h1>
        <p>{t('setup.wizard.phoneLogin.subtitle')}</p>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.phone')}</span>
          <input
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            autoFocus
            spellCheck={false}
            value={phone}
            onChange={(event) => {
              setPhone(event.target.value);
              if (request.kind === 'error') setRequest({ kind: 'idle' });
            }}
            onBlur={() => setTouched(true)}
            placeholder="+992 93 738-00-70"
            aria-invalid={showPhoneHint}
            aria-describedby="phone-hint"
          />
          <span id="phone-hint" className="wizard-field-hint">
            {showPhoneHint
              ? t('setup.wizard.phoneLogin.error.invalidPhone')
              : t('setup.wizard.phoneLogin.hint.phone')}
          </span>
        </label>

        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.password')}</span>
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
              if (request.kind === 'error') setRequest({ kind: 'idle' });
            }}
            aria-describedby="password-hint"
          />
          <span id="password-hint" className="wizard-field-hint">
            {t('setup.wizard.phoneLogin.hint.password')}
          </span>
        </label>

        {showSlowSkeleton && (
          <div className="wizard-skeleton-list" aria-hidden>
            <div className="wizard-skeleton-card" />
            <div className="wizard-skeleton-card" />
          </div>
        )}

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <button type="submit" className="wizard-primary" disabled={!canSubmit}>
          {request.kind === 'loading' ? (
            <>
              <Loader2 className="wizard-spinner" aria-hidden />
              <span>{t('setup.wizard.phoneLogin.action.signingIn')}</span>
            </>
          ) : (
            <>
              <span>{t('setup.wizard.phoneLogin.action.signIn')}</span>
              <ArrowRight aria-hidden />
            </>
          )}
        </button>

        <button type="button" className="wizard-link-action wizard-fallback-link" onClick={onUseOwnerCode}>
          {t('setup.wizard.phoneLogin.action.useCode')}
        </button>
      </form>
    </section>
  );
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.phoneLogin.error.bridgeMissing');
  }
  // Backend returns 401 with no detail (no user enumeration) → one combined, honest message.
  return t('setup.wizard.phoneLogin.error.signInFailed');
}
```

- [ ] **Step 4: Run the test to green**

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun test PhoneLoginScreen`
Expected: 2 pass.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.tsx src/AFK4.SetupWizard.Web/src/PhoneLoginScreen.test.tsx
git commit -m "feat(setup-wizard-web): phone-login screen with owner-code fallback"
```

---

### Task 18: Wire phone login into `App.tsx`, route owner-code screen + `DeviceScreen` by auth mode

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/App.tsx`
- Modify: `src/AFK4.SetupWizard.Web/src/OwnerCodeScreen.tsx`
- Modify: `src/AFK4.SetupWizard.Web/src/DeviceScreen.tsx`

- [ ] **Step 1: `App.tsx` — state, imports, discovery handlers**

Add imports (after line 8):

```tsx
import { PhoneLoginScreen } from './PhoneLoginScreen';
```

Update the `wizardApi` import (lines 12-19) to include the client factories:

```tsx
import {
  authenticatedInstallClient,
  getBootstrapConfig,
  ownerCodeInstallClient,
  type WizardBranch,
  type WizardDiscoverResponse,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';
```

Extend `WizardState` (lines 21-30) with an auth mode:

```tsx
type AuthMode = 'phone' | 'code';

interface WizardState {
  step: WizardStep;
  authMode: AuthMode;
  ownerCode: string;
  ownerName: string;
  branches: WizardBranch[];
  branch: WizardBranch | null;
  role: WizardRole;
  enrollResult: WizardEnrollResult | null;
  selectedSeat: WizardSeat | null;
}
```

Update `initialState` (lines 32-41):

```tsx
const initialState: WizardState = {
  step: 'phoneLogin',
  authMode: 'phone',
  ownerCode: '',
  ownerName: '',
  branches: [],
  branch: null,
  role: 'gaming_pc',
  enrollResult: null,
  selectedSeat: null,
};
```

Replace `handleDiscovered` (lines 83-103) with a shared applier + two entry points:

```tsx
  const applyDiscovery = useCallback(
    (authMode: AuthMode, ownerCode: string, response: WizardDiscoverResponse) => {
      const base = {
        authMode,
        ownerCode,
        ownerName: response.ownerName,
        branches: response.branches,
      } as const;
      if (response.branches.length === 1) {
        setState((prev) => ({ ...prev, ...base, branch: response.branches[0], step: 'role' }));
        return;
      }
      setState((prev) => ({ ...prev, ...base, branch: null, step: 'branchSelection' }));
    },
    [],
  );

  const handleCodeDiscovered = useCallback(
    (ownerCode: string, response: WizardDiscoverResponse) => applyDiscovery('code', ownerCode, response),
    [applyDiscovery],
  );

  const handlePhoneDiscovered = useCallback(
    (response: WizardDiscoverResponse) => applyDiscovery('phone', '', response),
    [applyDiscovery],
  );

  const goToOwnerCode = useCallback(() => {
    setState((prev) => ({ ...prev, step: 'ownerCode', authMode: 'code' }));
  }, []);

  const goToPhoneLogin = useCallback(() => {
    setState(initialState);
  }, []);
```

Update `backToOwnerCode` (lines 120-122) to reset to phone login (the new first step):

```tsx
  const backToReset = useCallback(() => {
    setState(initialState);
  }, []);
```

Update `backFromRole` (lines 124-130) so the "back" target on a single-branch flow returns to the correct login screen:

```tsx
  const backFromRole = useCallback(() => {
    setState((prev) => ({
      ...prev,
      step:
        prev.branches.length > 1
          ? 'branchSelection'
          : prev.authMode === 'phone'
            ? 'phoneLogin'
            : 'ownerCode',
      branch: prev.branches.length > 1 ? null : prev.branch,
    }));
  }, []);
```

Derive the install client for the device step:

```tsx
  const installClient = useMemo<WizardInstallClient>(
    () => (state.authMode === 'phone' ? authenticatedInstallClient() : ownerCodeInstallClient(state.ownerCode)),
    [state.authMode, state.ownerCode],
  );
```

Update `stepAnnouncement` (lines 150-160) to map `phoneLogin`/`ownerCode` to step 1:

```tsx
  const stepAnnouncement = useMemo(() => {
    const stepLabelKey: Record<typeof state.step, MessageKey> = {
      phoneLogin: 'setup.wizard.stepper.signIn',
      ownerCode: 'setup.wizard.stepper.signIn',
      branchSelection: 'setup.wizard.stepper.branch',
      role: 'setup.wizard.stepper.role',
      device: 'setup.wizard.stepper.device',
      finished: 'setup.wizard.stepper.done',
    };
    const order = ['phoneLogin', 'branchSelection', 'role', 'device', 'finished'];
    const announceStep = state.step === 'ownerCode' ? 'phoneLogin' : state.step;
    const stepNumber = order.indexOf(announceStep) + 1;
    return `${t('setup.wizard.common.step')} ${stepNumber}: ${t(stepLabelKey[state.step])}`;
  }, [state.step, t]);
```

- [ ] **Step 2: `App.tsx` — render the screens**

Replace the `main` body screens (lines 242-283) so phone login is first, owner code is the fallback, branch/role pass the right back-handlers, and DeviceScreen receives the client:

```tsx
        {state.step === 'phoneLogin' && (
          <PhoneLoginScreen onDiscovered={handlePhoneDiscovered} onUseOwnerCode={goToOwnerCode} />
        )}

        {state.step === 'ownerCode' && (
          <OwnerCodeScreen onDiscovered={handleCodeDiscovered} onUsePhone={goToPhoneLogin} />
        )}

        {state.step === 'branchSelection' && (
          <BranchSelectionScreen
            ownerName={state.ownerName}
            branches={state.branches}
            onSelect={handleSelectBranch}
            onBack={backToReset}
          />
        )}

        {state.step === 'role' && state.branch && (
          <RoleScreen
            ownerName={state.ownerName}
            branchName={state.branch.branchName}
            initialRole={state.role}
            onContinue={handleRoleContinue}
            onBack={backFromRole}
          />
        )}

        {state.step === 'device' && state.branch && (
          <DeviceScreen
            installClient={installClient}
            ownerName={state.ownerName}
            branch={state.branch}
            role={state.role}
            defaultDisplayName={defaultDisplayName}
            onEnrolled={handleEnrolled}
            onBack={backToRole}
          />
        )}

        {state.step === 'finished' && state.enrollResult && state.branch && (
          <FinishedScreen
            result={state.enrollResult}
            branchName={state.branch.branchName}
            selectedSeat={state.selectedSeat}
          />
        )}
```

> `useMemo` is already imported in `App.tsx` (line 1). Remove the now-unused `backToOwnerCode` if you renamed it to `backToReset`; the only caller was BranchSelection's `onBack`.

- [ ] **Step 3: `OwnerCodeScreen.tsx` — add the back-to-phone link**

Change the props (lines 10-12):

```tsx
interface OwnerCodeScreenProps {
  onDiscovered(ownerCode: string, response: WizardDiscoverResponse): void;
  onUsePhone(): void;
}
```

Update the component signature (line 22): `export function OwnerCodeScreen({ onDiscovered, onUsePhone }: OwnerCodeScreenProps) {`

Add a fallback link after the submit button (before `</form>`, line 193 area):

```tsx
        <button type="button" className="wizard-link-action wizard-fallback-link" onClick={onUsePhone}>
          {t('setup.wizard.ownerCode.action.usePhone')}
        </button>
```

- [ ] **Step 4: `DeviceScreen.tsx` — accept the install client instead of `ownerCode`**

Update imports (lines 5-12) — drop `createSeat`/`enrollDevice`, add the client type:

```tsx
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';
```

Update props (lines 14-22) — replace `ownerCode: string;` with `installClient: WizardInstallClient;`:

```tsx
interface DeviceScreenProps {
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBack(): void;
}
```

Update the destructure (lines 30-38): replace `ownerCode,` with `installClient,`.

Add the 3–32 display-name rule. Replace `canEnroll` (lines 76-77):

```tsx
  const displayNameValid = trimmedDisplayName.length >= 3 && trimmedDisplayName.length <= 32;
  const canEnroll = displayNameValid && (!requiresSeat || selectedSeat !== null);
```

In `handleCreateSeat`, replace the `createSeat({ ownerCode, ... })` call (lines 142-148) with:

```tsx
      const seat = await installClient.createSeat({
        branchId: branch.branchId,
        zoneId: defaultZone.zoneId,
        zoneName: defaultZone.name,
        name: trimmedNewSeat,
      });
```

Update that callback's dependency array (line 182): replace `ownerCode` with `installClient`.

In `handleSubmit`, replace the `enrollDevice({ ownerCode, ... })` call (lines 193-199) with:

```tsx
        const result = await installClient.enrollDevice({
          branchId: branch.branchId,
          seatId: requiresSeat ? selectedSeat!.seatId : null,
          role,
          displayName: trimmedDisplayName,
        });
```

Update its dependency array (lines 205-216): replace `ownerCode` with `installClient`.

Constrain the display-name input (lines 240-248): add `minLength={3}` and `maxLength={32}`:

```tsx
          <input
            type="text"
            value={displayName}
            autoComplete="off"
            spellCheck={false}
            minLength={3}
            maxLength={32}
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder={defaultDisplayName}
            aria-invalid={trimmedDisplayName.length > 0 && !displayNameValid}
            aria-describedby="display-name-hint"
          />
```

- [ ] **Step 5: Type-check + build the web app**

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun run build`
Expected: `tsc -b` clean, `vite build` succeeds, 0 errors. Fix any remaining references to the removed `ownerCode` prop / `createSeat`/`enrollDevice` imports.

- [ ] **Step 6: Run the web test suite**

Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun test`
Expected: PASS (PhoneLoginScreen tests + any others).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.SetupWizard.Web/src/App.tsx src/AFK4.SetupWizard.Web/src/OwnerCodeScreen.tsx src/AFK4.SetupWizard.Web/src/DeviceScreen.tsx
git commit -m "feat(setup-wizard-web): phone login first, owner code fallback, 3-32 device name"
```

---

### Task 19: Full verification + manual preview

- [ ] **Step 1: Backend + wizard .NET suites**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Run: `dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj`
Expected: both green.

- [ ] **Step 2: i18n + web**

Run: `cd packages/i18n; bun test`
Run: `cd src/AFK4.SetupWizard.Web; ~/.bun/bin/bun test; ~/.bun/bin/bun run build`
Expected: all green; build clean.

- [ ] **Step 3: Manual preview — phone path end-to-end + owner-code fallback**

Per [[setup-wizard-preview-launch]]:
1. `cd src/AFK4.SetupWizard.Web; bun run dev` (→ http://127.0.0.1:5175)
2. set env `AFK4_SETUP_WIZARD_WEB_DEV_SERVER_URL=http://127.0.0.1:5175`
3. `dotnet run --project src/AFK4.SetupWizard -c Debug -- --preview`

Verify: phone-login screen is step 1; any phone+password proceeds (preview fake) → branch/role/device → enroll succeeds; the "войти по коду владельца" link reaches the owner-code screen and back; the device name field rejects <3 / >32 chars.

- [ ] **Step 4: Update memory**

Update `phone-staff-registration.md`: mark **Phase C DONE** (date, commits, gate results), note authenticated `/api/install/auth/*` + bridge ops + `PhoneLoginScreen`, and that only Phase D (SMS password reset) + the deferred owner-visibility badge remain.

---

## Self-Review

**Spec coverage (spec §5 Phase C):**
- Authenticated install endpoints resolving org/staff from `StaffContext`, gated by `devices.install` → Task 7 (discover/seats/enroll), authz via `StaffAuthorizationService`.
- Reuse `EfInstallService` enrollment core (extract post-resolution logic) → Tasks 3-5 (`EnrollResolvedAsync`, `CreateSeatResolvedAsync`, `BuildBranchDtoAsync`).
- Contracts drop `OwnerCode` → Task 1.
- Native bridge `wizard:phoneSignIn` holds token, attaches Bearer; client `PhoneSignInAsync` + authenticated discover/seats/enroll; preview fakes → Tasks 9, 11, 12.
- Frontend `PhoneLoginScreen` first, owner-code fallback link, PC name 3–32, i18n ru/en/tg → Tasks 13, 16, 17, 18.
- Errors: 401-no-enumeration combined message + always-present fallback link + no-branches empty state → Task 17.
- Owner code path unchanged (public endpoints + `OwnerCodeScreen` logic intact) → Tasks 7, 18.
- Testing: unit (client mapping/header), integration (authz gate, codeless enroll), frontend (validation/fallback) → Tasks 8, 10, 17.

**Deliberate scope decisions (documented):**
- Discover scopes to `StaffContext.BranchIds` uniformly; "all branches for owner" is naturally the owner's branch assignments. Owner code remains the fallback for any branch a staff member isn't assigned to.
- No new `EnrolledByStaffUserId` column — the audit record captures the acting staff (`ActorStaffUserId`), so the actor is recorded without a migration (YAGNI).
- 3–32 applies to BOTH install paths (both go through the wizard); existing owner-code tests using longer names get updated (Task 3 Step 5).

**Type consistency:** `WizardInstallClient` (`createSeat(WizardSeatDraft)`, `enrollDevice(WizardEnrollDraft)`) is the same in `wizardApi.ts` (Task 15), `App.tsx` (Task 18), and `DeviceScreen.tsx` (Task 18). Bridge ops `wizard:{phoneSignIn,discoverAuth,createSeatAuth,enrollAuth}` match between `wizardApi.ts` (Task 15) and `SetupWizardWebHostBridge.cs` (Task 11). `AuthenticatedInstallEnrollRequest` field order is identical across contract (Task 1), client (Task 9), bridge (Task 11), preview (Task 12), endpoint/service (Tasks 3, 7), and tests (Tasks 8, 10).

**Risk flags:** (1) `mock.module` hoisting in bun:test — Task 17 has a fallback (lazy import). (2) The 3–32 change may break owner-code backend tests — Task 3/4 run the suite and fix. (3) `RecordingSetupWizardApiClient` must implement the widened interface — Task 9 Step 3. (4) Test devDeps may be absent in `AFK4.SetupWizard.Web` — Task 14 Step 3 adds them (`bun install --force` may be needed per [[setup-wizard-preview-launch]]).
