---
name: afk4-branding-positioning-backlog
description: "Brand copy-sweep — product brand is 'AFK4.NET' (UPPERCASE, '.NET' as accent). Hard-coded 'AFK4' → 'AFK4.NET' sweep mostly DONE on branch brand/afk4net-copy-cleanup (2026-06-05)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 38b1e868-4b13-4184-9ee9-8d5b4231daea
---

Product brand is **"AFK4.NET"** — **UPPERCASE**, canonical per `brand/README.md` ("Wordmark is always uppercase AFK4.NET; `.NET` in the accent color"). Bare "AFK4" was unclear to club owners. Graphic brand (favicon/icons) is centralized in `brand/` via `scripts/build-brand-assets.mjs`; **text brand is NOT centralized** — strings live inline per app (3 independent web apps + WPF + installers). One source of truth for text would be overkill (brand name is stable) — sweep was done by point-replacing visible literals.

**Done in main before this branch (PR #53 copy-sweep `4cfb0b1` etc.):** i18n catalog brand-voice alignment, platform/customer/operator-web titles+roles+aria via i18n, **notification email templates** (`Notifications/Templates/{ru,en,tg}/*.json` — no bare "AFK4" left, already brand-clean).

**Done on branch `brand/afk4net-copy-cleanup` (2026-06-05):** visible "AFK4" → "AFK4.NET" across
- web: customer/operator/platform `index.html` titles, customer vite manifest, `useBranding` brandName fallback (+test), operator receipt header/footers/auth copy, platform Control Plane + `document.title`.
- WPF: window titles + logo TextBlocks (Operator MainWindow + Player.Shell), SetupWizard/GamingPc.Setup chrome.
- installers `*/Package.wxs`: ProductName, Manufacturer, DisplayName, Feature title, descriptions, RunOnce + shortcut names.
- tests updated. Web suites 613 pass + i18n voice + installer-string test (`UpdateHelperScriptTests`) 20 pass. WPF tests only build/run on Windows (no `Microsoft.WindowsDesktop.App` runtime in WSL).

**Deliberately NOT touched (technical identifiers, NOT brand copy):** Windows service ID `AFK4.Agent.Service` (namespace-style, ServiceInstall/Control Name + `options.ServiceName` + PS scripts), install paths `Directory Name="AFK4"`, registry keys `Software\AFK4\...`, `*.exe` artifact names, GUIDs/UpgradeCode, WiX `Id=`, env-vars, `AFK4.sln`, C# namespaces. Changing these breaks service/upgrade for no visible gain.

**Logo now in UI chrome (not just favicon), same branch:** brand SVGs live in `brand/` (`afk4-logo-horizontal.svg` = mark + wordmark, `.NET` in accent green; `afk4-icon.svg` = mark on tile, identical to each app's `public/favicon.svg`). Wired into headers:
- web Operator (3 brand-blocks) + Platform sidebar use `<img>` of `afk4-logo-horizontal.svg` / `favicon.svg` (copied into each `public/`); Customer left alone (shows tenant white-label brand).
- WPF (Operator MainWindow, Player.Shell, GamingPc.Setup, SetupWizard): logo rebuilt as **native XAML vector** — `DrawingImage x:Key="BrandMark"` in each project's `App.xaml` (9 RectangleGeometry, diagonal cells accent) + wordmark `TextBlock` with two `Run`s (`AFK4` + `.NET` accent). Dark surfaces use accent `#2DD4A7`/inactive `#173028`/text `#E2F1EC`; light (SetupWizard, GamingPc.Setup) use `#0B9E74`/`#D9E6E1`/`#0B1F18`. WebViewOperatorWindow's `StatusTitle` left as text (it's a dynamic status label, not a logo). All 4 WPF projects build clean with `dotnet build -p:EnableWindowsTargeting=true` (XAML→BAML validated; runtime render still needs Windows).

**Internal devops tail — WON'T DO (user decision 2026-06-09: "забей на внутренние инструменты"):** bare "AFK4" in `scripts/*.ps1`/`*.sh`/`*.py` + `installers/README.md` stays as-is. Branding epic is CLOSED.

See [[afk4-sp4-shipped]].
