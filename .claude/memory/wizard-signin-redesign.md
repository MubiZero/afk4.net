---
name: wizard-signin-redesign
description: "Setup Wizard sign-in phone-first redesign — MERGED PR #64 (97c6011). SUPERSEDED since by PR #75 (unified login field) + PR #77 polish. Kept only for the deferred owner-code-cleanup debt."
metadata: 
  node_type: memory
  type: project
  originSessionId: 7f9fa301-0ccd-443e-b70e-46a02bb23dca
---

Redesign of the Setup Wizard sign-in + forgot-password screens, part of the
"допилить инсталлер" roadmap stage (stage 1 of 1)инсталлер→2)оператор→3)вебы→4)платформ-админ).

DONE & **MERGED to main** via PR #64 (commit `97c6011` on main, 2026-06-08).
Branch deleted. Was a separate topic from PR #63 (Tajik translations, see
[[tg-i18n-honesty]]) which merged just before it.

> ⚠️ **SUPERSEDED — не считать описание ниже текущим состоянием.** Экран входа
> с тех пор переделан дважды: **PR #75** свёл вход в один phone/login/email field
> (`feature/setup-wizard-unified-login`, merged), а **PR #77** (in review, 2026-06-13,
> ветка `feature/setup-wizard-design-polish`) — phone-first с `+992`-префиксом,
> тогглами языка/темы, нативными кнопками окна, **И отдельным проходом по шагу 4**:
> экран «Игровое место» сведён к **create-only** (одно поле имени → визард создаёт
> место в зоне по умолчанию + enroll одним действием; список/поиск/тумблер/инлайн-
> создание убраны — расстановка по залам целиком в Operator App, он это уже умеет).
> Зона по умолчанию переименована `Main Hall`→«Общий зал» (сид в
> `EfPlatformTenantService.cs` + preview-фейк). Финальный экран: «Место»→«Зал»
> (только зал, без имени ПК), убрана плашка «оболочка установлена» (оставлено
> только error+retry), кнопка «Завершить» на всю ширину. Спека/план:
> `docs/superpowers/{specs,plans}/2026-06-13-setup-wizard-step4-slim-seat-create*`.
> **Текущая правда — в коде** (`DeviceScreen.tsx`, `FinishedScreen.tsx`,
> `PhoneLoginScreen.tsx`, `App.tsx`), не здесь. Файл держим ради «deferred debt» ниже.

> 📌 **Device approval (подтверждение компа в админке):** по умолчанию НЕ требуется —
> `BranchEntity.RequireManualDeviceApproval` дефолт `false` (`PlatformDbContext.cs`),
> при enroll устройство сразу `approved` (`EfInstallService.cs`). Это **опция** филиала,
> не обязательный шаг. Approve/reject endpoints + DeviceDrawer-плашка живут как opt-in.
> (2026-06-13 решили НЕ вырезать фичу, только починили устаревший preview-фейк, что
> хардкодил `pending`.)

What landed (PR #64 — историческое, перекрыто):
- Sign-in is phone-first by convention (clean phone+password default, email as a
  secondary link, no segmented toggle). **Owner-code login removed from the wizard
  entirely** — screen/state/API/stepper deleted; error copy no longer mentions it.
- "Forgot password?" shows only after a failed sign-in (sticky), next to the
  password field; redundant field hints dropped; step description moved into the
  eyebrow (`ШАГ 1 · …`); lighter field labels (weight 800→600); roomier spacing,
  full-width centered fields, button matches field width; phone placeholder
  `+992 93 738 00-70`.
- Forgot screen: inline spinners, success state, channel switch as a link, hands
  identity back to sign-in.
- Gates: i18n 34/34, wizard bun 10/10, tsc + vite build clean. (Root `bun test`
  shows unrelated happy-dom/localStorage failures in Customer/Operator web — env
  artefact of running all frontends from repo root, not a regression.)

**Deferred debt — RESOLVED 2026-06-13:** owner-code **полностью выпилен из всего проекта**
(эпик `chore/remove-owner-code`, FF-merged в `feature/setup-wizard-design-polish`).
Снесено: backend `Identity/OwnerCodes/*` + `OwnerCodeEntity` + `OwnerCodeEndpoints` +
unauth install-эндпоинты `/api/install/{discover,enroll,seats}` + owner-code-ветки
`EfInstallService` + `InstallOperationResult.OwnerCodeId`; контракты `Install{Discover,
Enroll,CreateSeat}Request` + owner-code DTO; permission `ManageOwnerCode` + audit-имена;
platform-admin `OwnerCodePanel`/client/`useOwnerCode`/`install.ownerCode.*` i18n;
wizard host-bridge owner-code хендлеры; **БД-миграция `DropOwnerCodes`** (drop таблицы
`owner_codes` + колонки `devices.EnrolledViaOwnerCodeId`). Единственный enroll-путь
теперь — authed (логин сотрудника), визард его уже использует. Гейты: Platform.Api
1154/1154, SetupWizard 31, Shared.Contracts 119, platform-web 381, i18n 34, сборки 0.
Спека/план: `docs/superpowers/{specs,plans}/2026-06-13-remove-owner-code*`.

Preview/run: see [[setup-wizard-preview-launch]].
