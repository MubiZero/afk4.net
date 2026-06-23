---
name: afk4-operator-clients-epic
description: "Operator Этап 3 «Клиенты» — ЭПИК ЗАКРЫТ (PR #103–#108 в main). Durable доменные/архитектурные факты + урок про money-path guard"
metadata:
  node_type: memory
  type: project
  originSessionId: af6ceac8-6e34-4042-8419-c1df5efb0a7c
---

**ЭПИК «КЛИЕНТЫ» ПОЛНОСТЬЮ ЗАКРЫТ И В MAIN.** Слайсы S0..S3 (PR #103–#107) + дизайн-полировка/space-redesign (PR #108, merge `b5dce420`). См. [[operator-redesign-phase0-decisions]]. Раздел = воркспейс `players` (`BackendPlayersWorkspace.tsx`, ~700 строк, НЕ заглушка). Сущность на бэке = **Player/PlayerAccount**, в вебе зовётся **client**. Подход master-detail (список | богатая карточка с табами Кошелёк/Пакеты/История) + `PanelModal` для редкого/чувствительного.

**Доменные факты (по коду, durable):**
- **Долг** = виртуальный счёт `accountType=debt`, считается из ledger; возникает только режимом `postpaid_debt` или ручной корректировкой (нехватка кошелька ≠ долг); гасится записью `-debt` (кошелёк не трогается, нельзя > текущего долга).
- **Пакеты** = предоплаченное игровое ВРЕМЯ (included+bonus сек), шаблон `PackageDefinition` (Owner/BranchManager) → продажа клиенту оператором (цена с кошелька) → трата в режиме `package` (bonus раньше included); срок годности обязателен, истёк = сгорает.
- **`SearchPlayersAsync(includeInactive)`** — дефолт false (Касса/Брони/POS/WPF не видят неактивных); «Клиенты» зовёт true (иначе деактивированного не найти/не реактивировать).
- Серверная keyset-пагинация истории: `GET /api/players/{id}/ledger` → `CursorPage<LedgerEntryDto>` (`PlayerLedgerProjector`); `wallet-summary` отдаёт только снимок `RecentEntries`, не историю.

**Money-path `IsActive`-guard (ВАЖНЫЙ durable-урок, анти-фрод):** деактивированному игроку нельзя двигать деньги — зеркало UI-гейта + прецедент POS (`EfPosService.cs:87`). Защита в ДВУХ слоях:
1. `EndpointHelpers.RejectInactivePlayerMoneyAction(player)` → 400 на 5 per-player money-эндпоинтах (top-up/refund/manual-correction в PlayerManagement, pay-debt в MoneyAction, purchase в Package) — все грузят игрока общим `LoadPlayerScopedEndpointAsync`.
2. **`EfMoneyActionExecutor.ExecuteAsync` (anti-fraud approval-путь)** — БЕЗ него `POST /api/branches/{id}/money-actions` и `/approve` реплеили refund/correction в обход guard'а, и это достижимо ШТАТНО: over-threshold refund/correction из per-player эндпоинтов отдаёт 409 «пересдай через /money-actions». Урок: guard только на «парадных» эндпоинтах дыряв, если есть второй money-mutating front door (approval-очередь). В executor для **refund** игрок берётся из ledger-записи (нельзя подсунуть активного в `command.PlayerAccountId`), для **correction** — из команды. Возвращает `Invalid` → 422.
- Вебхук `CreditOnlineTopUpAsync` НАМЕРЕННО мимо guard'а (деньги уже сняты у плательщика, иначе зависнут).

**Архитектура раздела (фронт, durable):**
- Единый дизайн-стандарт в `@afk4/tokens`: шкала `--space-1..6` (4/8/12/16/24/32) + высоты контролов `--control-sm/md/lg` (28/36/44); контролы 36px, чипы 28, строка списка 44.
- Раздел на всю ширину, 3 зоны при ≥1280px (список | карточка `minmax(480,680)` | постоянный рейл «История») через хук `useMediaQuery` (matchMedia, default false=узкий) + `ClientLedgerRail`; на <1280 рейл → вкладка «История», мини-лента кошелька возвращается. Журнал грузится по `ledgerPaneVisible = canViewLedger && (showLedgerRail || tab==='history')`.
- Обзор базы в шапке (`buildClientOverview`: Клиенты/Депозиты/Долги, sum по загруженному списку). Кросс-контекст профиля `ClientContextStrip` («играет на РС-XX · ближайшая бронь», `buildClientContext`, аддитивный — нет данных→null).
- **Read-only бэк под кросс-контекст** (money-path не тронут): `SessionTimelineItemDto.PlayerAccountId` (заполнение в `EfSessionTimelineReadService.MapItem`); `ReservationSearchQuery.PlayerAccountId` (опц.) + query-param + `.Where` в `EfReservationService`. Фронт фильтрует и на клиенте (страховка от незафильтрованного бэка).
- Power-tools: ручная корректировка (только деньги, wallet/debt × credit/debit), полный возврат (право `billing.refund`), PIN (`players.create`), правка профиля + soft-деактивация (PATCH `/api/branches/{id}/players/{id}` + `.../active-state`); двойной гейт UI-флаг + проверка в `runClientAction`.

**Известные остаточные миноры (не блокеры, для будущих сессий):** wallet stale-flash баланса при смене клиента (pre-existing; liveContext/recentEntries уже чинятся `handleSelectClient`-сбросом); `packageBusy` глобальный, не пер-экшен (косметика — кнопка «Купить» спиннерит на любом money-действии); dev-mock не создаёт бронь (кнопка «Бронь» в превью даёт ложное «подтверждено» через 204-ack, на реале настоящая бронь).

**Док-канон:** specs `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md`; планы S0..S3 в `docs/superpowers/plans/2026-06-2*-operator-clients-*.md`.
