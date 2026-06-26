# Memory Index

## Поведение / процесс
- [Working style — bias to action](feedback_working_style.md) — внутри утверждённого плана выполнять задача-за-задачей без чек-инов; паузить только на блокерах/изменении объёма/деструктиве.
- [Auto-merge authorized](afk4-auto-merge-authorized.md) — мержить слайс-PR самому после зелёного CI (PR #109 и последующие); полный цикл слайса автономен.
- [«Открой превью» = дай ссылку](afk4-preview-means-give-link.md) — `bun run dev` → отдать URL (http://127.0.0.1:5174/), НЕ headless-скриншоты.
- [TodoWrite → Task tools](tooling-todowrite-is-task-tools.md) — TodoWrite нет; использовать TaskCreate/TaskUpdate/TaskList.

## Окружение / тулчейн
- [Env quirks](afk4-env-quirks.md) — bun полный путь, bun test+build гейты, rtk→/tmp/x.sh, dotnet ef на Linux traps, Coolify runbook, WPF-мост через D:\ clone, agent-test WSL baseline.
- [Frontends on bun test](frontends-on-bun-test.md) — все фронты на `bun test` (не vitest, happy-dom+jest-dom), `mock.module` течёт process-wide; build=`tsc -b && vite` И тайпчекает тест-файлы (зелёный `bun test` ≠ зелёная сборка → типизировать bun-моки; финал слайса обязан включать `bun run build`).
- [Coolify reference](coolify-reference.md) — staging cool.mubi.dev, API /api/v1 Bearer; токен в `~/.config/afk4/coolify.token`.
- [Memory in git](memory-in-git-setup.md) — память версионируется в гите через симлинк/junction на `<repo>/.claude/memory`; новое устройство = clone + пересоздать линк ДО работы (WSL и Windows).
- [Setup Wizard preview launch](setup-wizard-preview-launch.md) — `dotnet run --project AFK4.SetupWizard -- --preview` + Vite 5175 + env URL; devDeps → `bun install --force`.

## Архитектура (инварианты)
- [Operator app = WebView2+React](afk4-operator-app-webview2.md) — Operator.App = тонкий WPF-хост + React (`AFK4.Operator.App.Web`), Linux-buildable.
- [Customer shell pivot](afk4-customer-shell-pivot.md) — Player.Shell = WebView2+React, enforcement (lock/lease/kiosk) в Agent.Service; shell не-авторитетен; осталось G5 hardware-smoke + Phase 2 (vault/privacy-wipe).
- [Platform.Web redesign](platform-web-redesign.md) — money 100×: DTO minor units, `formatCurrency` ждёт MAJOR → `minorToMajor` на UI-границе; org-эндпоинты IDOR-guard через `StaffContext.OrganizationId`; feature-shape (`*Model.ts`+`use*`).
- [Operator theme & dev-mock](operator-theme-and-preview.md) — `bun run dev` = mock по умолчанию (`?live`=staging); тема в `operatorTheme.tsx` (default dark); акцент оператора **синий #1f6feb**, НЕ бренд-emerald.
- [Operator rail sections](operator-rail-sections.md) — рейл = 6 секций+табы (`navSections`); `--shell-tabstrip` в calc-высотах; dev-mock отдаёт `[]` → object-клиенты гардить `?? []`.
- [Monolith refactor blueprint](monolith-refactor.md) — раскладка `Endpoints/`/`App.tsx` (если разбивать ещё); `dotnet format --include` только относительный путь.
- [API client decomposition](afk4-api-client-decomposition.md) — монорепо сохранён; god-client → domain-sub-client фасад; WPF ViewModels off-limits.

## Бренд / копи / i18n
- [Brand & positioning](afk4-brand-positioning.md) — бренд AFK4.NET (CAPS, `.NET` акцент), «киберклуб», emerald #2DD4A7, command-grid лого — locked.
- [Branding backlog — что НЕ трогать](afk4-branding-positioning-backlog.md) — copy-sweep done; технические ID/пути/GUID/exe НЕ переименовывать (ломает сервис/upgrade).
- [Copy voice & terminology](copy-voice-terminology.md) — глоссарий + `voice.test.ts` guard; floor-map хранит EN-токены, локализуется на render через `*Label` (не t()-ить).
- [Tajik i18n honesty](tg-i18n-honesty.md) — guard-тест против `tg===ru` (whitelist loanwords); добавляешь tg-ключ → реально таджикский; переводы НЕ native-reviewed.

## Активный бэклог / эпики
- [Operator «Склад» эпик](afk4-operator-stock-epic.md) — вынос склада в отдельную секцию рейла; слайсы S0–S4; S0 (Остатки v2, avg-cost) PR #116, **S1 Приёмка PR #117**, **S2 Журнал PR #118** (CreatedByDisplayName сервер-резолв, период клиентом на окне ≤200, CSV, уборка истории из Товаров); дальше S3 Штрихи/S4 Инвентаризация; money price=nested DTO vs avgCost=плоское; `tsc -b` тайпчекает тесты; финал-гейт включать `packages/i18n` тесты.
- [Online booking auto-confirm + hold](afk4-online-booking-autoconfirm-epic.md) — авто-confirm онлайн-броней при балансе (Slice 1 в main); холд денег — бэклог, решения зафиксированы, гейт на мобилку. Канон-док `docs/superpowers/specs/2026-06-18-online-booking-autoconfirm-hold.md`.
- [Multi-tenant payments](afk4-multitenant-payments-state.md) — dcgate per-branch; money-path FROZEN внешним bank-bot; `Secrets:EncryptionKeyBase64` критичен (потеря = недешифруемые creds); prod afk4 не задеплоен.
- [Time handling audit](afk4-time-handling-audit.md) — деньги server-authoritative/безопасны; реальный риск = skew/implicit-tz; рискованный lease/grace rewrite отложен до drift-логов; tz-multiregion YAGNI.
- [SP4 backlog](afk4-sp4-shipped.md) — SP4 в main; deferred: Player OTP, per-tenant PWA icons, SignalR Redis backplane, G5 hardware-smoke.

## Закрытые эпики (durable-уроки)
- [Operator redesign — Этапы](operator-redesign-phase0-decisions.md) — Этапы 0/1/2/3/4 ЗАКРЫТЫ; **Касса (Этап 2) завершена** (S0 #109 + S1 #110 + S2 #111 + S3 #112 + полировка #114 в main); впереди 5 Отчёты / 6 Управление; PR #114: общий `PaymentDialog` (тело без обёртки, иначе ремаунт), amber=только warning (деньги белые), снят потолок 5 категорий, уборка сирот/CSS; вкладки Кассы = `sales`(POS+Заказы)/`shift`/`journal`; currency = const-мапа `@afk4/money` (TJS→«с.», целые; разделитель тысяч = NBSP, отрицат. ASCII-минус); App.test отдельным `bun test`-прогоном; флак-урок: дренировать вторую волну рефетчей после nonce-бампа; паттерн «воркспейс как сегмент» (`embedded` корень `<section>` вместо `<main>`) — канон для слияний.
- [Operator «Клиенты» (Этап 3)](afk4-operator-clients-epic.md) — **ЗАКРЫТ** (PR #103–#108 в main, merge `b5dce420`). Durable: долг=виртуальный debt-счёт из ledger, пакеты=предоплач.время; **money-path IsActive-guard в 2 слоя** (5 per-player эндпоинтов + `EfMoneyActionExecutor` для approval-очереди — иначе over-threshold редирект обходил); раздел на всю ширину 3 зоны + кросс-контекст профиля.
- [Operator «Карта»](afk4-operator-map-epic.md) — карта = только ГРИД (вид «План» удалён); бэк-техдолг: мёртвый PUT `/floor-map`; `floor.*`/Platform.Web = ДРУГОЙ (venue) редактор.
- [Competitor UX teardown](operator-competitor-ux-teardown.md) — паттерны SmartShell/Langame для будущих этапов (Касса/Клиенты/Отчёты); раздел «опровергнутые верификацией факты».
- [Email identity parity](email-identity-parity.md) — инвариант phone↔email; reset-экраны построены (`ForgotPassword*.tsx`); ICU i18n.
- [Phone staff registration](phone-staff-registration.md) — payom.tj SMS + OTP-инварианты; phone = global login-id; owner-code выпилен.
- [Wizard sign-in / owner-code removed](wizard-signin-redesign.md) — owner-code удалён из всего проекта (`DropOwnerCodes`); device-approval — опция филиала (по умолчанию off).
- [Operator+Wizard auth](operator-wizard-auth-phone-first.md) — sign-in резолв username→email→verified phone; forgot-password всегда 200 (анти-enumeration).
- [Shared color tokens](shared-color-tokens.md) — `@afk4/tokens` единый CSS; импорт перед `styles.css`; guard used-vars + WCAG.
- [dcgate topup fix + test speedup](dcgate-online-topup-shift-fix.md) — shared-host тест-изоляция 573s→~27s; online-topup кредитуется с `ShiftId=null`.
- [Productionize installer](afk4-productionize-installer-epic.md) — carry-not-download (download→sc-1053); WiX Burn bundle несёт .NET 10; C(signing) дропнут; prod `app.afk4.net`; Package Smoke только push:main.
- [Setup Wizard shell provisioning](setup-wizard-shell-provisioning.md) — root-cause: device-cred в MACHINE ENV → SCM-cache → 401 → краш; фикс = читать `bootstrap.json` свежим.
- [Client demo runbook](afk4-client-demo-runbook.md) — сборка MSI `pwsh7` (не PS5.1); upgrade-over-enrolled НЕ переоткрывает визард → тест на чистой VM.
