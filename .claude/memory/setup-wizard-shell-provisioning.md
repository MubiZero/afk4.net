# Setup Wizard provisions BOTH apps (Operator App + Player Shell) — DONE

**Status (updated 2026-06-12): the colossal provisioning work is DONE & verified.** The wizard now
correctly installs the role's app on enroll and the agent brings it up WITHOUT reboot:
- `gaming_pc` → bundled **Player Shell** (`msiexec /i … /qn`).
- `manager_workstation` → bundled **Operator App**.
Finish screen shows install status + retry (`wizard:provisionShell`; web `FinishedScreen.tsx`
`ShellStatusRow`, i18n `setup.wizard.finished.shell.*`). Merged to main; clean-VM end-to-end was
the last gate and is now passed (earlier "stale May-17 agent on VM clones" trap resolved by
installing the fresh agent MSI).

Spec/plan: `docs/superpowers/{specs,plans}/2026-06-10-setup-wizard-shell-provisioning*`.

## THE hard-won root-cause lesson (keep — non-obvious, cost real debugging)
Fresh-VM symptom: agent service wouldn't start / app never came up. REAL cause was NOT the
GUI-launch-from-service path — it was that **the wizard wrote the device credential to MACHINE ENV
VARS, then started the service. A service launched by the SCM inherits an env block cached at BOOT,
so it never saw the freshly-written credential → 401 → unhandled in the BackgroundService →
`StopHost` tore down the whole agent. A reboot "fixed" it (SCM re-reads env at boot).**

Fix (commits `9e28449`, `d9ac51e`): wizard ALSO writes Agent bootstrap config to a FILE
`%ProgramData%\AFK4\Agent\bootstrap.json` (`FileBootstrapWriter`, ACL→SYSTEM+Administrators;
`AgentBootstrapValues` is the shared source of truth; `CompositeBootstrapWriter` = file+env). Agent
reads the file fresh every start (Program.cs: bytes → `AddJsonStream`, after env so it wins,
try/catch so a read error never crashes the host) → **enrollment takes effect immediately, no
reboot.** Also `BackgroundServiceExceptionBehavior = Ignore` (one worker fault can't kill the kiosk
agent) and a file logger → `%ProgramData%\AFK4\logs\agent.log`.

## Installer shape: shared-runtime Burn bundle (MERGED PR #74) — CURRENT
Сейчас инсталлятор = **WiX Burn bundle `afk4-client-<ver>-<channel>.exe`** (`installers/bundle/`),
который несёт ОБЩИЙ .NET 10 Desktop Runtime embedded (carry, НЕ download), компоненты publish
framework-dependent. Брендирован, zero-click, авто-запускает Setup Wizard. Влит в main PR #74
(`e6008fc`). Историческая ловушка: ранний вариант с **download** рантайма падал в sc-1053 на чистой
VM (FD-сервис не находил рантайм) → откатывали в self-contained (`20a7a31`); победил **carry**.
Полностью см. [[productionize-installer-epic]].

## Build & test facts
- Build packages: `powershell scripts/build-client-packages.ps1 -Version <v> -Channel internal -BunPath "C:\Users\mubin\.bun\bin\bun"` → self-contained MSIs in `artifacts/client-packages/` (gitignored).
- dotnet `C:\Program Files\dotnet\dotnet.exe`; WiX 7; bun `~/.bun/bin/bun`.
- Tests: `dotnet test tests/AFK4.SetupWizard.Tests`. Web: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test`. i18n guard: `cd packages/i18n && ~/.bun/bin/bun test`.
- **Staging creds** (`docs/operations/e2e-staging-dcgate-runbook.md`): API `https://afk4.staging.mubi.dev`; owner `e2eowner` / `E2eOwner!2026`; org `0169044b-2f74-46a7-8e52-7656a39a8f8c`; player `+992900000001` / PIN `112233`.

## Key code map
- Provisioning core: `src/AFK4.SetupWizard.Core/{ShellProvisioning,MsiexecPlayerShellProvisioner,SetupWizardPayloadResolver,SystemProcessRunner,AgentBootstrapValues,FileBootstrapWriter,CompositeBootstrapWriter,EnvironmentBootstrapWriter}.cs`
- Host bridge: `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs`; `src/AFK4.SetupWizard/App.xaml.cs`
- Agent: `src/AFK4.Agent.Service/Program.cs` (bootstrap file + HostOptions + file logger), `Logging/FileLoggerProvider.cs`, shell launch `Shell/PlayerShellProcessSupervisor.cs` (WTSGetActiveConsoleSessionId + CreateProcessAsUser into session 1), driven each heartbeat from `Worker.cs`.

## Wizard web UI — активно эволюционирует, ТЕКУЩАЯ ПРАВДА В КОДЕ
Дизайн мастера переделывался много раз; последний крупный проход — **PR #77** (in review, 2026-06-13):
phone-first с зашитым `+992`-префиксом и маской, тихая ссылка «вход по логину или почте», всегда
видимая «забыли пароль?», номер шага у заголовка через разделитель, направленные переходы
(вперёд/назад), пружинный press, каркас длинных списков (фикс. шапка+кнопки, скролл середины),
тогглы языка(RU/EN/TJ)/темы одной кнопкой, **нативные кнопки окна** (во всю высоту, заподлицо,
тонкие глифы, restore-иконка по `WindowState` из хоста, защита Close при установке). Финиш
локализует update-канал; занятые места — точка online(amber)/offline(grey). Не трактовать детали
как вечные — смотреть `PhoneLoginScreen.tsx`/`App.tsx`/`styles.css`. См. [[wizard-signin-redesign]],
[[copy-voice-terminology]], [[tg-i18n-honesty]].
