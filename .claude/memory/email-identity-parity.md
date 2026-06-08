# Epic: email-identity-parity (M3 merged + "email co-equal channel" follow-on)

**Status: M3 COMPLETE (merged, PR #58). Follow-on epic BUILT + fully verified, committed on `feature/email-coequal-channel`** (2026-06-08). Product owner asked for two changes that replaced the original "M4 terminal-email" plan: (1) email reset must send the **same short 6-digit code as SMS**, entered inline (not a browser link); (2) the Setup Wizard must allow **login by email** too, with full multi-org club-picker (so email = a truly co-equal channel, no "use phone instead" dead-end). Done across backend + all 3 frontends.

## What shipped (5 stages, all green)
- **Stage 1 — backend email = 6-digit OTP** (Platform.Api): `EfStaffPasswordResetService` rewritten — generates a 6-digit code via the SMS `RandomPhoneOtpGenerator`, stored hashed in the **reused** `PasswordResetTokenEntity` (added `AttemptCount` column → migration `AddPasswordResetAttemptCount`), 15-min lifetime, reuses `PhoneOtpOptions` for max-attempts/cooldown/hourly-cap. `IStaffPasswordResetService.CompleteResetAsync(token,pw)` → `ResetAsync(userNameOrEmail, code, pw)` returning `ResetPasswordByEmailResult{Status,RemainingAttempts}`. **Contract `StaffResetPasswordRequest` changed `{Token}` → `{UserNameOrEmail, Code}`** (breaking — drove stages 2-4). `/reset-password` endpoint mirrors the phone mapping (invalid_code+remainingAttempts / code_expired 410 / too_many_attempts 429) + `staff-reset` rate-limit on both forgot & reset. Tests rewritten (1090/1090).
- **Stage 2 — Platform.Web**: email flow made **inline** (request→verify→done, like phone, unified `step` model). `resetPasswordByToken` → `resetPasswordByEmail(userNameOrEmail,code,pw)`. **Deleted obsolete `ResetPassword.tsx` + `/auth/reset-password` route** (no emailed link anymore). Email login + club-picker already existed (`signInByLogin`/`signInToClub`/`StaffSignInChooseClubError`). 390/390 + tsc + build.
- **Stage 3 — Operator** (web + WPF host): email flow inline; deleted operator `ResetPassword.tsx` + `authView 'reset'`. `auth:resetByEmail` payload `{token}` → `{userNameOrEmail,code,newPassword}` through `authClient` → `OperatorWebHostBridge` → `HttpOperatorAuthApiClient` → `/reset-password`. 179/179 per-file + tsc + build; **host built + `OperatorWebHostBridgeTests` 11/11 ran on Windows**; full Operator.App.Tests 237/237 (2 dispatcher tests flake, pass on retry).
- **Stage 4 — SetupWizard** (the actual new work): email reset now **inline** in `ForgotPasswordScreen` (unified channel model, replaced the terminal/browser version + removed dead `setup.wizard.forgotPassword.email.sent` key); added `ResetPasswordByEmailAsync` to Core+host+fake. **Email login**: Core `SignInByLoginAsync`→`SetupWizardLoginResult{SignedIn,Clubs}` (handles 409) + `SignInToClubAsync`; host ops `wizard:signInByLogin`/`wizard:signInToClub` (store token; 409→clubs list); `PhoneLoginScreen` rewritten with a **phone/email mode toggle + club-picker view**. New honest ru/en/tg keys: `phoneLogin.mode.{phone,email}`, `phoneLogin.field.login`, `phoneLogin.chooseClub.{title,subtitle}`. wizard web 9/9 + tsc + build; Core 13/13 (+ club-picker/409 tests); **WPF host built on Windows**.
- **Stage 5**: i18n parity 32/32; Windows builds of both WPF hosts via `dotnet.exe` over UNC (`-p:ProduceReferenceAssembly=false`, retry 3-4× to beat the CS0006 UNC ref-dll race — it climbs the dep chain one project per attempt).

## Key facts for future work
- Email & SMS reset now share the 6-digit-code + attempt-counter model; **only difference is delivery + lifetime** (email 15 min, SMS 5 min). Email reset code is resolved by login/email (short code isn't self-identifying, unlike the old 72-char token).
- The old browser-link reset path is **gone** everywhere — `staff.password_reset` email template already said "enter this code" so no template change was needed (the code is just short now).
- bun `mock.module` is shared across files in one `bun test` run → every per-file mock of a module must export that module's **full** surface, else a sibling's partial mock makes an unrelated export "not found". The operator-web full-suite count (3 vs 75 fails) is meaningless ordering noise; **per-file run is the true signal (179/179)**.

# Epic: email-identity-parity (M3 — Operator i18n + email/SMS reset)

**Status: M3 COMPLETE — PR #58 open** (`feature/email-identity-parity` → `main`), HEAD `ee41166`.
Goal of epic: email is a co-equal alternative to phone for staff identity (login / register / reset). M1+M2 merged earlier (PR #57). M3 = Operator desktop app.

## What M3 delivered
- **ICU i18n engine** (`@afk4/i18n`): `t(key, values?)` via `intl-messageformat` (interpolation + per-locale plurals), backward-compatible. Added `createTranslator(locale)` for non-React modules / unit tests.
- **.NET host (Phase C, earlier session)**: 4 reset bridge ops + structured errors (`code`/`remainingAttempts`).
- **Operator web**: `<I18nProvider>`, channel-aware Forgot/Reset screens, login relabel + «Забыли пароль?», email login free from M1.
- **Full Operator localization (ru/en/tg, honest)**: all 9 workspace screens, App shell + nav + signals strip, the entire `operatorHelpers` label layer, and `floorMapState` / `checkoutState` / `actionOutbox`. `pluralRu` deleted.

## Translation policy
Real ru/en/tg everywhere (parity + voice guards). ru byte-exact to old literals (keeps ru-rendered tests green). tg = real Tajik (Cyrillic), not ru copies — except true loanwords (Платформа, оператор, ПК…).

## Deliberate scope cuts (product owner approved)
- `apiErrors.projectOperatorError` left raw: `.title` never rendered (dead), `.detail` is pass-through; 55+ call-site cascade not worth one generic fallback.
- `operatorData.seats` demo fixtures (~110) left raw: seed data shown only before backend loads; backend map path IS localized.
- `connectionResolver` default error left raw (consistent with apiErrors); `BackendPlayersWorkspace` API note 'Создано из карточки клиента' raw (pre-existing).

## Sentinels kept raw on purpose (compared, not displayed-raw): `'нет смены'`, `'Неактивен'`, billing tokens fed to `billingLabel`, English version tokens fed to `appVersionLabel`/`deviceStatusLabel`, `matchesLogSource` substring heuristics (`'касс'`/`'чек'`/`'оператор'`).

## Verification (this machine)
i18n 32/32 · Operator tsc clean + 181/181 + vite build ✓ · Platform.Web 392/392 + tsc ✓ · Customer.Web 66/67 (the 1 fail = pre-existing flaky `toast auto-dismiss` timer test, passes in isolation, unrelated to i18n).
**NOT run here (no .NET SDK on this machine):** `AFK4.Operator.App.Tests` (host) + `Platform.Api.Tests`. This session changed ZERO .cs files (only .tsx/.ts/.json), so they're unaffected — host was 237/237 when Phase C landed. **Confirm in CI.**

## Incidents caught in review & fixed (subagent-introduced)
1. **Fake-green**: a subagent made `t` optional in `operatorHelpers` with Russian fallbacks → ~50 UI call sites showed Russian under en/tg (tsc + ru-tests don't catch). Fixed: `t` made required, all call sites threaded.
2. **Mojibake** from PowerShell file writes: `—`→`вЂ"`, `…`→`вЂ¦`, `×`→`Г—`, tg `муваққатан`→latin `qq`, plus stray BOMs on 4 .tsx. All repaired via Edit/bun. **Lesson: subagents writing files via PowerShell Set-Content corrupt UTF-8 — have them use Edit/Write tools only.**

## Out-of-scope finding (future epic, owner = native speaker)
~79% of the PRE-EXISTING catalog has `tg === ru` (legacy fake-Tajik across ALL frontends). M3's new keys are honest; no new fake tg introduced.

## Environment note (this machine = C:\projects\afk4.net)
bun on PATH (`C:\Users\mubin\.bun\bin\bun`); no .NET SDK; the external superpowers memory graph + skill repos (D:\claude-working-style #35–38, D:\interface-limb) are NOT synced here — repo-tracked `.claude/memory` is the reliable cross-device channel.
