# Customer Portal PWA — Plan 1: Foundation, Shell & Dashboard

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the `AFK4.Customer.Web` PWA with tenant branding, player sign-in, an authenticated app shell, and a live dashboard — the first end-to-end vertical of the customer portal.

**Architecture:** A new standalone bun-workspace package mirroring `AFK4.Platform.Web` (Vite 8 + React 19 + TS 6, Tailwind 4 + shadcn, bun test). A small backend slice adds a public per-tenant branding endpoint. The frontend resolves the tenant key, themes itself, signs the player in against the existing `/api/public/player/*` endpoints, and renders the dashboard from `GET /api/me/dashboard` with a client-ticking session card.

**Tech Stack:** .NET 10 / EF Core (backend slice), React 19, Vite 8, TypeScript 6, Tailwind CSS 4, shadcn/ui (new-york), `@afk4/money`, `@afk4/i18n`, bun test + happy-dom + @testing-library/react.

Spec: `docs/superpowers/specs/2026-06-03-customer-portal-pwa-design.md`. This plan covers spec build-sequence tasks 1–6. Plan 2 (separate doc) covers history/purchases/wallet/reservations/profile + PWA/offline + i18n.

**Conventions for the implementer:**
- All `bun`/`vite`/`tsc` commands run from `src/AFK4.Customer.Web` unless noted; install deps with `bun install` from the repo root (workspace).
- Backend commands run from the repo root.
- Money is `long` minor units everywhere; convert to major only at render via `@afk4/money`.
- Match existing file style in `AFK4.Platform.Web` (2-space indent, single quotes, no default export for components).

---

## Task 1: Backend — per-tenant branding endpoint

**Files:**
- Create: `src/AFK4.Shared.Contracts/Branding/TenantBrandingDto.cs`
- Modify: `src/AFK4.Platform.Api/Data/OrganizationEntity.cs`
- Create (generated): `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddBrandingToOrganization.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (new public endpoint near line 652)
- Modify: `src/AFK4.Platform.DevSeed/Program.cs:115-120` (seed slug + branding)
- Test: `tests/AFK4.Platform.Api.Tests/TenantBrandingEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/TenantBrandingEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class TenantBrandingEndpointTests
{
    private static async Task<Guid> SeedOrgAsync(PlatformApiFactory factory, string slug, string status = "active")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = id,
            Slug = slug,
            Name = "CyberX",
            Status = status,
            LogoUrl = "https://cdn.example/cyberx.png",
            AccentColor = "#c8ff00",
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task GetBranding_KnownActiveSlug_ReturnsBranding()
    {
        await using var factory = new PlatformApiFactory();
        var orgId = await SeedOrgAsync(factory, "cyberx");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/tenant/cyberx/branding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantBrandingDto>();
        Assert.NotNull(body);
        Assert.Equal(orgId, body!.OrganizationId);
        Assert.Equal("CyberX", body.Name);
        Assert.Equal("#c8ff00", body.AccentColor);
    }

    [Fact]
    public async Task GetBranding_UnknownSlug_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/tenant/nope/branding");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBranding_SuspendedOrg_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        await SeedOrgAsync(factory, "frozen", status: "suspended");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/public/tenant/frozen/branding");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter TenantBrandingEndpointTests`
Expected: FAIL — `TenantBrandingDto` does not exist / `OrganizationEntity` has no `LogoUrl`.

- [ ] **Step 3: Add the contract**

Create `src/AFK4.Shared.Contracts/Branding/TenantBrandingDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Branding;

public sealed record TenantBrandingDto(
    Guid OrganizationId,
    string Name,
    string? LogoUrl,
    string? AccentColor);
```

- [ ] **Step 4: Add branding columns to the entity**

In `src/AFK4.Platform.Api/Data/OrganizationEntity.cs`, add after `Name` (line 9):

```csharp
    public string? LogoUrl { get; set; }

    public string? AccentColor { get; set; }
```

- [ ] **Step 5: Create the EF migration**

Run from repo root:
`dotnet ef migrations add AddBrandingToOrganization --project src/AFK4.Platform.Api --output-dir Data/Migrations`
Expected: a new `<timestamp>_AddBrandingToOrganization.cs` adding two nullable `text` columns. Open it and confirm `Up()` calls `AddColumn<string>(name: "LogoUrl", ...)` and `AddColumn<string>(name: "AccentColor", ...)`, both `nullable: true`.

- [ ] **Step 6: Add the public endpoint**

In `src/AFK4.Platform.Api/Program.cs`, after the `/api/public/player/refresh` block (line 652), add:

```csharp
app.MapGet("/api/public/tenant/{tenantKey}/branding", async (
    string tenantKey,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var org = await dbContext.Organizations
        .AsNoTracking()
        .Where(o => o.Slug == tenantKey && o.Status == "active")
        .Select(o => new TenantBrandingDto(o.OrganizationId, o.Name, o.LogoUrl, o.AccentColor))
        .FirstOrDefaultAsync(cancellationToken);
    return org is null ? Results.NotFound() : Results.Ok(org);
}).RequireRateLimiting("player-public");
```

Add `using AFK4.Shared.Contracts.Branding;` to the top of `Program.cs` if not already present.

- [ ] **Step 7: Seed demo branding in DevSeed**

In `src/AFK4.Platform.DevSeed/Program.cs`, replace the `Organizations.Add` block (lines 115-120) with:

```csharp
        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Slug = "demo",
            Name = "AFK4 Demo",
            LogoUrl = null,
            AccentColor = "#c8ff00",
            CreatedAtUtc = now.AddDays(-10)
        });
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter TenantBrandingEndpointTests`
Expected: PASS (3 tests).

- [ ] **Step 9: Run the full backend suite to confirm no regressions**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (previous count + 3).

- [ ] **Step 10: Commit**

```bash
git add src/AFK4.Shared.Contracts/Branding/ src/AFK4.Platform.Api/Data/OrganizationEntity.cs src/AFK4.Platform.Api/Data/Migrations/ src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.DevSeed/Program.cs tests/AFK4.Platform.Api.Tests/TenantBrandingEndpointTests.cs
git commit -m "feat(portal): public per-tenant branding endpoint + DevSeed slug"
```

---

## Task 2: Scaffold the `AFK4.Customer.Web` workspace package

**Files:**
- Create: `src/AFK4.Customer.Web/package.json`
- Create: `src/AFK4.Customer.Web/vite.config.ts`
- Create: `src/AFK4.Customer.Web/tsconfig.json`
- Create: `src/AFK4.Customer.Web/bunfig.toml`
- Create: `src/AFK4.Customer.Web/components.json`
- Create: `src/AFK4.Customer.Web/index.html`
- Create: `src/AFK4.Customer.Web/src/test/setup.ts`
- Create: `src/AFK4.Customer.Web/src/index.css`
- Create: `src/AFK4.Customer.Web/src/lib/utils.ts`
- Create: `src/AFK4.Customer.Web/src/main.tsx`
- Create: `src/AFK4.Customer.Web/src/App.tsx`
- Create: `src/AFK4.Customer.Web/src/App.test.tsx`
- Create: `src/AFK4.Customer.Web/src/vite-env.d.ts`
- Modify: root `package.json` (`workspaces` array)

- [ ] **Step 1: Add the workspace entry**

In root `package.json`, add `"src/AFK4.Customer.Web"` to the `workspaces` array:

```json
  "workspaces": [
    "packages/*",
    "src/AFK4.Platform.Web",
    "src/AFK4.Operator.App.Web",
    "src/AFK4.Customer.Web"
  ]
```

- [ ] **Step 2: Create `package.json`**

```json
{
  "name": "afk4-customer-web",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite --host 127.0.0.1 --port 5176",
    "build": "tsc -b && vite build",
    "test": "bun test",
    "preview": "vite preview --host 127.0.0.1 --port 4176"
  },
  "dependencies": {
    "@afk4/i18n": "workspace:*",
    "@afk4/money": "workspace:*",
    "class-variance-authority": "^0.7.1",
    "clsx": "^2.1.1",
    "lucide-react": "^1.17.0",
    "radix-ui": "^1.4.3",
    "react": "^19.2.6",
    "react-dom": "^19.2.6",
    "tailwind-merge": "^3.6.0"
  },
  "devDependencies": {
    "@happy-dom/global-registrator": "^20.9.0",
    "@tailwindcss/vite": "^4.3.0",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/react": "^16.3.2",
    "@types/bun": "^1.3.14",
    "@types/node": "^25.9.1",
    "@types/react": "^19.2.15",
    "@types/react-dom": "^19.2.3",
    "@vitejs/plugin-react": "^6.0.2",
    "tailwindcss": "^4.3.0",
    "typescript": "^6.0.3",
    "vite": "^8.0.13"
  },
  "engines": {
    "node": ">=20.19.0"
  }
}
```

- [ ] **Step 3: Create `vite.config.ts`**

```ts
import path from 'node:path';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') }
  }
});
```

- [ ] **Step 4: Create `tsconfig.json`** (copy `src/AFK4.Platform.Web/tsconfig.json` verbatim — same compiler options and `@/*` path alias). Then create `bunfig.toml`:

```toml
[test]
preload = ["./src/test/setup.ts"]
```

- [ ] **Step 5: Create `components.json`**

```json
{
  "$schema": "https://ui.shadcn.com/schema.json",
  "style": "new-york",
  "rsc": false,
  "tsx": true,
  "tailwind": { "config": "", "css": "src/index.css", "baseColor": "neutral", "cssVariables": true },
  "aliases": { "components": "@/components", "utils": "@/lib/utils", "ui": "@/components/ui" },
  "iconLibrary": "lucide"
}
```

- [ ] **Step 6: Create `index.html`**

```html
<!doctype html>
<html lang="ru">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
    <title>AFK4</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 7: Create `src/vite-env.d.ts`**

```ts
/// <reference types="vite/client" />
```

- [ ] **Step 8: Create `src/lib/utils.ts`** (the shadcn `cn` helper — identical to `AFK4.Platform.Web`)

```ts
import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
```

- [ ] **Step 9: Create `src/index.css`** — Tailwind 4 import + the Clean Esports dark theme tokens

```css
@import 'tailwindcss';

@theme {
  --color-bg: #101314;
  --color-surface: #181c1d;
  --color-surface-2: #1f2625;
  --color-border: #262c2c;
  --color-accent: #c8ff00;
  --color-accent-fg: #101314;
}

:root {
  /* tenant-overridable accent (applyTheme writes here) */
  --accent: #c8ff00;
  --accent-fg: #101314;
  /* text opacities per dark-theme rule */
  --text-1: rgba(255, 255, 255, 0.87);
  --text-2: rgba(255, 255, 255, 0.60);
  --text-3: rgba(255, 255, 255, 0.38);
}

html, body, #root { height: 100%; }

body {
  margin: 0;
  background: var(--color-bg);
  color: var(--text-1);
  font-family: system-ui, -apple-system, 'Segoe UI', sans-serif;
  font-variant-numeric: tabular-nums;
  -webkit-font-smoothing: antialiased;
}

@media (prefers-reduced-motion: reduce) {
  * { animation-duration: 0.01ms !important; transition-duration: 0.01ms !important; }
}
```

(Note: a distinctive display font like Geist/Satoshi is wired in Plan 2's polish task; system-ui keeps the scaffold dependency-free.)

- [ ] **Step 10: Create `src/App.tsx`** (placeholder shell, replaced in Task 6)

```tsx
export function App() {
  return (
    <main style={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center' }}>
      <p>AFK4</p>
    </main>
  );
}
```

- [ ] **Step 11: Create `src/main.tsx`**

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './index.css';

const root = document.getElementById('root');
if (root) {
  createRoot(root).render(
    <StrictMode>
      <App />
    </StrictMode>
  );
}
```

- [ ] **Step 12: Create `src/test/setup.ts`** (identical to `AFK4.Platform.Web/src/test/setup.ts`)

```ts
import { afterEach, expect } from 'bun:test';
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'http://localhost/' });
expect.extend(matchers);
const { cleanup } = await import('@testing-library/react');
afterEach(() => cleanup());
```

- [ ] **Step 13: Create the smoke test `src/App.test.tsx`**

```tsx
import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { App } from './App';

it('renders the app brand mark', () => {
  render(<App />);
  expect(screen.getByText('AFK4')).toBeInTheDocument();
});
```

- [ ] **Step 14: Install and verify**

Run from repo root: `bun install`
Then from `src/AFK4.Customer.Web`: `bun test`
Expected: PASS (1 test). Then `bun run build` → Expected: clean `tsc -b` + vite build with no type errors.

- [ ] **Step 15: Commit**

```bash
git add package.json bun.lock src/AFK4.Customer.Web/
git commit -m "feat(portal): scaffold AFK4.Customer.Web (vite/react/tailwind/shadcn)"
```

---

## Task 3: Player token store (localStorage)

**Files:**
- Create: `src/AFK4.Customer.Web/src/auth/playerTokenStore.ts`
- Test: `src/AFK4.Customer.Web/src/auth/playerTokenStore.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
import { it, expect, beforeEach } from 'bun:test';
import {
  readPlayerSession, writePlayerSession, clearPlayerSession,
  playerSessionFromSignInResponse, isPlayerAccessTokenExpired,
  type PlayerSession
} from './playerTokenStore';

function makeStorage(): Storage {
  const map = new Map<string, string>();
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => { map.set(k, v); },
    removeItem: (k) => { map.delete(k); },
    clear: () => map.clear(),
    key: () => null,
    length: 0
  } as unknown as Storage;
}

const sample = {
  playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор',
  phoneVerified: true,
  accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
} satisfies PlayerSession;

let storage: Storage;
beforeEach(() => { storage = makeStorage(); });

it('round-trips a session through storage', () => {
  writePlayerSession(sample, storage);
  expect(readPlayerSession(storage)).toEqual(sample);
});

it('clear removes the session', () => {
  writePlayerSession(sample, storage);
  clearPlayerSession(storage);
  expect(readPlayerSession(storage)).toBeNull();
});

it('reads null when accessToken is missing', () => {
  storage.setItem('afk4.player.session', JSON.stringify({ ...sample, accessToken: '' }));
  expect(readPlayerSession(storage)).toBeNull();
});

it('maps a sign-in response into a session', () => {
  const s = playerSessionFromSignInResponse({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: false,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  });
  expect(s.playerAccountId).toBe('p1');
  expect(s.phoneVerified).toBe(false);
});

it('detects an expired access token', () => {
  expect(isPlayerAccessTokenExpired({ ...sample, accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z' })).toBe(true);
  expect(isPlayerAccessTokenExpired(sample)).toBe(false);
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `bun test src/auth/playerTokenStore.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `playerTokenStore.ts`**

```ts
import type { PlayerSignInResponse } from '../api/types';

const PLAYER_STORAGE_KEY = 'afk4.player.session';

export interface PlayerSession {
  playerAccountId: string;
  organizationId: string;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

// Players are on personal devices: persist across launches (localStorage), unlike staff.
function getStorage(): Storage | null {
  if (typeof globalThis === 'undefined') return null;
  return (globalThis as { localStorage?: Storage }).localStorage ?? null;
}

export function readPlayerSession(storage: Storage | null = getStorage()): PlayerSession | null {
  if (storage === null) return null;
  const raw = storage.getItem(PLAYER_STORAGE_KEY);
  if (raw === null || raw === '') return null;
  try {
    const parsed = JSON.parse(raw) as PlayerSession;
    if (typeof parsed.accessToken !== 'string' || parsed.accessToken.length === 0) return null;
    if (typeof parsed.organizationId !== 'string' || parsed.organizationId.length === 0) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function writePlayerSession(session: PlayerSession, storage: Storage | null = getStorage()): void {
  storage?.setItem(PLAYER_STORAGE_KEY, JSON.stringify(session));
}

export function clearPlayerSession(storage: Storage | null = getStorage()): void {
  storage?.removeItem(PLAYER_STORAGE_KEY);
}

export function playerSessionFromSignInResponse(response: PlayerSignInResponse): PlayerSession {
  return {
    playerAccountId: response.playerAccountId,
    organizationId: response.organizationId,
    displayName: response.displayName,
    phoneVerified: response.phoneVerified,
    accessToken: response.accessToken,
    accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
    refreshToken: response.refreshToken,
    refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
  };
}

export function isPlayerAccessTokenExpired(session: PlayerSession, now: Date = new Date()): boolean {
  const expiresAt = Date.parse(session.accessTokenExpiresAtUtc);
  if (Number.isNaN(expiresAt)) return true;
  return expiresAt <= now.getTime();
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `bun test src/auth/playerTokenStore.test.ts`
Expected: PASS (5 tests). (`api/types` is created in Task 4; the type-only import resolves at runtime to nothing, but `tsc -b` will flag it — defer the build check to Task 4.)

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/auth/
git commit -m "feat(portal): player token store (localStorage persistence)"
```

---

## Task 4: API clients — types, `playerApi`, `brandingApi`

**Files:**
- Create: `src/AFK4.Customer.Web/src/api/types.ts`
- Create: `src/AFK4.Customer.Web/src/api/playerApi.ts`
- Create: `src/AFK4.Customer.Web/src/api/brandingApi.ts`
- Test: `src/AFK4.Customer.Web/src/api/playerApi.test.ts`
- Test: `src/AFK4.Customer.Web/src/api/brandingApi.test.ts`

- [ ] **Step 1: Create `src/api/types.ts`** (TS mirrors of the backend contracts used in Plan 1)

```ts
export interface PlayerSignInRequest { organizationId: string; phoneNumber: string; password: string; }

export interface PlayerSignInResponse {
  playerAccountId: string;
  organizationId: string;
  displayName: string;
  phoneVerified: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface MoneyDto { currencyCode: string; minorUnits: number; }

export interface ActiveSessionDto {
  sessionId: string;
  seatId: string;
  seatName: string;
  startedAtUtc: string;
  durationMode: 'open' | 'fixed';
  remainingSeconds: number | null;
  accruedCostMinorUnits: number | null;
  currencyCode: string;
}

export interface PlayerDashboardDto {
  walletBalance: MoneyDto;
  debtBalance: MoneyDto;
  activeSession: ActiveSessionDto | null;
}

export interface TenantBrandingDto {
  organizationId: string;
  name: string;
  logoUrl: string | null;
  accentColor: string | null;
}
```

- [ ] **Step 2: Write the failing `playerApi` test**

```ts
import { it, expect, mock } from 'bun:test';
import { PlayerApiClient, PlayerApiError } from './playerApi';
import type { PlayerSession } from '../auth/playerTokenStore';

function okJson(body: unknown): Response {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}
function status(code: number, body: unknown = {}): Response {
  return { ok: code < 400, status: code, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}

const session: PlayerSession = {
  playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true,
  accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
};

it('signIn POSTs the request and returns the response', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ playerAccountId: 'p1', accessToken: 'a' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
  await client.signIn({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/public/player/sign-in');
  expect(init.method).toBe('POST');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
});

it('getDashboard attaches the Bearer header', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl, session, onSessionChanged: () => {} });
  await client.getDashboard();
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/dashboard');
  expect(init.headers.Authorization).toBe('Bearer tok');
});

it('refreshes once on 401 then retries with the new token', async () => {
  let updated: PlayerSession | null = null;
  const fetchImpl = mock()
    .mockResolvedValueOnce(status(401))
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true }))
    .mockResolvedValueOnce(okJson({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl, session, onSessionChanged: (s) => { updated = s; } });
  await client.getDashboard();
  expect(fetchImpl.mock.calls[1][0]).toBe('https://api.test/api/public/player/refresh');
  expect(fetchImpl.mock.calls[2][1].headers.Authorization).toBe('Bearer tok2');
  expect(updated?.accessToken).toBe('tok2');
});

it('throws PlayerApiError with the parsed message on a non-401 error', async () => {
  const fetchImpl = mock().mockResolvedValue(status(400, { error: 'amount must be positive' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl, session, onSessionChanged: () => {} });
  await expect(client.getDashboard()).rejects.toBeInstanceOf(PlayerApiError);
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `bun test src/api/playerApi.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement `playerApi.ts`**

```ts
import type { PlayerSession } from '../auth/playerTokenStore';
import { playerSessionFromSignInResponse } from '../auth/playerTokenStore';
import type {
  PlayerSignInRequest, PlayerSignInResponse, PlayerDashboardDto
} from './types';

export class PlayerApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'PlayerApiError';
  }
}

interface PlayerApiOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
  session: PlayerSession | null;
  onSessionChanged: (session: PlayerSession | null) => void;
}

export class PlayerApiClient {
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
  private session: PlayerSession | null;
  private readonly onSessionChanged: (session: PlayerSession | null) => void;

  constructor(options: PlayerApiOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.fetchImpl = options.fetchImpl ?? fetch;
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  signIn(request: PlayerSignInRequest): Promise<PlayerSignInResponse> {
    return this.publicPost<PlayerSignInResponse>('/api/public/player/sign-in', request);
  }

  getDashboard(): Promise<PlayerDashboardDto> {
    return this.authedGet<PlayerDashboardDto>('/api/me/dashboard');
  }

  private async publicPost<T>(path: string, body: unknown): Promise<T> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }

  private async authedGet<T>(path: string): Promise<T> {
    let response = await this.fetchImpl(`${this.baseUrl}${path}`, { method: 'GET', headers: this.buildHeaders() });
    if (response.status === 401 && (await this.refreshOnce())) {
      response = await this.fetchImpl(`${this.baseUrl}${path}`, { method: 'GET', headers: this.buildHeaders() });
    }
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }

  private buildHeaders(): Record<string, string> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.session) headers.Authorization = `Bearer ${this.session.accessToken}`;
    return headers;
  }

  private async refreshOnce(): Promise<boolean> {
    if (!this.session) return false;
    const response = await this.fetchImpl(`${this.baseUrl}/api/public/player/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: this.session.refreshToken })
    });
    if (!response.ok) {
      this.session = null;
      this.onSessionChanged(null);
      return false;
    }
    const next = playerSessionFromSignInResponse(JSON.parse(await response.text()));
    this.session = next;
    this.onSessionChanged(next);
    return true;
  }

  private static async toError(response: Response): Promise<PlayerApiError> {
    let message = `Request failed with status ${response.status}`;
    try {
      const parsed = JSON.parse(await response.text()) as { error?: string };
      if (parsed.error) message = parsed.error;
    } catch { /* keep default */ }
    return new PlayerApiError(response.status, message);
  }
}
```

(Note: `signIn`/`getDashboard` are the only methods needed for Plan 1; Plan 2 adds the rest using the same `authedGet`/`publicPost`/`authedSend` helpers — extend, don't duplicate.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `bun test src/api/playerApi.test.ts`
Expected: PASS (4 tests).

- [ ] **Step 6: Write the failing `brandingApi` test**

```ts
import { it, expect, mock } from 'bun:test';
import { fetchTenantBranding } from './brandingApi';

function okJson(body: unknown): Response {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}
function notFound(): Response {
  return { ok: false, status: 404, headers: new Map(), text: async () => '' } as unknown as Response;
}

it('GETs the branding endpoint for the tenant key', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ organizationId: 'org1', name: 'CyberX', logoUrl: null, accentColor: '#c8ff00' }));
  const result = await fetchTenantBranding('https://api.test', 'cyberx', fetchImpl);
  expect(fetchImpl.mock.calls[0][0]).toBe('https://api.test/api/public/tenant/cyberx/branding');
  expect(result?.name).toBe('CyberX');
});

it('returns null on 404 (unknown tenant)', async () => {
  const fetchImpl = mock().mockResolvedValue(notFound());
  expect(await fetchTenantBranding('https://api.test', 'nope', fetchImpl)).toBeNull();
});
```

- [ ] **Step 7: Run to verify it fails, then implement `brandingApi.ts`**

```ts
import type { TenantBrandingDto } from './types';

export async function fetchTenantBranding(
  baseUrl: string,
  tenantKey: string,
  fetchImpl: typeof fetch = fetch
): Promise<TenantBrandingDto | null> {
  const response = await fetchImpl(`${baseUrl.replace(/\/$/, '')}/api/public/tenant/${encodeURIComponent(tenantKey)}/branding`, {
    method: 'GET'
  });
  if (!response.ok) return null;
  return JSON.parse(await response.text()) as TenantBrandingDto;
}
```

- [ ] **Step 8: Run both API tests + the build**

Run: `bun test src/api` then `bun run build`
Expected: API tests PASS; `tsc -b` clean (the Task 3 type-only import now resolves).

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Customer.Web/src/api/
git commit -m "feat(portal): player API client + branding API + contract types"
```

---

## Task 5: Branding bootstrap — resolve tenant key, apply theme

**Files:**
- Create: `src/AFK4.Customer.Web/src/branding/resolveTenantKey.ts`
- Create: `src/AFK4.Customer.Web/src/branding/applyTheme.ts`
- Test: `src/AFK4.Customer.Web/src/branding/resolveTenantKey.test.ts`
- Test: `src/AFK4.Customer.Web/src/branding/applyTheme.test.ts`

- [ ] **Step 1: Write the failing `resolveTenantKey` test**

```ts
import { it, expect } from 'bun:test';
import { resolveTenantKey } from './resolveTenantKey';

function makeStorage(seed?: Record<string, string>): Storage {
  const map = new Map<string, string>(Object.entries(seed ?? {}));
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => { map.set(k, v); },
    removeItem: (k) => { map.delete(k); },
    clear: () => map.clear(), key: () => null, length: 0
  } as unknown as Storage;
}

it('prefers the ?tenant= query override and caches it', () => {
  const storage = makeStorage();
  expect(resolveTenantKey('club.portal.afk4.net', '?tenant=override', storage)).toBe('override');
  expect(storage.getItem('afk4.player.tenantKey')).toBe('override');
});

it('derives the key from a subdomain', () => {
  expect(resolveTenantKey('cyberx.portal.afk4.net', '', makeStorage())).toBe('cyberx');
});

it('falls back to the cached key when nothing else is present', () => {
  expect(resolveTenantKey('localhost', '', makeStorage({ 'afk4.player.tenantKey': 'demo' }))).toBe('demo');
});

it('returns null when there is nothing to resolve', () => {
  expect(resolveTenantKey('localhost', '', makeStorage())).toBeNull();
});
```

- [ ] **Step 2: Run to verify it fails, then implement `resolveTenantKey.ts`**

```ts
const TENANT_KEY_STORAGE = 'afk4.player.tenantKey';
// Hosts that are not a tenant subdomain (local dev, bare apex, the portal host itself).
const NON_TENANT_HOSTS = new Set(['localhost', '127.0.0.1', 'portal', 'www']);

export function resolveTenantKey(
  hostname: string,
  search: string,
  storage: Storage | null = (globalThis as { localStorage?: Storage }).localStorage ?? null
): string | null {
  const override = new URLSearchParams(search).get('tenant');
  if (override) {
    storage?.setItem(TENANT_KEY_STORAGE, override);
    return override;
  }

  const firstLabel = hostname.split('.')[0];
  if (firstLabel && !NON_TENANT_HOSTS.has(firstLabel) && hostname.includes('.')) {
    storage?.setItem(TENANT_KEY_STORAGE, firstLabel);
    return firstLabel;
  }

  return storage?.getItem(TENANT_KEY_STORAGE) ?? null;
}
```

- [ ] **Step 3: Write the failing `applyTheme` test**

```ts
import { it, expect } from 'bun:test';
import { applyTheme } from './applyTheme';
import type { TenantBrandingDto } from '../api/types';

it('writes the accent color into the --accent CSS variable', () => {
  applyTheme({ organizationId: 'o', name: 'CyberX', logoUrl: null, accentColor: '#ff0066' });
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#ff0066');
});

it('keeps the default accent when branding is null or has no color', () => {
  document.documentElement.style.removeProperty('--accent');
  applyTheme(null);
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('');
  applyTheme({ organizationId: 'o', name: 'X', logoUrl: null, accentColor: null });
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('');
});
```

- [ ] **Step 4: Run to verify it fails, then implement `applyTheme.ts`**

```ts
import type { TenantBrandingDto } from '../api/types';

// Simple hex contrast pick for the accent foreground (dark text on light accent, etc.).
function readableForeground(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return '#101314';
  const n = parseInt(m[1], 16);
  const r = (n >> 16) & 0xff, g = (n >> 8) & 0xff, b = n & 0xff;
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.6 ? '#101314' : '#ffffff';
}

export function applyTheme(branding: TenantBrandingDto | null): void {
  const root = document.documentElement;
  if (branding?.accentColor) {
    root.style.setProperty('--accent', branding.accentColor);
    root.style.setProperty('--accent-fg', readableForeground(branding.accentColor));
  }
  if (branding?.name) document.title = branding.name;
}
```

- [ ] **Step 5: Run both branding tests + build**

Run: `bun test src/branding` then `bun run build`
Expected: tests PASS; build clean.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Customer.Web/src/branding/
git commit -m "feat(portal): tenant key resolution + theme application"
```

---

## Task 6: App shell — router, auth gate, bottom nav, Sign-in screen

**Files:**
- Create: `src/AFK4.Customer.Web/src/components/ui/button.tsx` (port from `AFK4.Platform.Web/src/components/ui/button.tsx`)
- Create: `src/AFK4.Customer.Web/src/components/ui/input.tsx` (port from `AFK4.Platform.Web`)
- Create: `src/AFK4.Customer.Web/src/components/BottomNav.tsx`
- Create: `src/AFK4.Customer.Web/src/components/AppShell.tsx`
- Create: `src/AFK4.Customer.Web/src/screens/auth/SignInScreen.tsx`
- Create: `src/AFK4.Customer.Web/src/routing.ts`
- Replace: `src/AFK4.Customer.Web/src/App.tsx`
- Replace: `src/AFK4.Customer.Web/src/App.test.tsx`
- Test: `src/AFK4.Customer.Web/src/routing.test.ts`
- Test: `src/AFK4.Customer.Web/src/screens/auth/SignInScreen.test.tsx`

- [ ] **Step 1: Port the `button` and `input` shadcn primitives**

Copy `src/AFK4.Platform.Web/src/components/ui/button.tsx` and `input.tsx` into the matching paths under `AFK4.Customer.Web`, unchanged (they depend only on `@/lib/utils`, `class-variance-authority`, `radix-ui`, already installed).

- [ ] **Step 2: Write the failing routing test**

```ts
import { it, expect } from 'bun:test';
import { resolvePlayerRoute, routePath } from './routing';

it('maps the root path to the dashboard tab', () => {
  expect(resolvePlayerRoute('/').kind).toBe('dashboard');
});

it('maps /history to the history tab', () => {
  expect(resolvePlayerRoute('/history').kind).toBe('history');
});

it('parses a receipt route with its session id', () => {
  const route = resolvePlayerRoute('/history/abc-123/receipt');
  expect(route).toEqual({ kind: 'receipt', sessionId: 'abc-123' });
});

it('falls back to the dashboard for unknown paths', () => {
  expect(resolvePlayerRoute('/nonsense').kind).toBe('dashboard');
});

it('round-trips a tab through routePath', () => {
  expect(routePath({ kind: 'reservations' })).toBe('/reservations');
});
```

- [ ] **Step 3: Run to verify it fails, then implement `routing.ts`**

```ts
export type PlayerRoute =
  | { kind: 'dashboard' }
  | { kind: 'history' }
  | { kind: 'receipt'; sessionId: string }
  | { kind: 'reservations' }
  | { kind: 'profile' };

export type PlayerTab = 'dashboard' | 'history' | 'reservations' | 'profile';

export function resolvePlayerRoute(pathname: string): PlayerRoute {
  const parts = pathname.split('/').filter(Boolean);
  if (parts.length === 0) return { kind: 'dashboard' };
  if (parts[0] === 'history' && parts[2] === 'receipt') return { kind: 'receipt', sessionId: parts[1] };
  if (parts[0] === 'history') return { kind: 'history' };
  if (parts[0] === 'reservations') return { kind: 'reservations' };
  if (parts[0] === 'profile') return { kind: 'profile' };
  return { kind: 'dashboard' };
}

export function routePath(route: PlayerRoute): string {
  switch (route.kind) {
    case 'dashboard': return '/';
    case 'history': return '/history';
    case 'receipt': return `/history/${route.sessionId}/receipt`;
    case 'reservations': return '/reservations';
    case 'profile': return '/profile';
  }
}
```

- [ ] **Step 4: Write the failing Sign-in screen test**

```tsx
import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { SignInScreen } from './SignInScreen';

it('submits phone + password and reports the resulting session', async () => {
  const onSignedIn = mock();
  const signIn = mock().mockResolvedValue({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  });

  render(<SignInScreen organizationId="org1" brandName="CyberX" signIn={signIn} onSignedIn={onSignedIn} />);
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000001' } });
  fireEvent.change(screen.getByLabelText('PIN или пароль'), { target: { value: '1234' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));

  await waitFor(() => expect(onSignedIn).toHaveBeenCalled());
  expect(signIn).toHaveBeenCalledWith({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
});

it('shows a generic error when sign-in fails', async () => {
  const signIn = mock().mockRejectedValue(new Error('nope'));
  render(<SignInScreen organizationId="org1" brandName="CyberX" signIn={signIn} onSignedIn={() => {}} />);
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000001' } });
  fireEvent.change(screen.getByLabelText('PIN или пароль'), { target: { value: 'x' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Неверный номер или пароль');
});
```

- [ ] **Step 5: Run to verify it fails, then implement `SignInScreen.tsx`**

```tsx
import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { PlayerSignInRequest, PlayerSignInResponse } from '@/api/types';

interface SignInScreenProps {
  organizationId: string;
  brandName: string;
  signIn: (request: PlayerSignInRequest) => Promise<PlayerSignInResponse>;
  onSignedIn: (response: PlayerSignInResponse) => void;
}

export function SignInScreen({ organizationId, brandName, signIn, onSignedIn }: SignInScreenProps) {
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      const response = await signIn({ organizationId, phoneNumber, password });
      onSignedIn(response);
    } catch {
      setError('Неверный номер или пароль');
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col justify-center gap-8 px-6 py-12">
      <header className="space-y-1">
        <p className="text-sm text-[var(--text-2)]">Вход в портал</p>
        <h1 className="text-3xl font-extrabold tracking-tight">{brandName}</h1>
      </header>

      <form className="space-y-4" onSubmit={handleSubmit}>
        <div className="space-y-1.5">
          <label htmlFor="phone" className="text-sm text-[var(--text-2)]">Телефон</label>
          <Input id="phone" type="tel" inputMode="tel" autoComplete="tel"
            value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="+992 90 000 00 01" />
        </div>
        <div className="space-y-1.5">
          <label htmlFor="password" className="text-sm text-[var(--text-2)]">PIN или пароль</label>
          <Input id="password" type="password" autoComplete="current-password"
            value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>

        {error && <p role="alert" className="text-sm text-red-400">{error}</p>}

        <Button type="submit" className="w-full" disabled={pending}>
          {pending ? 'Входим…' : 'Войти'}
        </Button>
      </form>

      <Button type="button" variant="outline" className="w-full" disabled
        title="Вход по коду из SMS появится позже">
        Войти по SMS-коду · скоро
      </Button>
    </main>
  );
}
```

- [ ] **Step 6: Implement `BottomNav.tsx`**

```tsx
import { Home, Clock, CalendarDays, User } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { PlayerTab } from '@/routing';

const TABS: { tab: PlayerTab; label: string; Icon: typeof Home }[] = [
  { tab: 'dashboard', label: 'Главная', Icon: Home },
  { tab: 'history', label: 'История', Icon: Clock },
  { tab: 'reservations', label: 'Брони', Icon: CalendarDays },
  { tab: 'profile', label: 'Профиль', Icon: User }
];

export function BottomNav({ active, onNavigate }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void }) {
  return (
    <nav className="sticky bottom-0 grid grid-cols-4 border-t border-[var(--color-border)] bg-[var(--color-surface)]"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}>
      {TABS.map(({ tab, label, Icon }) => (
        <button key={tab} type="button" onClick={() => onNavigate(tab)}
          aria-current={active === tab ? 'page' : undefined}
          className={cn(
            'flex min-h-[56px] flex-col items-center justify-center gap-1 text-xs transition-colors',
            'focus-visible:outline-2 focus-visible:outline-[var(--accent)]',
            active === tab ? 'text-[var(--accent)]' : 'text-[var(--text-3)] hover:text-[var(--text-2)]'
          )}>
          <Icon size={20} aria-hidden />
          {label}
        </button>
      ))}
    </nav>
  );
}
```

- [ ] **Step 7: Implement `AppShell.tsx`** (frame + bottom nav around the active screen)

```tsx
import type { ReactNode } from 'react';
import { BottomNav } from './BottomNav';
import type { PlayerTab } from '@/routing';

export function AppShell({ active, onNavigate, children }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <div className="flex-1 overflow-y-auto pb-2">{children}</div>
      <BottomNav active={active} onNavigate={onNavigate} />
    </div>
  );
}
```

- [ ] **Step 8: Replace `App.tsx`** (auth gate + router wiring)

```tsx
import { useCallback, useMemo, useState } from 'react';
import { PlayerApiClient } from './api/playerApi';
import type { PlayerSignInResponse } from './api/types';
import {
  readPlayerSession, writePlayerSession, clearPlayerSession,
  playerSessionFromSignInResponse, type PlayerSession
} from './auth/playerTokenStore';
import { resolvePlayerRoute, routePath, type PlayerRoute, type PlayerTab } from './routing';
import { AppShell } from './components/AppShell';
import { SignInScreen } from './screens/auth/SignInScreen';
import { DashboardScreen } from './screens/dashboard/DashboardScreen';

const API_BASE = import.meta.env.VITE_API_BASE ?? '';

function tabForRoute(route: PlayerRoute): PlayerTab {
  if (route.kind === 'receipt') return 'history';
  return route.kind;
}

export function App() {
  const [session, setSession] = useState<PlayerSession | null>(() => readPlayerSession());
  const [route, setRoute] = useState<PlayerRoute>(() =>
    resolvePlayerRoute(typeof window === 'undefined' ? '/' : window.location.pathname));

  const onSessionChanged = useCallback((next: PlayerSession | null) => {
    setSession(next);
    if (next) writePlayerSession(next); else clearPlayerSession();
  }, []);

  const api = useMemo(
    () => new PlayerApiClient({ baseUrl: API_BASE, session, onSessionChanged }),
    [session, onSessionChanged]
  );

  const navigate = useCallback((tab: PlayerTab) => {
    const next: PlayerRoute = { kind: tab };
    setRoute(next);
    if (typeof window !== 'undefined') window.history.pushState(null, '', routePath(next));
  }, []);

  const handleSignedIn = useCallback((response: PlayerSignInResponse) => {
    onSessionChanged(playerSessionFromSignInResponse(response));
  }, [onSessionChanged]);

  if (!session) {
    return (
      <SignInScreen
        organizationId={session ? (session as PlayerSession).organizationId : (import.meta.env.VITE_DEMO_ORG_ID ?? '')}
        brandName="AFK4"
        signIn={(req) => api.signIn(req)}
        onSignedIn={handleSignedIn}
      />
    );
  }

  return (
    <AppShell active={tabForRoute(route)} onNavigate={navigate}>
      {route.kind === 'dashboard' && <DashboardScreen api={api} displayName={session.displayName} />}
      {route.kind !== 'dashboard' && (
        <section className="px-6 py-10 text-[var(--text-2)]">Скоро здесь появится этот раздел.</section>
      )}
    </AppShell>
  );
}
```

(Note: `SignInScreen` needs the real `organizationId` from branding — Task 6's `App.tsx` uses a `VITE_DEMO_ORG_ID` env stand-in so the gate compiles and tests pass; Plan 2's branding-bootstrap task replaces this by wiring `useBranding()` to feed `organizationId` + `brandName`. The placeholder is intentional and localized to one prop.)

- [ ] **Step 9: Replace `App.test.tsx`** (gate behavior)

```tsx
import { it, expect, beforeEach } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { App } from './App';

beforeEach(() => { globalThis.localStorage?.clear(); });

it('shows the sign-in screen when there is no session', () => {
  render(<App />);
  expect(screen.getByRole('button', { name: 'Войти' })).toBeInTheDocument();
});

it('shows the app shell + dashboard tab when a session exists', () => {
  globalThis.localStorage?.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
  render(<App />);
  expect(screen.getByRole('navigation')).toBeInTheDocument();
  expect(screen.getByText('Главная')).toBeInTheDocument();
});
```

- [ ] **Step 10: Run the screen + routing + app tests**

Run: `bun test src/routing.test.ts src/screens/auth src/App.test.tsx`
Expected: PASS. (The `App.test.tsx` second case requires `DashboardScreen` from Task 7 — if running this task in isolation before Task 7, stub `DashboardScreen` to a `<div>` and replace it in Task 7. Prefer running Tasks 6 and 7 back-to-back.)

- [ ] **Step 11: Commit**

```bash
git add src/AFK4.Customer.Web/src/components/ src/AFK4.Customer.Web/src/screens/auth/ src/AFK4.Customer.Web/src/routing.ts src/AFK4.Customer.Web/src/routing.test.ts src/AFK4.Customer.Web/src/App.tsx src/AFK4.Customer.Web/src/App.test.tsx
git commit -m "feat(portal): app shell, hand-rolled router, bottom nav, sign-in screen"
```

---

## Task 7: Dashboard + live session card

**Files:**
- Create: `src/AFK4.Customer.Web/src/lib/money.ts`
- Create: `src/AFK4.Customer.Web/src/screens/dashboard/liveSession.ts`
- Create: `src/AFK4.Customer.Web/src/screens/dashboard/LiveSessionCard.tsx`
- Create: `src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/dashboard/liveSession.test.ts`
- Test: `src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.test.tsx`

- [ ] **Step 1: Write the failing `liveSession` test** (pure projection helpers — the math, no React)

```ts
import { it, expect } from 'bun:test';
import { elapsedSeconds, formatClock, projectRemainingSeconds } from './liveSession';

it('formats seconds as HH:MM:SS', () => {
  expect(formatClock(0)).toBe('00:00:00');
  expect(formatClock(6138)).toBe('01:42:18');
});

it('computes elapsed seconds from the start time', () => {
  const start = '2026-06-03T20:00:00Z';
  const now = new Date('2026-06-03T21:42:18Z');
  expect(elapsedSeconds(start, now)).toBe(6138);
});

it('counts a fixed session down and clamps at zero', () => {
  const fetchedAt = new Date('2026-06-03T20:00:00Z');
  const now = new Date('2026-06-03T20:00:30Z');
  expect(projectRemainingSeconds(100, fetchedAt, now)).toBe(70);
  expect(projectRemainingSeconds(10, fetchedAt, now)).toBe(0);
});
```

- [ ] **Step 2: Run to verify it fails, then implement `liveSession.ts`**

```ts
export function elapsedSeconds(startedAtUtc: string, now: Date = new Date()): number {
  const started = Date.parse(startedAtUtc);
  if (Number.isNaN(started)) return 0;
  return Math.max(0, Math.floor((now.getTime() - started) / 1000));
}

export function projectRemainingSeconds(remainingAtFetch: number, fetchedAt: Date, now: Date = new Date()): number {
  const drift = Math.floor((now.getTime() - fetchedAt.getTime()) / 1000);
  return Math.max(0, remainingAtFetch - drift);
}

export function formatClock(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hh = String(Math.floor(s / 3600)).padStart(2, '0');
  const mm = String(Math.floor((s % 3600) / 60)).padStart(2, '0');
  const ss = String(s % 60).padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}
```

- [ ] **Step 3: Implement `src/lib/money.ts`** (minor→major formatting via `@afk4/money`)

```ts
import { minorToMajor } from '@afk4/money';

export function formatMoney(minorUnits: number, currencyCode: string): string {
  const major = minorToMajor(minorUnits);
  return `${major.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currencyCode}`;
}
```

(`@afk4/money` exposes `minorToMajor(minorUnits: number): number` — a single-arg helper; do not hand-roll decimal math.)

- [ ] **Step 4: Write the failing `DashboardScreen` test**

```tsx
import { it, expect, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { DashboardScreen } from './DashboardScreen';

function apiWith(dashboard: unknown) {
  return { getDashboard: mock().mockResolvedValue(dashboard) } as unknown as import('@/api/playerApi').PlayerApiClient;
}

it('renders the wallet balance and a no-session empty state', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null
  });
  render(<DashboardScreen api={api} displayName="Фёдор" />);
  expect(await screen.findByText('245,00 TJS')).toBeInTheDocument();
  expect(screen.getByText('Нет активной сессии')).toBeInTheDocument();
});

it('renders the active session seat and a running timer', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: {
      sessionId: 's1', seatId: 'seat1', seatName: 'PC-14 · VIP',
      startedAtUtc: '2026-06-03T20:00:00Z', durationMode: 'open',
      remainingSeconds: null, accruedCostMinorUnits: 3850, currencyCode: 'TJS'
    }
  });
  render(<DashboardScreen api={api} displayName="Фёдор" />);
  expect(await screen.findByText('PC-14 · VIP')).toBeInTheDocument();
  await waitFor(() => expect(screen.getByTestId('session-timer').textContent).toMatch(/^\d\d:\d\d:\d\d$/));
});
```

- [ ] **Step 5: Run to verify it fails, then implement `LiveSessionCard.tsx`**

```tsx
import { useEffect, useState } from 'react';
import type { ActiveSessionDto } from '@/api/types';
import { elapsedSeconds, projectRemainingSeconds, formatClock } from './liveSession';
import { formatMoney } from '@/lib/money';

export function LiveSessionCard({ session, fetchedAt }: { session: ActiveSessionDto; fetchedAt: Date }) {
  const [, setTick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, []);

  const clock = session.durationMode === 'fixed'
    ? formatClock(projectRemainingSeconds(session.remainingSeconds ?? 0, fetchedAt))
    : formatClock(elapsedSeconds(session.startedAtUtc));

  return (
    <section className="rounded-2xl border-l-2 border-[var(--accent)] bg-[var(--color-surface)] p-4">
      <div className="flex items-center justify-between text-xs text-[var(--text-2)]">
        <span>{session.durationMode === 'fixed' ? 'ОСТАЛОСЬ' : 'СЕССИЯ АКТИВНА'}</span>
        <span className="font-bold text-[var(--text-1)]">{session.seatName}</span>
      </div>
      <p data-testid="session-timer" className="mt-1.5 text-3xl font-extrabold tracking-tight">{clock}</p>
      {session.durationMode === 'open' && session.accruedCostMinorUnits != null && (
        <p className="mt-1 text-sm text-[var(--text-2)]">
          ≈ <span className="text-[var(--accent)]">{formatMoney(session.accruedCostMinorUnits, session.currencyCode)}</span> накоплено
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 6: Implement `DashboardScreen.tsx`** (fetch + 30s poll + states)

```tsx
import { useEffect, useState } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerDashboardDto } from '@/api/types';
import { formatMoney } from '@/lib/money';
import { LiveSessionCard } from './LiveSessionCard';

type Load = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: PlayerDashboardDto; fetchedAt: Date };

export function DashboardScreen({ api, displayName }: { api: PlayerApiClient; displayName: string }) {
  const [load, setLoad] = useState<Load>({ state: 'loading' });

  useEffect(() => {
    let cancelled = false;
    async function refresh() {
      try {
        const data = await api.getDashboard();
        if (!cancelled) setLoad({ state: 'ready', data, fetchedAt: new Date() });
      } catch {
        if (!cancelled) setLoad((prev) => (prev.state === 'ready' ? prev : { state: 'error' }));
      }
    }
    void refresh();
    const id = setInterval(refresh, 30_000);
    return () => { cancelled = true; clearInterval(id); };
  }, [api]);

  return (
    <main className="space-y-5 px-6 py-8">
      <header>
        <p className="text-sm text-[var(--text-2)]">С возвращением</p>
        <h1 className="text-2xl font-extrabold tracking-tight">{displayName}</h1>
      </header>

      {load.state === 'loading' && <div className="h-28 animate-pulse rounded-2xl bg-[var(--color-surface)]" />}
      {load.state === 'error' && <p className="text-sm text-red-400">Не удалось загрузить данные. Проверьте соединение.</p>}

      {load.state === 'ready' && (
        <>
          <section className="rounded-2xl bg-[var(--color-surface)] p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">Баланс кошелька</p>
            <p className="mt-1 text-3xl font-extrabold tracking-tight">
              {formatMoney(load.data.walletBalance.minorUnits, load.data.walletBalance.currencyCode)}
            </p>
            {load.data.debtBalance.minorUnits > 0 && (
              <p className="mt-1 text-sm text-red-400">
                Долг: {formatMoney(load.data.debtBalance.minorUnits, load.data.debtBalance.currencyCode)}
              </p>
            )}
          </section>

          {load.data.activeSession
            ? <LiveSessionCard session={load.data.activeSession} fetchedAt={load.fetchedAt} />
            : <section className="rounded-2xl border border-dashed border-[var(--color-border)] p-6 text-center text-[var(--text-2)]">Нет активной сессии</section>}
        </>
      )}
    </main>
  );
}
```

- [ ] **Step 7: Run the dashboard tests + full app build**

Run: `bun test src/screens/dashboard` then `bun test` then `bun run build`
Expected: all PASS; `tsc -b` + vite build clean.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Customer.Web/src/lib/money.ts src/AFK4.Customer.Web/src/screens/dashboard/
git commit -m "feat(portal): dashboard with balance + live session card"
```

---

## Self-review notes

- **Spec coverage (build-seq 1–6):** Task 1 = backend branding; Task 2 = scaffold; Tasks 3–4 = token store + API clients; Task 5 = branding bootstrap helpers (`resolveTenantKey`/`applyTheme`; the `useBranding` *wiring* into `App.tsx` lands in Plan 2's first task, where `organizationId`/`brandName` replace the `VITE_DEMO_ORG_ID` stand-in — called out at Task 6 Step 8); Task 6 = app shell + router + sign-in; Task 7 = dashboard + live session. D8 gate, wallet, history, etc. are Plan 2.
- **Cross-task type consistency:** `PlayerSession`, `PlayerSignInResponse`, `ActiveSessionDto`, `PlayerDashboardDto`, `PlayerApiClient.signIn/getDashboard`, `resolvePlayerRoute/routePath`, `formatMoney`, `formatClock` are defined once and referenced with the same signatures throughout.
- **Known intentional stand-in:** `App.tsx` reads `organizationId` for the sign-in form from `VITE_DEMO_ORG_ID` until Plan 2 wires the branding fetch. This keeps Plan 1 independently runnable and testable.
- **Money:** `formatMoney` delegates to `@afk4/money` — verify the exact exported helper name in `packages/money/src` during Task 7 Step 3.
