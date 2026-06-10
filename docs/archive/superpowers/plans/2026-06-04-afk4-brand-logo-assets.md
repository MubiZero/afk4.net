# AFK4.NET Brand Logo Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the canonical AFK4.NET brand logo assets (master SVGs for the command-grid mark and lockups, plus a generation pipeline that derives favicons, PWA PNGs, and tray/MSI-ready `.ico`/`.png`) and replace the placeholder lime "A4" brand across all three web frontends.

**Architecture:** A single `brand/` directory holds hand-authored master SVGs (the one source of truth) whose colors match the brand spec tokens. A Node/Bun generation script (`scripts/build-brand-assets.mjs`) rasterizes the mark-only icon SVGs into every required PNG size and a multi-resolution `.ico`, writing to `brand/dist/`. The three Vite frontends then consume the SVG favicon plus generated PWA PNGs from their `public/` dirs. Rasterized outputs are **mark-only** (no text), so rasterization never depends on a font being installed; text lockups stay vector-only for web/print.

**Tech Stack:** Hand-authored SVG; Bun + `@resvg/resvg-js` (SVG→PNG, ships prebuilt win32-x64 binary) + `png-to-ico` (PNG→ICO, pure JS); Bun's built-in test runner (`bun:test`); existing Vite/React frontends.

**Source of truth:** [Brand & positioning spec](../specs/2026-06-04-afk4-brand-positioning-design.md). Key tokens used below: accent dark `#2DD4A7`, accent light `#0B9E74`, inactive-cell dark `#173028`, inactive-cell light `#D9E6E1`, icon background `#0E2019`, text dark `#E2F1EC`, text light `#0B1F18`. Mark = 3×3 rounded grid, active cells on the diagonal **top-left / center / bottom-right**.

---

## File Structure

Created:
- `brand/afk4-mark.svg` — command-grid mark, dark surfaces (transparent bg)
- `brand/afk4-mark-light.svg` — mark for light surfaces
- `brand/afk4-icon.svg` — mark on rounded dark background (app/tray/favicon)
- `brand/afk4-icon-maskable.svg` — same with PWA maskable safe-zone padding
- `brand/afk4-logo-horizontal.svg` / `-light.svg` — mark + `AFK4.NET` wordmark
- `brand/afk4-logo-vertical.svg` / `-light.svg` — stacked lockup
- `brand/afk4-brand.test.ts` — asset verification (bun:test)
- `brand/README.md` — asset map, tokens, regeneration instructions
- `scripts/build-brand-assets.mjs` — generation pipeline
- `brand/dist/*` — generated `.png` sizes + `afk4.ico` (committed)
- `src/AFK4.Platform.Web/public/favicon.svg`, `src/AFK4.Operator.App.Web/public/favicon.svg` — new favicons

Modified:
- `package.json` (root) — add devDeps + `build:brand` script
- `src/AFK4.Customer.Web/public/favicon.svg` — replace lime placeholder
- `src/AFK4.Customer.Web/public/pwa-192.png` / `pwa-512.png` / `pwa-maskable-512.png` — regenerate (created if absent)
- `src/AFK4.Platform.Web/index.html`, `src/AFK4.Operator.App.Web/index.html` — add `<link rel="icon">`

Out of scope (separate follow-up plans): WPF `ApplicationIcon` + `NotifyIcon` tray wiring, MSI icon wiring, color/type design tokens in app code, the marketing landing page, English brand copy.

---

## Task 1: Command-grid mark master SVGs

**Files:**
- Create: `brand/afk4-mark.svg`
- Create: `brand/afk4-mark-light.svg`
- Create: `brand/afk4-brand.test.ts`

- [ ] **Step 1: Write the failing test**

Create `brand/afk4-brand.test.ts`:

```ts
import { test, expect } from "bun:test";
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const BRAND = import.meta.dir; // brand/ dir

function read(rel: string): string {
  return readFileSync(join(BRAND, rel), "utf8");
}

test("dark mark uses accent #2DD4A7 on three diagonal cells", () => {
  const svg = read("afk4-mark.svg");
  expect(svg).toContain("<svg");
  expect(svg).toContain('viewBox="0 0 52 52"');
  const accents = svg.match(/#2DD4A7/gi) ?? [];
  expect(accents.length).toBe(3); // exactly three active cells
  expect(svg).toContain("#173028"); // inactive cells present
});

test("light mark uses deep accent #0B9E74 and light inactive cells", () => {
  const svg = read("afk4-mark-light.svg");
  expect((svg.match(/#0B9E74/gi) ?? []).length).toBe(3);
  expect(svg).toContain("#D9E6E1");
});

test("no lime placeholder remains in brand sources", () => {
  expect(read("afk4-mark.svg").toLowerCase()).not.toContain("#c8ff00");
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `bun test brand/afk4-brand.test.ts`
Expected: FAIL — `ENOENT` reading `afk4-mark.svg` (file does not exist yet).

- [ ] **Step 3: Create the dark mark**

Create `brand/afk4-mark.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 52 52" width="52" height="52" role="img" aria-label="AFK4.NET">
  <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#173028"/>
  <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
</svg>
```

- [ ] **Step 4: Create the light mark**

Create `brand/afk4-mark-light.svg` (identical geometry; active cells `#0B9E74`, inactive `#D9E6E1`):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 52 52" width="52" height="52" role="img" aria-label="AFK4.NET">
  <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#0B9E74"/>
  <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#0B9E74"/>
  <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
  <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#0B9E74"/>
</svg>
```

- [ ] **Step 5: Run test to verify it passes**

Run: `bun test brand/afk4-brand.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add brand/afk4-mark.svg brand/afk4-mark-light.svg brand/afk4-brand.test.ts
git commit -m "feat(brand): add AFK4.NET command-grid mark master SVGs"
```

---

## Task 2: Icon + maskable + lockup SVGs

**Files:**
- Create: `brand/afk4-icon.svg`, `brand/afk4-icon-maskable.svg`
- Create: `brand/afk4-logo-horizontal.svg`, `brand/afk4-logo-horizontal-light.svg`
- Create: `brand/afk4-logo-vertical.svg`, `brand/afk4-logo-vertical-light.svg`
- Modify: `brand/afk4-brand.test.ts`

- [ ] **Step 1: Add failing tests for icon + lockups**

Append to `brand/afk4-brand.test.ts`:

```ts
test("app icon has dark rounded background and three accent cells", () => {
  const svg = read("afk4-icon.svg");
  expect(svg).toContain('viewBox="0 0 64 64"');
  expect(svg).toContain("#0E2019");                       // bg
  expect((svg.match(/#2DD4A7/gi) ?? []).length).toBe(3);  // active cells
});

test("maskable icon keeps content inside the safe zone via scale", () => {
  const svg = read("afk4-icon-maskable.svg");
  expect(svg).toContain('viewBox="0 0 64 64"');
  expect(svg).toContain("scale(");
});

test("horizontal lockup has wordmark with accent .NET", () => {
  const svg = read("afk4-logo-horizontal.svg");
  expect(svg).toContain(">AFK4<");
  expect(svg).toContain('fill="#2DD4A7">.NET<');
  expect(svg).toContain("#E2F1EC"); // wordmark text color (dark surface)
});

test("vertical lockup exists and stacks mark over wordmark", () => {
  const svg = read("afk4-logo-vertical.svg");
  expect(svg).toContain(">AFK4<");
  expect(svg).toContain("translate");
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `bun test brand/afk4-brand.test.ts`
Expected: FAIL — new files missing (`afk4-icon.svg` etc.).

- [ ] **Step 3: Create the app icon**

Create `brand/afk4-icon.svg` (grid centered on a rounded dark tile; `translate(6 6)` centers the 52-unit grid in 64):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64" role="img" aria-label="AFK4.NET">
  <rect width="64" height="64" rx="14" fill="#0E2019"/>
  <g transform="translate(6 6)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  </g>
</svg>
```

- [ ] **Step 4: Create the maskable icon**

Create `brand/afk4-icon-maskable.svg` (full-bleed bg + grid scaled to ~70% and centered, so the mark sits inside the maskable safe zone):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64" role="img" aria-label="AFK4.NET">
  <rect width="64" height="64" fill="#0E2019"/>
  <g transform="translate(13.8 13.8) scale(0.7)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  </g>
</svg>
```

- [ ] **Step 5: Create the horizontal lockup (dark + light)**

Create `brand/afk4-logo-horizontal.svg` (mark scaled to 40px high at left, wordmark right; font stack with system fallbacks — convert to outlines only if pixel-exact print rendering is later required):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 250 56" width="250" height="56" role="img" aria-label="AFK4.NET">
  <g transform="translate(2 8) scale(0.769)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  </g>
  <text x="56" y="38" font-family="Inter, 'Segoe UI', system-ui, sans-serif" font-size="30" font-weight="800" letter-spacing="-1.5" fill="#E2F1EC">AFK4<tspan fill="#2DD4A7">.NET</tspan></text>
</svg>
```

Create `brand/afk4-logo-horizontal-light.svg` (same, wordmark `fill="#0B1F18"`, `.NET` `fill="#0B9E74"`, mark cells light: active `#0B9E74`, inactive `#D9E6E1`):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 250 56" width="250" height="56" role="img" aria-label="AFK4.NET">
  <g transform="translate(2 8) scale(0.769)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#0B9E74"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#0B9E74"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#0B9E74"/>
  </g>
  <text x="56" y="38" font-family="Inter, 'Segoe UI', system-ui, sans-serif" font-size="30" font-weight="800" letter-spacing="-1.5" fill="#0B1F18">AFK4<tspan fill="#0B9E74">.NET</tspan></text>
</svg>
```

- [ ] **Step 6: Create the vertical lockup (dark + light)**

Create `brand/afk4-logo-vertical.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 96" width="120" height="96" role="img" aria-label="AFK4.NET">
  <g transform="translate(34 6)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#173028"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#2DD4A7"/>
  </g>
  <text x="60" y="86" text-anchor="middle" font-family="Inter, 'Segoe UI', system-ui, sans-serif" font-size="22" font-weight="800" letter-spacing="-1" fill="#E2F1EC">AFK4<tspan fill="#2DD4A7">.NET</tspan></text>
</svg>
```

Create `brand/afk4-logo-vertical-light.svg` (same, light cells + `fill="#0B1F18"` / `.NET` `#0B9E74`):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 96" width="120" height="96" role="img" aria-label="AFK4.NET">
  <g transform="translate(34 6)">
    <rect x="3"  y="3"  width="13" height="13" rx="3.5" fill="#0B9E74"/>
    <rect x="20" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="37" y="3"  width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="3"  y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="20" y="20" width="13" height="13" rx="3.5" fill="#0B9E74"/>
    <rect x="37" y="20" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="3"  y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="20" y="37" width="13" height="13" rx="3.5" fill="#D9E6E1"/>
    <rect x="37" y="37" width="13" height="13" rx="3.5" fill="#0B9E74"/>
  </g>
  <text x="60" y="86" text-anchor="middle" font-family="Inter, 'Segoe UI', system-ui, sans-serif" font-size="22" font-weight="800" letter-spacing="-1" fill="#0B1F18">AFK4<tspan fill="#0B9E74">.NET</tspan></text>
</svg>
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `bun test brand/afk4-brand.test.ts`
Expected: PASS (all tests).

- [ ] **Step 8: Commit**

```bash
git add brand/afk4-icon.svg brand/afk4-icon-maskable.svg brand/afk4-logo-horizontal.svg brand/afk4-logo-horizontal-light.svg brand/afk4-logo-vertical.svg brand/afk4-logo-vertical-light.svg brand/afk4-brand.test.ts
git commit -m "feat(brand): add AFK4.NET icon, maskable, and lockup SVGs"
```

---

## Task 3: Asset generation pipeline (PNG sizes + favicon ICO)

**Files:**
- Modify: `package.json` (root)
- Create: `scripts/build-brand-assets.mjs`
- Modify: `brand/afk4-brand.test.ts`
- Create (generated): `brand/dist/icon-{16,32,48,64,128,256}.png`, `brand/dist/pwa-192.png`, `brand/dist/pwa-512.png`, `brand/dist/pwa-maskable-512.png`, `brand/dist/afk4.ico`

- [ ] **Step 1: Add the dev dependencies and script entry**

Run:

```bash
bun add -d @resvg/resvg-js png-to-ico
```

Then add to root `package.json` `"scripts"` (keep existing entries):

```json
"build:brand": "bun scripts/build-brand-assets.mjs"
```

- [ ] **Step 2: Write the failing test for generated outputs**

Append to `brand/afk4-brand.test.ts`:

```ts
function pngSize(rel: string): { w: number; h: number } {
  const buf = readFileSync(join(BRAND, rel));
  // PNG IHDR: width = bytes 16..20, height = 20..24, big-endian
  return { w: buf.readUInt32BE(16), h: buf.readUInt32BE(20) };
}

test("generated PWA pngs exist at correct sizes", () => {
  expect(existsSync(join(BRAND, "dist/pwa-192.png"))).toBe(true);
  expect(pngSize("dist/pwa-192.png")).toEqual({ w: 192, h: 192 });
  expect(pngSize("dist/pwa-512.png")).toEqual({ w: 512, h: 512 });
  expect(pngSize("dist/pwa-maskable-512.png")).toEqual({ w: 512, h: 512 });
});

test("favicon ico is generated and non-empty", () => {
  expect(existsSync(join(BRAND, "dist/afk4.ico"))).toBe(true);
  expect(readFileSync(join(BRAND, "dist/afk4.ico")).length).toBeGreaterThan(0);
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `bun test brand/afk4-brand.test.ts`
Expected: FAIL — `dist/pwa-192.png` does not exist yet.

- [ ] **Step 4: Write the generation script**

Create `scripts/build-brand-assets.mjs`:

```js
import { Resvg } from "@resvg/resvg-js";
import pngToIco from "png-to-ico";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const BRAND = join(ROOT, "brand");
const DIST = join(BRAND, "dist");
mkdirSync(DIST, { recursive: true });

function renderPng(svgRelPath, size) {
  const svg = readFileSync(join(BRAND, svgRelPath), "utf8");
  const resvg = new Resvg(svg, { fitTo: { mode: "width", value: size } });
  return resvg.render().asPng();
}

// App/favicon/tray icon sizes (from afk4-icon.svg)
const ICON_SIZES = [16, 32, 48, 64, 128, 256];
for (const size of ICON_SIZES) {
  writeFileSync(join(DIST, `icon-${size}.png`), renderPng("afk4-icon.svg", size));
}

// PWA icons
writeFileSync(join(DIST, "pwa-192.png"), renderPng("afk4-icon.svg", 192));
writeFileSync(join(DIST, "pwa-512.png"), renderPng("afk4-icon.svg", 512));
writeFileSync(join(DIST, "pwa-maskable-512.png"), renderPng("afk4-icon-maskable.svg", 512));

// Multi-resolution favicon .ico (16/32/48) for browsers + WPF/MSI consumption
const ico = await pngToIco([
  join(DIST, "icon-16.png"),
  join(DIST, "icon-32.png"),
  join(DIST, "icon-48.png"),
]);
writeFileSync(join(DIST, "afk4.ico"), ico);

console.log("brand assets written to brand/dist/");
```

- [ ] **Step 5: Run the generator**

Run: `bun run build:brand`
Expected: prints `brand assets written to brand/dist/` and creates the PNG/ICO files.

- [ ] **Step 6: Run tests to verify they pass**

Run: `bun test brand/afk4-brand.test.ts`
Expected: PASS (PWA sizes correct, ico non-empty).

- [ ] **Step 7: Commit**

```bash
git add package.json bun.lock scripts/build-brand-assets.mjs brand/dist brand/afk4-brand.test.ts
git commit -m "feat(brand): add brand asset generation pipeline (png + ico)"
```

---

## Task 4: Replace placeholder brand across the three web frontends

**Files:**
- Modify: `src/AFK4.Customer.Web/public/favicon.svg` (replace lime placeholder)
- Create: `src/AFK4.Customer.Web/public/pwa-192.png`, `pwa-512.png`, `pwa-maskable-512.png`
- Create: `src/AFK4.Platform.Web/public/favicon.svg`
- Create: `src/AFK4.Operator.App.Web/public/favicon.svg`
- Modify: `src/AFK4.Platform.Web/index.html`, `src/AFK4.Operator.App.Web/index.html`
- Modify: `brand/afk4-brand.test.ts`

- [ ] **Step 1: Add the failing guard test**

Append to `brand/afk4-brand.test.ts`:

```ts
import { join as pjoin } from "node:path";
const REPO = pjoin(BRAND, "..");

const WEB_FAVICONS = [
  "src/AFK4.Customer.Web/public/favicon.svg",
  "src/AFK4.Platform.Web/public/favicon.svg",
  "src/AFK4.Operator.App.Web/public/favicon.svg",
];

test("every web app ships the new emerald favicon, not the lime placeholder", () => {
  for (const rel of WEB_FAVICONS) {
    const p = pjoin(REPO, rel);
    expect(existsSync(p)).toBe(true);
    const svg = readFileSync(p, "utf8").toLowerCase();
    expect(svg).not.toContain("#c8ff00"); // no lime placeholder
    expect(svg).toContain("#2dd4a7");      // new accent present
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `bun test brand/afk4-brand.test.ts`
Expected: FAIL — Platform/Operator favicons missing; Customer favicon still contains `#c8ff00`.

- [ ] **Step 3: Place the favicon in all three public dirs**

The favicon is the app-icon mark. Copy `brand/afk4-icon.svg` into each public dir as `favicon.svg`:

```bash
cp brand/afk4-icon.svg src/AFK4.Customer.Web/public/favicon.svg
mkdir -p src/AFK4.Platform.Web/public src/AFK4.Operator.App.Web/public
cp brand/afk4-icon.svg src/AFK4.Platform.Web/public/favicon.svg
cp brand/afk4-icon.svg src/AFK4.Operator.App.Web/public/favicon.svg
```

(On PowerShell use `Copy-Item brand/afk4-icon.svg src/AFK4.Customer.Web/public/favicon.svg` etc.)

- [ ] **Step 4: Copy regenerated PWA pngs into Customer.Web**

```bash
cp brand/dist/pwa-192.png src/AFK4.Customer.Web/public/pwa-192.png
cp brand/dist/pwa-512.png src/AFK4.Customer.Web/public/pwa-512.png
cp brand/dist/pwa-maskable-512.png src/AFK4.Customer.Web/public/pwa-maskable-512.png
```

- [ ] **Step 5: Wire the favicon link in Platform.Web and Operator.App.Web**

In `src/AFK4.Platform.Web/index.html`, add inside `<head>` (just before `<title>`):

```html
    <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
```

In `src/AFK4.Operator.App.Web/index.html`, add the same line inside `<head>` before `<title>`.

(`Customer.Web/index.html` already has this link — no change needed.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `bun test brand/afk4-brand.test.ts`
Expected: PASS — all three favicons present, emerald, no lime.

- [ ] **Step 7: Verify the frontends still build**

Run: `cd src/AFK4.Customer.Web && bun run build` (repeat for `AFK4.Platform.Web`, `AFK4.Operator.App.Web`).
Expected: each build succeeds; the PWA manifest in Customer.Web resolves `favicon.svg` and the PWA pngs.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Customer.Web/public src/AFK4.Platform.Web/public src/AFK4.Platform.Web/index.html src/AFK4.Operator.App.Web/public src/AFK4.Operator.App.Web/index.html brand/afk4-brand.test.ts
git commit -m "feat(brand): ship AFK4.NET emerald favicon/PWA icons across web frontends"
```

---

## Task 5: Brand asset README

**Files:**
- Create: `brand/README.md`

- [ ] **Step 1: Write the README**

Create `brand/README.md`:

```markdown
# AFK4.NET brand assets

Canonical logo assets for AFK4.NET. Master files are hand-authored SVGs; raster
outputs are generated. See the brand foundation spec:
`docs/superpowers/specs/2026-06-04-afk4-brand-positioning-design.md`.

## Files

| File | Use |
|------|-----|
| `afk4-mark.svg` / `-light.svg` | command-grid mark only (dark / light surfaces) |
| `afk4-icon.svg` | mark on rounded dark tile — favicon / app / tray |
| `afk4-icon-maskable.svg` | PWA maskable (safe-zone padded) |
| `afk4-logo-horizontal.svg` / `-light.svg` | primary lockup (mark + wordmark) |
| `afk4-logo-vertical.svg` / `-light.svg` | stacked lockup |
| `dist/*` | generated png sizes + `afk4.ico` |

## Tokens

- Accent: `#2DD4A7` (dark surfaces) / `#0B9E74` (light surfaces)
- Inactive cell: `#173028` (dark) / `#D9E6E1` (light)
- Icon background: `#0E2019`
- Wordmark text: `#E2F1EC` (dark) / `#0B1F18` (light)
- Active cells: diagonal — top-left, center, bottom-right
- Wordmark is **always uppercase** `AFK4.NET`; `.NET` in the accent color.

## Regenerate raster assets

```bash
bun run build:brand
```

Outputs go to `dist/`. Re-copy into web `public/` dirs if the mark changes.

## Verify

```bash
bun test brand/afk4-brand.test.ts
```
```

- [ ] **Step 2: Commit**

```bash
git add brand/README.md
git commit -m "docs(brand): document AFK4.NET brand assets and regeneration"
```

---

## Self-Review

- **Spec coverage:** mark (Task 1), icon/maskable/lockups (Task 2), favicon + ico + PWA png "для трея и MSI" generation (Task 3), placeholder replacement across frontends (Task 4), usage docs (Task 5). Tokens (`#2DD4A7`/`#0B9E74`/etc.), uppercase wordmark, diagonal active cells, dark+light, 16–24px legibility (mark-only rasterization) — all covered. Deferred items (WPF/NotifyIcon/MSI wiring, code design tokens, landing, EN copy) are explicitly listed out of scope for follow-up plans.
- **Placeholder scan:** every SVG and script step contains full literal content; no TBD/TODO; test code is concrete.
- **Type/name consistency:** `renderPng(svgRelPath, size)` defined once and reused; `read()` / `pngSize()` / `BRAND` / `DIST` names consistent across test and script; file paths identical between create-steps, copy-steps, and commit-steps.

## Notes for the executor

- Toolchain: `@resvg/resvg-js` ships a prebuilt win32-x64 binary, so `bun add -d` needs no native build step. `png-to-ico` is pure JS.
- Rasterized outputs are **mark-only** (no `<text>`), so generation never needs a font installed. Lockups keep `<text>` with a font stack; convert to outlined paths only if a later print/landing task needs pixel-exact rendering without Inter present.
- This plan assumes a feature branch (e.g., continue on `docs/brand-positioning` or a fresh `feat/brand-logo-assets`).
```
