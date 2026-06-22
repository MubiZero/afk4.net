---
name: afk4-operator-clients-epic
description: "Operator redesign Этап 3 «Клиенты» — активный эпик: решения дизайна, слайсы S0..S3+S1b, ветка, бэк-факты"
metadata: 
  node_type: memory
  type: project
  originSessionId: af6ceac8-6e34-4042-8419-c1df5efb0a7c
---

Этап 3 «Клиенты» программы редизайна Operator (см. [[operator-redesign-phase0-decisions]]). **S0 смержен в main** (merge-commit `82a55199`, PR #103: типизация API-клиента, feature-папка `src/players/`+тесты, dev-mock; no-behavior-change). **S1 готов** на ветке `feat/operator-clients-s1` (от свежего main) — 12 коммитов, выполнен через SDD (план `docs/superpowers/plans/2026-06-22-operator-clients-s1-redesign.md`, 11 задач). Сьют 302 pass/55 файлов + App.test 88/88 + i18n 34/34 + build чист; финальное whole-branch ревью (opus) = Ready to merge YES, 0 Critical/Important. **S1b готов** на ветке `feat/operator-clients-s1b` (от main 7e400722) — 11 коммитов, SDD, первый слайс с бэкендом (.NET). Добавлен keyset-эндпоинт `GET /api/players/{id}/ledger` → `CursorPage<LedgerEntryDto>` (новый `PlayerLedgerProjector`, зеркало `PlayerHistoryProjector`; переиспользованы `CursorPage<T>`/`CursorToken`); фронт `getLedger`; История переключена с `recentEntries` на серверный журнал + фильтр-чипы + «Показать ещё» + скелетон. Проверки: фронт 315/55 + App.test 89 + i18n 34 + бэкенд dotnet 1191 + обе сборки. Финал-ревью (opus) = Ready to merge YES, 0 Critical/Important. Контроллер-фиксы: guard `loadMoreLedger` по `ledgerLoading` (анти-двойной-клик); CSS-класс чипа `.clients-history-chip`→`.clients-history-filter` (был тихий рассинхрон с компонентом, #34). Дальше: **S2** (power-tools: ручная корректировка деньгами + полный возврат со строки истории, PIN клиента — бэк-эндпоинты УЖЕ есть: `ledger/manual-corrections`/`ledger/{id}/refunds`/`pin`) → **S3** (правка профиля + деактивация, новый бэкенд PATCH).

Унаследованный keyset-нюанс (вне S1b, общий слой): `CursorToken` усекает `CreatedAtUtc` до мс; теоретический пропуск только при >2×(limit+1) записей в одну мс у игрока — нереалистично, тот же механизм в `PlayerHistoryProjector` visits/purchases. Отложенные миноры S1 (некритичные, для S1b/полиша): двойная фильтрация поиска (сервер+клиент, унаследовано из монолита — убрать когда придёт серверная пагинация); нет теста на packageFallback; статус-бейдж строки `aria-hidden`. **Вне скоупа (отдельная POS-задача, #39):** React дубль-key варнинг `77777777…` в App.test — POS-фикстура (productId), не players. Раздел = воркспейс `players` (`BackendPlayersWorkspace.tsx`, ~650 строк, **НЕ заглушка**: поиск/профиль/кошелёк/пакеты/долг/создание уже работают). Сущность на бэке = **Player/PlayerAccount**, в вебе зовётся client.

**Решения S1 (сверх спеки, обоснованы):**
- **CI-дискавери (Task 1):** `package.json` оператора гонял `bun run test` с top-level-glob (`ls src/*.test.ts*`) → тесты в `src/players/`/`src/booking/`/`src/api/clients/` (6 файлов, вкл. S0-шные) в CI **не исполнялись** (ложный green, #37). Фикс = рекурсивный `find` (App.test по-прежнему отдельным процессом). Все 271 subdir-теста проходят — безопасно; оживляет дремлющие тесты S0/booking.
- **i18n ledger-лейблы (#29/#35):** переиспользуем готовый каталог `ledger.type.*` (ru/en/tg, native tg, 11/13 типов) вместо нового `op.players.ledger.type.*`; добавляем только `ledger.type.wallet_payment`/`cashback` + `op.players.ledger.type.fallback`.
- **drawer-каркас power-tools перенесён в S2** (YAGNI: не плодить мёртвую инфру). «Новый клиент» — через готовый `PanelModal`. feedback показывает только оркестратор (единый источник). Сегменты на стабильных id (чинит латентный баг локализованной строки).
- Бэк-факт: оператор-тест-скрипт изоляции App.test задокументирован в `.github/workflows/pr-verification.yml` (#88 mock.module leak).

**Объём (полный овэрхол): и редизайн, и функц-дыры.** Решения:
- **Подход A** — master-detail (список слева + богатая карточка с табами Кошелёк/Пакеты/История) + drawer/модалки для редкого/чувствительного (паттерн `BookingDrawer`).
- **4 фичи:** богатая история; power-tools (ручная корректировка **только деньги** + **полный** возврат); PIN клиента; правка профиля + деактивация (**только soft**, не hard-delete).
- **+S1b: серверная keyset-пагинация истории** (курсор `(CreatedAtUtc, LedgerEntryId)`) + серверный фильтр по типу. Возвращает «честный» фильтр истории.
- **YAGNI-срезы:** сегмент «Новые»/«Спящие» (нет `CreatedAtUtc`/last-visit в `PlayerSearchResultDto`); бонус-минуты в корректировке (это эпик лояльности); частичный возврат; нарядная полоса портфельных метрик.

**Слайсы (каждый = свой PR):** S0 рефактор+типизация+dev-mock+тесты → S1 визуал master-detail+богатый рендер истории (поверх RecentEntries)+человеческие пакеты → S1b ledger-эндпоинт+фильтр+«показать ещё» → S2 power-tools+PIN → S3 правка профиля+деактивация (новый бэкенд PATCH).

**Док-канон:** `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md` + `docs/superpowers/plans/2026-06-22-operator-clients-s0-refactor.md`.

**Бэк-факты (проверены 2026-06-22):**
- Операторские эндпоинты — `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs`. **Есть:** create, pin (`SetPlayerPinRequest`, ≥4 симв., право `players.create`), search, wallet-summary, top-ups, debts/payments, ledger/{id}/refunds (`RefundLedgerEntryRequest`), ledger/manual-corrections (`ManualLedgerCorrectionRequest`: AccountType wallet/debt/bonus_time + Amount + QuantitySeconds + Reason). **НЕТ:** операторского PATCH правки имени/телефона и toggle деактивации (есть только self-service `PATCH /api/me/profile`) → S3 их добавляет.
- `wallet-summary` отдаёт только `RecentEntries` (снимок, не история) → отсюда нужда в S1b `/ledger`.
- Права воркспейса `players` агрегированы в `operatorPermissions.ts`; точное право для correction/refund подтвердить при S2 (кандидаты `billing.money_action.approve`/`billing.wallet.top_up`).
- Рефактор: `projectPlayerClient`/`PlayerClientItem`/`playerPackageLabel` шарятся POS/Бронями/Картой → НЕ переносить из `operatorHelpers.ts` (цикл); `players/playersModel.ts` их ре-экспортит, переезжают только `fixturePlayers`/`playerStatusLabel`.
