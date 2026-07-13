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

**Из WSL с WPF-окном (не просто dev server) — уже покрыто скиллом:** `.claude/skills/operator-wpf-preview`
теперь обобщён на Оператора И Мастер установки (таблица портов/env-var/аргументов внутри).
Ключевые граблина: 1) НИКОГДА не запускать сборку обоих хостов параллельно — делят
`AFK4.BuildingBlocks`/`AFK4.Shared.Contracts`/`AFK4.Localization`, гонка валит оба и может
оставить `bin/obj` наполовину собранным (только `.exe`, без `.dll` → `dotnet run --no-build`
падает «application does not exist» даже после «успешной» сборки — лечится чисткой bin/obj);
2) залипший vite с прошлой сессии может занять чужой порт (5175 внезапно отдавал контент
Оператора) — не верить curl 200, проверять `<title>` и `readlink /proc/<PID>/cwd`.

Related: [[platform-web-redesign]], [[frontends-on-bun-test]], [[afk4-env-quirks]].
