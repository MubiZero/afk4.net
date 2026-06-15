---
name: monolith-refactor
description: "Two monoliths split into modules — DONE & merged to main (272c5ab, 2026-06-06). Program.cs 13303→425, Operator App.tsx 10469→1235. Kept for the split blueprint/patterns."
metadata: 
  node_type: memory
  type: project
  originSessionId: c43c3457-e97c-4b54-a8ba-a4b4343ee9c2
---

✅ **STATUS (2026-06-06): split DONE на ЛОКАЛЬНОМ main.** Оба монолита разобраны методом **port-forward** проверенной ветки `refactor/program-cs-endpoints`: `git checkout` модулей + урезанных shell + выверка крошечного drift. Коммиты `1bc94fb` (backend Program.cs 13303→425, 36 файлов `Endpoints/`) и `203110b` (frontend App.tsx 10469→1235, 15 модулей). Гейты: backend **1055/1055**, frontend tsc 0 + **173/173** + build 0; route-set идентичен, модули byte-identical с рефактор-веткой. **Запушено в origin/main 2026-06-06 (`272c5ab`)** вместе с Phase B-UI и Phase C. Старая ветка `refactor/program-cs-endpoints` уже УДАЛЕНА (нет ни local, ни origin). Drift, который доливали при порте: backend +115 (7-8 DI SMS/PhoneOtp в Program.cs + 4 `/api/auth/staff/*` эндпоинта в AuthEndpoints.cs); frontend +20/−8 (AccountPanel-обвязка + 3 brand-строки в shell App.tsx + 1 в operatorHelpers.ts `buildPosReceiptText`). План: `docs/superpowers/plans/2026-06-06-split-monoliths-port-forward.md`.

Блок ниже — проверенный чертёж раскладки (как это делали на рефактор-ветке 2026-06-05):

**Бэкенд (DONE, тесты 1001/1001 зелёные):** `src/AFK4.Platform.Api/Program.cs` сведён к **396 строкам** (только DI/middleware + 27 строк регистрации `app.MapXxxEndpoints()` + `app.Run()`). Всё вынесено в папку `Endpoints/`:
- **Хелперы** → `EndpointHelpers.*.cs` (9 файлов: Http, Audit, Devices, Loaders, Dtos, Validation, Reports, Constants) — один `internal static partial class EndpointHelpers`, подключён в Program.cs и доменных файлах через `using static AFK4.Platform.Api.Endpoints.EndpointHelpers;`. Тела эндпоинтов при этом не менялись.
- **Эндпоинты** (188 шт) → 27 доменных `XxxEndpoints.cs` (`internal static class` + `public static void MapXxxEndpoints(this WebApplication app)`). Program.cs нужен `using AFK4.Platform.Api.Endpoints;` чтобы видеть extension-методы.
- **records** (HealthResponse и пр.) → `EndpointContracts.cs`, оставлены в GLOBAL namespace (на `HealthResponse` ссылается `HealthEndpointTests`).
- Самый крупный домен — `DeviceEndpoints.cs` (~1600 строк, 24 эндпоинта); можно дробить дальше, но это уже модуль, не монолит.

**Метод выноса (надёжный):** byte-exact перенос блоков по точным строковым маркерам (route-строки `app.MapX("...")`), без переписывания логики → поведение гарантированно не меняется. Финальный причёс отступов: `dotnet format whitespace <csproj> --include 'src/AFK4.Platform.Api/Endpoints/' ` — **путь в `--include` ОБЯЗАТЕЛЬНО относительный от cwd**, абсолютный молча матчит 0 файлов.

**Известный мелкий долг на бэке:** в новых файлах лежит полный (избыточный) блок usings — безвредно (0 warnings, `.editorconfig` не включает IDE0005), при желании чистится отдельным проходом.

**Факты про сборку/тесты:** `.editorconfig` в корне = 4 пробела, CRLF, trim trailing, final newline. Program.cs = UTF-8 **без BOM**, CRLF. `Get-Content | Measure-Object -Line` врёт по числу строк — считать через `[System.IO.File]::ReadAllLines`. Тесты бэка: `dotnet test tests/AFK4.Platform.Api.Tests` ≈ **8–10 мин**, 1001 тест.

**App.tsx (DONE, 2026-06-05; tsc -b=0, bun test 170/170, bun run build=0):** `src/AFK4.Operator.App.Web/src/App.tsx` сведён **10 457 → 1223 строки** (осталась оболочка: `App`/`AppInner`-оркестратор со всем состоянием + `SignInScreen`/`BlockedTenantScreen`/`WindowControls`/`WindowResizeHandles`). Разбит на 16 файлов рядом с App.tsx:
- Сначала удалено **~1060 строк мёртвого кода** — 6 фикстурных воркспейсов (`BookingWorkspace`, `PosWorkspace`…), вытеснённых `Backend*`-версиями, нигде не рендерились.
- **Фундамент:** `operatorTypes.ts` (все type-алиасы), `operatorPermissions.ts` (permissionNames/workspacePermissionRules/hasPermission…), `operatorHelpers.ts` (~1340 строк — ВСЕ чистые хелперы/константы: форматтеры дат/денег, `readXxx`-парсеры, лейблы, `*Label`, и общие сателлиты типа fixturePlayers/isGuid/auditActionLabel/auditActorLabel), `operatorPrimitives.tsx` (FeedbackNotice, CriticalActionConfirmation, StateFlag).
- **Воркспейсы — по файлу на каждый:** `BackendPosWorkspace`, `BackendBookingWorkspace`, `BackendPlayersWorkspace`, `BackendPaymentsWorkspace`, `BackendLogsWorkspace`, `BackendSettingsWorkspace` (1836 строк — сам крупный, можно ещё дробить), `ReviewWorkspace`, `MapWorkspace`, `DashboardWorkspace`, `MapSidePanel` (+CheckoutDialog), `SummarySidePanel`.
- **Метод (как и на бэке):** снизу вверх (сначала типы→хелперы→примитивы, иначе цикл — App импортирует воркспейс, а воркспейсу нужен тип из App). Перенос byte-exact, поведение не менялось. Правило сателлитов: хелпер, нужный 2+ файлам → в `operatorHelpers.ts` (export), локальный → в файл воркспейса. Импорты финализировались через `tsc` (missing-name) + чистка `tsc --noEmit --noUnusedLocals` (убрано 89 осиротевших импортов). Юзер просил «без скриптов» — переносил Read+Edit'ами; крупные воркспейсы отдавал Sonnet-агентам пачками.

**Мелкий долг по фронту:** `operatorHelpers.ts` — grab-bag на 1340 строк, при желании дробится на labels/format/parse. Две pre-existing неиспользуемые локалки (не импорты): `BackendLogsWorkspace.tsx` `updateSummary`, `BackendPaymentsWorkspace.tsx` `cashOut`. Фронт-тесты — `bun test` (см. [[frontends-on-bun-test]]).
