---
name: productionize-installer-epic
description: "Productionize client installer epic — CLOSED: B (prod URL) + A (shared runtime) shipped to main; C (signing) dropped"
metadata: 
  node_type: memory
  type: project
  originSessionId: 636069a5-d5ae-4bad-b419-86fa60370f51
---

Epic «Productionize the client installer» — по сути ЗАКРЫТ (design: `docs/superpowers/specs/2026-06-11-productionize-client-installer-design.md`).

- **B — prod URL by channel: DONE & in main.** `build-client-packages.ps1` маппит channel→URL (`internal`/`beta`→staging, `stable`→`https://app.afk4.net`) через `-p:AFK4PlatformBaseUrl`. Тест `BuildClientPackagesScript_MapsChannelToPlatformBaseUrl`.
- **A — shared runtime: DONE & MERGED (PR #74 `feature/installer-shared-runtime` → main `e6008fc`).** Инсталлятор теперь несёт ОБЩИЙ рантайм: 4 компонента публикуются framework-dependent (`63a175b`) + один WiX Burn bundle `afk4-client-<ver>-<channel>.exe` (`installers/bundle/Bundle.wxs`, `85565eb`/`7656cd4`), который **carries** (не скачивает) .NET 10 Desktop Runtime. Бандл брендирован и zero-click, сам авто-запускает Setup Wizard после установки агент-MSI (`c42d1e2`, `cec0bf4`). Это убрало дублирование рантайма в каждом MSI (было ~160MB self-contained на компонент).
- **C — code signing: DROPPED (user, 2026-06-13).** Сертификат/подпись из планов убраны. Не предлагать снова.

## THE hard-won lesson (durable — стоило реального дебага)
FD+Burn **сначала попробовали с DOWNLOAD рантайма и откатили** (`20a7a31`, 2026-06-10): на чистой VM framework-dependent агент-сервис падал в sc-1053 (рантайм не находился — apphost не видел Microsoft.NETCore.App). **Фикс = carry, не download** (бандл несёт рантайм embedded: Compressed+SourceFile, без DownloadUrl). Рантайм-пин: .NET Desktop Runtime 10.0.x; WiX 7, бандлу нужны Bal + Netfx extensions. См. [[setup-wizard-shell-provisioning]].
