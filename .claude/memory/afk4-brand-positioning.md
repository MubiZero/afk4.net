---
name: afk4-brand-positioning
description: "AFK4.NET brand foundation — locked positioning, naming, emerald palette, command-grid logo; spec + asset plan committed 2026-06-04"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9758e85d-c3bf-4ec3-a14b-0e42a3eec377
---

AFK4.NET brand & positioning foundation, agreed in brainstorm 2026-06-04. **Locked decisions — don't relitigate:**

- Brand name **AFK4.NET**, ALWAYS uppercase. No constructed name meaning (explicitly rejected the "AFK = Away From Keyboard" narrative). Industry term used: **киберклуб** (not "компьютерный клуб").
- Positioning territory **A**: core = trust/accounting (immutable ledger + audit); support = speed at the desk + cloud for multi-branch networks; openness/AGPL = trust signal, not headline. ICP = club/network owner & manager.
- **No slogan/tagline** (deliberate). Canonical descriptor: **«AFK4.NET — Система управления киберклубами»** (plural) / "Cyber club management system". README + PRD aligned to AFK4.NET name and "cyber club" (was "computer club").
- Tone: operator-grade + gaming accent (serious, concrete, no hype, no emoji).
- Palette **Emerald/Mint**: accent `#2DD4A7` (dark) / `#0B9E74` (light); icon bg `#0E2019`; inactive cell `#173028` (dark) / `#D9E6E1` (light). Logo = **C3 "command grid"** 3×3 mark (active cells on the diagonal: top-left, center, bottom-right) + uppercase wordmark, `.NET` in accent.

Full spec: `docs/superpowers/specs/2026-06-04-afk4-brand-positioning-design.md`
Asset plan: `docs/superpowers/plans/2026-06-04-afk4-brand-logo-assets.md` — **EXECUTED 2026-06-04** (5 commits `cf29c6e`→`b978fc8`, merged to `main` via PR #53, merge `6661d5d`). Built: master SVGs in `brand/` (mark/icon/maskable/horizontal+vertical lockups, dark+light), `bun:test` suite `brand/afk4-brand.test.ts` (10 pass), generation pipeline `scripts/build-brand-assets.mjs` (`bun run build:brand`, deps `@resvg/resvg-js`+`png-to-ico`) → committed `brand/dist/` (png 16–256 + pwa 192/512/maskable + `afk4.ico`), and emerald `favicon.svg` shipped across all 3 web frontends (lime `#c8ff00` gone). Deviation from plan: did NOT copy raster pwa-pngs into Customer.Web/public nor edit its PWA manifest — that manifest already points at the SVG favicon (`purpose: 'any maskable'`), so the SVG swap updates the installed-PWA icon; raster set stays in `brand/dist/` as library. Final review = ready-to-merge, no critical/important issues. Deferred to follow-ups: WPF/NotifyIcon/MSI icon wiring, color/type design tokens in app code (incl. stale PWA `theme_color #101314`→brand `#07120F` in `Customer.Web/vite.config.ts`), marketing landing, EN copy.

Process note: user pushed back hard on over-asking trivia (e.g. pronunciation) and on an off-brand jokey slogan. Keep B2B-serious, take reasonable defaults, don't over-question. See [[ux-audit-roadmap]] [[platform-web-redesign]].
