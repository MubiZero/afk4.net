---
name: setup-wizard-preview-launch
description: How to launch the AFK4 Setup Wizard (WPF+WebView2) in preview mode with the live React dev server
metadata: 
  node_type: memory
  type: project
  originSessionId: 15e86a91-8567-474d-9b69-4bed613f0ddb
---

AFK4.SetupWizard is a WPF + WebView2 desktop app (net10.0-windows). Preview = fake services, no elevation/network/disk/Agent — `#if DEBUG` only, launched via `--preview`.

**Launch recipe (live UI, HMR):**
1. Vite dev server: from `src/AFK4.SetupWizard.Web` run `bun run dev` → http://127.0.0.1:5175 (script: `vite --host 127.0.0.1 --port 5175`).
2. Point the shell at it: env `AFK4_SETUP_WIZARD_WEB_DEV_SERVER_URL=http://127.0.0.1:5175`.
3. `dotnet run --project src/AFK4.SetupWizard -c Debug -- --preview`.

Asset resolution (SetupWizardWebAssetResolver): dev-server env var → `src/AFK4.SetupWizard.Web/dist/index.html` (built) → `WebAssets/index.html` fallback. Without the env var and without a `dist` build it falls back to the static `WebAssets` snapshot, not the live source.

**Gotchas:**
- Frontend devDeps (vite, @vitejs/plugin-react) can be physically missing while `bun install` reports "no changes" (lock satisfied, files absent — looks like a prior `--production` install). Fix: `bun install --force`.
- `bun --cwd <dir> run dev` and `bun run --filter afk4-setup-wizard-web dev` both failed here; `cd` into the web dir then `bun run dev` works.
- Worth capturing as a project skill via `/run-skill-generator` (needed dep install + env var + two processes).

Related: [[platform-web-redesign]], [[frontends-on-bun-test]].
