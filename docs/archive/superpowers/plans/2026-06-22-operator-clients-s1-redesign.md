# Operator «Клиенты» — S1 (визуальный редизайн master-detail + богатый рендер) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Переписать раздел «Клиенты» из монолита `BackendPlayersWorkspace.tsx` (~650 строк, всё-в-одной-панели) в чистый master-detail (список слева, богатая карточка справа) с человекочитаемой историей операций, человеческими пакетами, честными сегментами на стабильных id и табами Кошелёк/Пакеты/История. БЕЗ нового бэкенда, БЕЗ ledger-эндпоинта/фильтра/пагинации (это S1b), БЕЗ power-tools/PIN/правки профиля (S2/S3).

**Architecture:** Slice S1 спеки `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md` (разделы «Раскладка и IA», «Слайсинг» строка S1, «Честные ограничения»). Зеркалим паттерн `src/booking/`: оркестратор `BackendPlayersWorkspace.tsx` грузит данные ИНЛАЙН (как `BackendBookingWorkspace`), держит весь state/effects/actions, рендерит master-detail через co-located чистые компоненты в `src/players/`. БЕЗ отдельного `usePlayers`-хука (booking грузит инлайн — следуем). Чистые проекции/мапперы/лейблы — в `src/players/playersModel.ts` (расширяем существующий, S0 его уже завёл). Готовые примитивы (`Skeleton`/`EmptyState`/`StateFlag`/`FeedbackNotice` из `operatorPrimitives.tsx`, `PanelModal.tsx`, `useDeferredFlag.ts`) — ИСПОЛЬЗУЕМ, не пишем свои. Деньги/время остаются в общих хелперах (`formatMinorUnits`/`formatMoney`/`formatTime` из `operatorHelpers`) — не переносим.

**Ключевое решение про i18n лейблы типов ledger (#29/#35 — переиспользуй готовую инфру, не плоди дубль):** в каталоге УЖЕ есть полный набор `ledger.type.*` (ru/en/tg, реально таджикские), покрывающий 11 из 13 значений `LedgerEntryTypeNames`. Спека предлагала `op.players.ledger.type.*`, но это был бы дубль готовых ключей. Поэтому `ledgerTypeLabel` маппит на существующие `ledger.type.*`, и в i18n-таске добавляются ТОЛЬКО два недостающих ключа: `ledger.type.wallet_payment`, `ledger.type.cashback`.

**Tech Stack:** React + TypeScript (Vite), тесты на `bun test` (happy-dom + jest-dom, НЕ vitest). i18n `@afk4/i18n` (типизированные `MessageKey`), деньги `@afk4/money` (minor units), `lucide-react` иконки.

**Behavior-preservation contract (Task 9 ОБЯЗАН это сохранить):** после переписи в тонкий оркестратор должно по-прежнему работать всё нижеперечисленное (App.test это сторожит, селекторы поправить под новый DOM — покрытие НЕ удалять):
- дебаунс-поиск 180 мс → `searchPlayers(branchId, query, 25)` (эффект оркестратора);
- фильтр сегментами (теперь по стабильным id, не по локализованной строке);
- topUp → `POST /api/players/{id}/wallet/top-ups` (amount minor units + reason + idempotencyKey `wallet-top-up-*`);
- payDebt → `POST /api/players/{id}/debts/payments` (`debt-payment-*`), видна/активна только при debt>0;
- buyPackage → `POST /api/players/{id}/packages/purchases` (`package-purchase-*`), packageDefinitionId из опций;
- createPlayer → `POST /api/branches/{branchId}/players` (`player-create-*`) — теперь через `PanelModal`;
- бронь-из-карточки → `reservations.create(branchId, …)` под `manageReservations`, note `op.players.note.createdFromCard`;
- гейтинг по правам (`hasPermission` + `permissionNames`): topUpWallet/payDebt/purchasePackage/createPlayerAccount/manageReservations/viewPackages;
- fixture-режим при `backend === null` (`fixturePlayers`), статус загрузки через `workspaceLoadStatusLabel`;
- пустой результат поиска → EmptyState с заголовком `op.players.list.emptyTitle` («Клиенты не найдены») и текстом `op.players.list.emptyBackend`/`emptyConnect`;
- глобальные `StateFlag` в шапке (всего клиентов; на платформе) — per-client числа из шапки УБРАНЫ.

## Global Constraints

- **Bun:** все команды через `/home/fedya/.bun/bin/bun`. Тесты — `bun test` (happy-dom + jest-dom, НЕ vitest). Тайпчек/сборка — `bun run build` (= `tsc` + `vite`); сами тесты НЕ тайпчекают, тайп-ошибки ловит только `bun run build`.
- **Рабочая директория фронта:** `/home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web`.
- **`App.test.tsx` — ОТДЕЛЬНЫМ прогоном** `bun test src/App.test.tsx` (утечка `mock.module` process-wide; общий `bun test` его флакает).
- **Ветка:** `feat/operator-clients-s1`.
- **Стейджить ТОЛЬКО файлы своего таска явным `git add <path>`** — НЕ `git commit -a/-am` (в репо `.claude/memory/` под гитом, sweeping ловит лишнее).
- **Никаких AI-подписей** нигде (ни в коммитах, ни в коде, ни в комментариях).
- **Деньги** в minor units; форматирование на границе UI существующими `formatMinorUnits`/`formatMoney` — своих форматтеров не плодить.
- **i18n:** новые ключи добавляются в `locales/{ru,en,tg}.json` (в КОРНЕ репо), затем `bun run gen` в `packages/i18n` регенерит `messages.ts`. tg — РЕАЛЬНЫЙ таджикский, не копия ru (guard `messages.test.ts` против `tg===ru`; новые ключи НЕ добавлять в whitelist).
- **Акцент оператора синий `#1f6feb`** (через токены `var(--accent)` и т.п.); тёмная тема по умолчанию.
- **Класс-контракт CSS** (имена классов) обязан совпадать между компонентами (Tasks 4-9) и `styles/12-players.css` (Task 10).

---

### Task 1: Рекурсивная дискавери тестов в CI (`package.json`)

CI оператора гоняет `bun run test`; его текущий glob `ls src/*.test.ts src/*.test.tsx` берёт только top-level `src/` → тесты в подкаталогах (`src/players/`, `src/booking/`, `src/api/clients/`) в CI **не исполняются вовсе** (6 файлов, включая S0-шные `players/playersModel.test.ts` и `api/clients/players.test.ts` — ложный green, #37/#39). S1 добавляет ~7 файлов тестов в `src/players/` — без этой починки они зелёные «на бумаге», но в CI не гоняются. Фикс: рекурсивная дискавери, изоляция `App.test` сохранена. Все subdir-тесты уже проходят (проверено: 271 pass), включение безопасно.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/package.json` (скрипт `test`)

**Interfaces:** нет (инфраструктурная правда).

- [ ] **Step 1: Заменить glob на рекурсивный `find`**

В `src/AFK4.Operator.App.Web/package.json` заменить строку скрипта `test`:

```jsonc
"test": "bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && bun test src/App.test.tsx",
```

на:

```jsonc
"test": "bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && bun test src/App.test.tsx",
```

(`App.test.tsx` по-прежнему гоняется отдельным процессом — изоляция от cross-file `mock.module` leak сохранена; см. комментарий в `.github/workflows/pr-verification.yml`.)

- [ ] **Step 2: Прогнать `bun run test`**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
Expected: первый прогон теперь включает `src/players/`+`src/booking/`+`src/api/clients/` (≈271 pass, было меньше — subdir-файлы добавились), затем отдельный прогон `src/App.test.tsx` зелёный.

- [ ] **Step 3: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/package.json
git commit -m "test(operator): рекурсивная дискавери тестов — гонять src/players и src/booking в CI (были немыми)"
```

---

### Task 2: i18n — новые ключи ru/en/tg + `bun run gen` + чистка мёртвых ключей

Добавить недостающие каталожные ключи (два `ledger.type.*` + новые `op.players.*` для табов/чипов/истории/пакетов/модалки/fallback), регенерить `messages.ts`, удалить мёртвые ключи сегментов «Новые»/«Спящие». tg — реальный таджикский.

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `packages/i18n/src/messages.ts` (через `bun run gen`, не руками)

**Interfaces:**
- Produces: новые `MessageKey`-значения, доступные `t()` во всех тасках.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`**

Вставить следующие пары (порядок внутри файла не критичен; рекомендуется рядом с существующими `op.players.*` / `ledger.type.*`). Также обновить существующее значение `op.players.segments.inactive` — было «неактивные», должно быть «Неактивные» (консистентность капитализации с другими сегмент-чипами «Все»/«VIP»/«Есть долг»):

```jsonc
// обновить существующий ключ (капитализация):
"op.players.segments.inactive": "Неактивные",
// недостающие типы ledger (остальные 11 уже есть в ledger.type.*)
"ledger.type.wallet_payment": "Оплата с кошелька",
"ledger.type.cashback": "Кэшбэк",
// fallback для неизвестного типа операции
"op.players.ledger.type.fallback": "Операция",
// табы карточки
"op.players.tabs.wallet": "Кошелёк",
"op.players.tabs.packages": "Пакеты",
"op.players.tabs.history": "История",
// чипы в шапке карточки
"op.players.chip.balance": "Баланс",
"op.players.chip.debt": "Долг",
"op.players.chip.packages": "Пакеты",
// секция «Пакеты»
"op.players.packages.includedMinutes": "{minutes} мин в пакете",
"op.players.packages.bonusMinutes": "+{minutes} бонусных мин",
"op.players.packages.expiresOn": "до {date}",
"op.players.packages.perpetual": "бессрочно",
"op.players.packages.expired": "истёк",
"op.players.packages.emptyTitle": "Нет активных пакетов",
"op.players.packages.emptyDescription": "У клиента нет купленных пакетов.",
"op.players.packages.buyTitle": "Купить пакет",
// секция «История»
"op.players.history.emptyTitle": "Операций нет",
"op.players.history.emptyDescription": "По клиенту пока нет операций.",
"op.players.history.reversalBadge": "сторно",
// секция «Кошелёк»
"op.players.wallet.balanceLabel": "Баланс",
"op.players.wallet.debtLabel": "Долг",
"op.players.wallet.topUpTitle": "Пополнить депозит",
"op.players.wallet.payDebtTitle": "Погасить долг",
// модалка «Новый клиент»
"op.players.newClient.title": "Новый клиент",
"op.players.newClient.subtitle": "карточка клуба",
"op.players.newClient.submit": "Создать",
"op.players.newClient.openBtn": "Новый клиент",
// шапка раздела
"op.players.detail.reservationBtn": "Бронь"
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`** (+ обновить `op.players.segments.inactive`)

```jsonc
"op.players.segments.inactive": "Inactive",
"ledger.type.wallet_payment": "Wallet payment",
"ledger.type.cashback": "Cashback",
"op.players.ledger.type.fallback": "Operation",
"op.players.tabs.wallet": "Wallet",
"op.players.tabs.packages": "Packages",
"op.players.tabs.history": "History",
"op.players.chip.balance": "Balance",
"op.players.chip.debt": "Debt",
"op.players.chip.packages": "Packages",
"op.players.packages.includedMinutes": "{minutes} min in package",
"op.players.packages.bonusMinutes": "+{minutes} bonus min",
"op.players.packages.expiresOn": "until {date}",
"op.players.packages.perpetual": "no expiry",
"op.players.packages.expired": "expired",
"op.players.packages.emptyTitle": "No active packages",
"op.players.packages.emptyDescription": "The client has no purchased packages.",
"op.players.packages.buyTitle": "Buy package",
"op.players.history.emptyTitle": "No operations",
"op.players.history.emptyDescription": "No operations for this client yet.",
"op.players.history.reversalBadge": "reversal",
"op.players.wallet.balanceLabel": "Balance",
"op.players.wallet.debtLabel": "Debt",
"op.players.wallet.topUpTitle": "Top up deposit",
"op.players.wallet.payDebtTitle": "Pay off debt",
"op.players.newClient.title": "New client",
"op.players.newClient.subtitle": "club card",
"op.players.newClient.submit": "Create",
"op.players.newClient.openBtn": "New client",
"op.players.detail.reservationBtn": "Reservation"
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json` (реальный таджикский)** (+ обновить `op.players.segments.inactive`)

```jsonc
"op.players.segments.inactive": "Ғайрифаъол",
"ledger.type.wallet_payment": "Пардохт аз ҳамён",
"ledger.type.cashback": "Бозгашти маблағ",
"op.players.ledger.type.fallback": "Амалиёт",
"op.players.tabs.wallet": "Ҳамён",
"op.players.tabs.packages": "Пакетҳо",
"op.players.tabs.history": "Таърих",
"op.players.chip.balance": "Бақия",
"op.players.chip.debt": "Қарз",
"op.players.chip.packages": "Пакетҳо",
"op.players.packages.includedMinutes": "{minutes} дақиқа дар пакет",
"op.players.packages.bonusMinutes": "+{minutes} дақиқаи бонусӣ",
"op.players.packages.expiresOn": "то {date}",
"op.players.packages.perpetual": "бемуҳлат",
"op.players.packages.expired": "мӯҳлаташ гузашт",
"op.players.packages.emptyTitle": "Пакетҳои фаъол нестанд",
"op.players.packages.emptyDescription": "Муштарӣ пакети харидашуда надорад.",
"op.players.packages.buyTitle": "Харидани пакет",
"op.players.history.emptyTitle": "Амалиёт нест",
"op.players.history.emptyDescription": "Барои ин муштарӣ ҳоло амалиёт нест.",
"op.players.history.reversalBadge": "сторно",
"op.players.wallet.balanceLabel": "Бақия",
"op.players.wallet.debtLabel": "Қарз",
"op.players.wallet.topUpTitle": "Пур кардани депозит",
"op.players.wallet.payDebtTitle": "Пардохти қарз",
"op.players.newClient.title": "Муштарии нав",
"op.players.newClient.subtitle": "корти клуб",
"op.players.newClient.submit": "Эҷод кардан",
"op.players.newClient.openBtn": "Муштарии нав",
"op.players.detail.reservationBtn": "Брон"
```

Примечание про tg-guard (`messages.test.ts`): `op.players.history.reversalBadge` = «сторно» совпадает с ru. `ledger.type.reversal` уже в whitelist по этой же причине. Добавить `op.players.history.reversalBadge` в `TG_IDENTICAL_TO_RU_ALLOWED` в `packages/i18n/src/messages.test.ts` с обоснованием (займствование/термин), либо — предпочтительно — дать реальный таджикский вариант, чтобы не расширять whitelist. **Рекомендация:** оставить «сторно» (это интернациональный бухгалтерский термин, уже узаконенный для `ledger.type.reversal`) и добавить ключ в whitelist. Остальные tg-значения отличны от ru и в whitelist не нуждаются.

- [ ] **Step 4: Удалить мёртвые ключи сегментов «Новые»/«Спящие»**

Сегменты `op.players.segments.new` и `op.players.segments.sleeping` после Task 9 больше не используются (честные сегменты их выбросили). Проверить grep по всему вебу:

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && grep -rn "segments.new\|segments.sleeping\|segments.fromSearch\|segments.clients\|strip.deposit\|strip.entries\|strip.label" src/
```

Если ссылок НЕТ (после Task 9 их быть не должно — но этот таск выполняется ДО Task 9, поэтому здесь grep ещё покажет старый `BackendPlayersWorkspace.tsx`). **Порядок:** удалять мёртвые ключи только в финальной зачистке после Task 9. На этом шаге Task 2 — только ДОБАВЛЕНИЕ и ОБНОВЛЕНИЕ ключей; удаление вынести в Task 9 Step (после переписи оркестратора), чтобы не оставить висячих ссылок. (См. Task 9.)

- [ ] **Step 5: Регенерить `messages.ts`**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `generated …/messages.ts from 3 locales`.

- [ ] **Step 6: Прогнать i18n-гарды + тайпчек фронта**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test && cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: i18n-тесты зелёные (parity ru=en=tg; voice без caps/«компьютер»; tg≠ru guard проходит при условии whitelist-записи для `op.players.history.reversalBadge` из Step 3); `bun run build` проходит — новые ключи (`op.players.ledger.type.fallback` и др.) теперь в `MessageKey`, модель и компоненты идут после.

- [ ] **Step 7: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts packages/i18n/src/messages.test.ts
git commit -m "i18n(operator-clients): ключи табов/чипов/истории/пакетов/модалки клиентов (ru/en/tg)"
```

---

### Task 3: Модель — `ledgerTypeLabel` + `projectLedgerEntry` + `projectPlayerPackage` + честные сегменты

Расширить `src/players/playersModel.ts` чистыми функциями для богатого рендера и стабильными сегментами. Сегменты сейчас в оркестраторе хранят локализованную строку (`activeSegment` = `t('op.players.segments.all')`) и ломаются при смене языка — чиним на стабильные id.

**Files:**
- Modify: `src/players/playersModel.ts`
- Modify: `src/players/playersModel.test.ts`

**Interfaces:**
- Consumes: `LedgerEntryDto`, `PlayerPackageDto` из `../operatorApiClients`; `PlayerClientItem`, `TFunc`, `formatTime` из `../operatorHelpers`; `MessageKey` из `@afk4/i18n`.
- Produces (экспорт из `playersModel.ts`):
  - `ledgerTypeLabel(entryType: string, t: TFunc): string`
  - `interface LedgerEntryView { id: string; timeLabel: string; typeLabel: string; description: string; reason: string; amountMinorUnits: number; currencyCode: string; isCredit: boolean; isReversal: boolean }`
  - `projectLedgerEntry(entry: LedgerEntryDto, t: TFunc): LedgerEntryView`
  - `interface PlayerPackageView { id: string; name: string; remainingIncludedMinutes: number; remainingBonusMinutes: number; totalRemainingMinutes: number; expiryLabel: string | null; isExpired: boolean }`
  - `projectPlayerPackage(pkg: PlayerPackageDto, t: TFunc, locale: string): PlayerPackageView`
  - `type ClientSegmentId = 'all' | 'vip' | 'debt' | 'inactive'`
  - `interface ClientSegment { id: ClientSegmentId; label: string; count: number }`
  - `buildClientSegments(clients: PlayerClientItem[], t: TFunc): ClientSegment[]`
  - `matchesSegment(client: PlayerClientItem, id: ClientSegmentId): boolean`

- [ ] **Step 1: Написать падающий тест** — добавить в КОНЕЦ `src/players/playersModel.test.ts`

```ts
import {
  ledgerTypeLabel,
  projectLedgerEntry,
  projectPlayerPackage,
  buildClientSegments,
  matchesSegment,
  type ClientSegmentId
} from './playersModel';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';
import type { PlayerClientItem } from '../operatorHelpers';

const ledger = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

const pkg = (over: Partial<PlayerPackageDto>): PlayerPackageDto => ({
  playerPackageId: 'pp-x', packageDefinitionId: 'pd-x', playerAccountId: 'p',
  name: 'Ночной 5ч', purchasedPrice: { currencyCode: 'TJS', minorUnits: 25000 },
  includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800,
  purchasedAtUtc: '2026-06-21T09:00:00Z', expiresAtUtc: null, ...over
});

const client = (over: Partial<PlayerClientItem>): PlayerClientItem => ({
  playerAccountId: 'p', name: 'X', status: 'active', balanceMinorUnits: 0,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '',
  source: 'backend', ...over
});

describe('ledgerTypeLabel', () => {
  it('maps known entry types to the shared ledger.type.* keys, unknown → fallback key', () => {
    expect(ledgerTypeLabel('top_up', t)).toBe('ledger.type.top_up');
    expect(ledgerTypeLabel('gameplay_charge', t)).toBe('ledger.type.gameplay_charge');
    expect(ledgerTypeLabel('package_purchase', t)).toBe('ledger.type.package_purchase');
    expect(ledgerTypeLabel('bonus_grant', t)).toBe('ledger.type.bonus_grant');
    expect(ledgerTypeLabel('debt_payment', t)).toBe('ledger.type.debt_payment');
    expect(ledgerTypeLabel('refund', t)).toBe('ledger.type.refund');
    expect(ledgerTypeLabel('manual_correction', t)).toBe('ledger.type.manual_correction');
    expect(ledgerTypeLabel('wallet_payment', t)).toBe('ledger.type.wallet_payment');
    expect(ledgerTypeLabel('cashback', t)).toBe('ledger.type.cashback');
    expect(ledgerTypeLabel('reversal', t)).toBe('ledger.type.reversal');
    expect(ledgerTypeLabel('mystery_type', t)).toBe('op.players.ledger.type.fallback');
  });
});

describe('projectLedgerEntry', () => {
  it('projects credit/debit by amount sign and flags reversals', () => {
    const credit = projectLedgerEntry(ledger({ ledgerEntryId: 'le-1', entryType: 'top_up', amount: { currencyCode: 'TJS', minorUnits: 5000 }, description: 'Пополнение', reason: 'Касса' }), t);
    expect(credit.id).toBe('le-1');
    expect(credit.isCredit).toBe(true);
    expect(credit.isReversal).toBe(false);
    expect(credit.amountMinorUnits).toBe(5000);
    expect(credit.currencyCode).toBe('TJS');
    expect(credit.description).toBe('Пополнение');
    expect(credit.reason).toBe('Касса');
    expect(credit.typeLabel).toBe('ledger.type.top_up');

    const debit = projectLedgerEntry(ledger({ entryType: 'gameplay_charge', amount: { currencyCode: 'TJS', minorUnits: -1200 } }), t);
    expect(debit.isCredit).toBe(false);

    const reversal = projectLedgerEntry(ledger({ entryType: 'refund', reversesLedgerEntryId: 'le-1' }), t);
    expect(reversal.isReversal).toBe(true);
  });
});

describe('projectPlayerPackage', () => {
  it('converts remaining seconds to minutes and labels expiry / perpetual / expired', () => {
    const perpetual = projectPlayerPackage(pkg({ expiresAtUtc: null }), t, 'ru-RU');
    expect(perpetual.remainingIncludedMinutes).toBe(150); // 9000/60
    expect(perpetual.remainingBonusMinutes).toBe(30);     // 1800/60
    expect(perpetual.totalRemainingMinutes).toBe(180);
    expect(perpetual.expiryLabel).toBeNull();
    expect(perpetual.isExpired).toBe(false);

    const dated = projectPlayerPackage(pkg({ expiresAtUtc: '2099-01-01T00:00:00Z' }), t, 'ru-RU');
    expect(typeof dated.expiryLabel).toBe('string');
    expect(dated.isExpired).toBe(false);

    const expired = projectPlayerPackage(pkg({ expiresAtUtc: '2000-01-01T00:00:00Z' }), t, 'ru-RU');
    expect(expired.isExpired).toBe(true);
  });
});

describe('client segments (stable ids — survive locale change)', () => {
  it('buildClientSegments returns four stable-id segments with correct counts', () => {
    const clients = [
      client({ tone: 'vip', status: 'package' }),
      client({ debtMinorUnits: 3500, status: 'debt' }),
      client({ status: 'inactive', tone: 'regular' }),
      client({ status: 'active' })
    ];
    const segments = buildClientSegments(clients, t);
    expect(segments.map((s) => s.id)).toEqual(['all', 'vip', 'debt', 'inactive']);
    const byId = (id: ClientSegmentId) => segments.find((s) => s.id === id)!;
    expect(byId('all').count).toBe(4);
    expect(byId('vip').count).toBe(1);
    expect(byId('debt').count).toBe(1);
    expect(byId('inactive').count).toBe(1);
    expect(byId('all').label).toBe('op.players.segments.all');
  });

  it('matchesSegment filters by real fields', () => {
    expect(matchesSegment(client({ tone: 'vip' }), 'all')).toBe(true);
    expect(matchesSegment(client({ tone: 'vip' }), 'vip')).toBe(true);
    expect(matchesSegment(client({ tone: 'active' }), 'vip')).toBe(false);
    expect(matchesSegment(client({ debtMinorUnits: 100 }), 'debt')).toBe(true);
    expect(matchesSegment(client({ debtMinorUnits: 0 }), 'debt')).toBe(false);
    expect(matchesSegment(client({ status: 'inactive' }), 'inactive')).toBe(true);
    expect(matchesSegment(client({ status: 'active' }), 'inactive')).toBe(false);
  });
});
```

(Стаб `t` уже объявлен в начале файла: `const t = ((key: string) => key) as unknown as TFunc;` — возвращает ключ.)

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/playersModel.test.ts`
Expected: FAIL — `ledgerTypeLabel`/`projectLedgerEntry`/`projectPlayerPackage`/`buildClientSegments`/`matchesSegment` ещё не экспортированы.

- [ ] **Step 3: Дописать функции в `src/players/playersModel.ts`**

Заменить верхнюю строку импорта на расширенную и добавить новые функции в конец файла. Сначала импорт-строка (строка 5):

```ts
import { formatMinorUnits, formatTime, type PlayerClientItem, type TFunc } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';
```

Затем добавить в конец `src/players/playersModel.ts`:

```ts
// Карта entryType → существующий каталожный ключ ledger.type.* (ru/en/tg уже заполнены, в т.ч.
// реальный таджикский). Используем общий каталог вместо дубля op.players.ledger.type.* (#29).
// Значения entryType — из AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames.
const LEDGER_TYPE_KEYS: Record<string, MessageKey> = {
  top_up: 'ledger.type.top_up',
  gameplay_charge: 'ledger.type.gameplay_charge',
  package_purchase: 'ledger.type.package_purchase',
  package_consumption: 'ledger.type.package_consumption',
  bonus_grant: 'ledger.type.bonus_grant',
  bonus_consumption: 'ledger.type.bonus_consumption',
  refund: 'ledger.type.refund',
  manual_correction: 'ledger.type.manual_correction',
  postpaid_debt: 'ledger.type.postpaid_debt',
  debt_payment: 'ledger.type.debt_payment',
  wallet_payment: 'ledger.type.wallet_payment',
  reversal: 'ledger.type.reversal',
  cashback: 'ledger.type.cashback'
};

export function ledgerTypeLabel(entryType: string, t: TFunc): string {
  const key = LEDGER_TYPE_KEYS[entryType];
  return key ? t(key) : t('op.players.ledger.type.fallback');
}

export interface LedgerEntryView {
  id: string;
  timeLabel: string;
  typeLabel: string;
  description: string;
  reason: string;
  amountMinorUnits: number;
  currencyCode: string;
  isCredit: boolean;   // знак суммы: >=0 = кредит (зелёный), <0 = дебет (красный)
  isReversal: boolean; // запись реверсирует другую (reversesLedgerEntryId != null)
}

export function projectLedgerEntry(entry: LedgerEntryDto, t: TFunc): LedgerEntryView {
  const minorUnits = entry.amount?.minorUnits ?? 0;
  return {
    id: entry.ledgerEntryId,
    timeLabel: formatTime(entry.createdAtUtc),
    typeLabel: ledgerTypeLabel(entry.entryType, t),
    description: entry.description ?? '',
    reason: entry.reason ?? '',
    amountMinorUnits: minorUnits,
    currencyCode: entry.amount?.currencyCode ?? '',
    isCredit: minorUnits >= 0,
    isReversal: entry.reversesLedgerEntryId != null
  };
}

export interface PlayerPackageView {
  id: string;
  name: string;
  remainingIncludedMinutes: number;
  remainingBonusMinutes: number;
  totalRemainingMinutes: number;
  expiryLabel: string | null; // локализованная дата срока; null = бессрочно
  isExpired: boolean;
}

export function projectPlayerPackage(pkg: PlayerPackageDto, t: TFunc, locale: string): PlayerPackageView {
  const includedMinutes = Math.floor((pkg.remainingIncludedSeconds ?? 0) / 60);
  const bonusMinutes = Math.floor((pkg.remainingBonusSeconds ?? 0) / 60);
  const expiresAt = pkg.expiresAtUtc ? new Date(pkg.expiresAtUtc) : null;
  const validExpiry = expiresAt !== null && !Number.isNaN(expiresAt.getTime());
  return {
    id: pkg.playerPackageId,
    name: pkg.name || t('op.players.profile.packageFallback'),
    remainingIncludedMinutes: includedMinutes,
    remainingBonusMinutes: bonusMinutes,
    totalRemainingMinutes: includedMinutes + bonusMinutes,
    expiryLabel: validExpiry ? expiresAt!.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' }) : null,
    isExpired: validExpiry ? expiresAt!.getTime() < Date.now() : false
  };
}

// Честные сегменты по реальным полям, на СТАБИЛЬНЫХ id (label локализуется на render —
// id не зависит от языка, что чинит латентный баг с фильтром по локализованной строке).
export type ClientSegmentId = 'all' | 'vip' | 'debt' | 'inactive';

export interface ClientSegment {
  id: ClientSegmentId;
  label: string;
  count: number;
}

export function matchesSegment(client: PlayerClientItem, id: ClientSegmentId): boolean {
  switch (id) {
    case 'all':
      return true;
    case 'vip':
      return client.tone === 'vip';
    case 'debt':
      return client.debtMinorUnits > 0;
    case 'inactive':
      return client.status === 'inactive';
    default:
      return false;
  }
}

export function buildClientSegments(clients: PlayerClientItem[], t: TFunc): ClientSegment[] {
  const ids: ClientSegmentId[] = ['all', 'vip', 'debt', 'inactive'];
  const labels: Record<ClientSegmentId, MessageKey> = {
    all: 'op.players.segments.all',
    vip: 'op.players.segments.vip',
    debt: 'op.players.segments.debt',
    inactive: 'op.players.segments.inactive'
  };
  return ids.map((id) => ({
    id,
    label: t(labels[id]),
    count: clients.filter((client) => matchesSegment(client, id)).length
  }));
}
```

И добавить импорт `MessageKey` в шапку файла (строка 5, рядом с импортами из `@afk4/i18n` — если в playersModel ещё нет импорта из `@afk4/i18n`, добавить):

```ts
import type { MessageKey } from '@afk4/i18n';
```

Примечание: `op.players.ledger.type.fallback` и `op.players.segments.inactive` — это валидные ключи (оба добавлены/обновлены в Task 2, который идёт раньше). Тест Step 1 на стабе `t` сравнивает с СТРОКОЙ-ключом и работает независимо от каталога.

- [ ] **Step 4: Запустить тесты модели — убедиться PASS**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/playersModel.test.ts`
Expected: PASS (все describe-блоки зелёные).

- [ ] **Step 4: Запустить тесты модели — убедиться PASS**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/playersModel.test.ts`
Expected: PASS (все describe-блоки зелёные).

- [ ] **Step 5: Прогнать сборку + коммит**

Ключи (`op.players.ledger.type.fallback` и др.) уже добавлены в Task 2 — сборка должна проходить чисто.

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: чисто (ключи уже в `MessageKey` после Task 2).

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/playersModel.ts src/AFK4.Operator.App.Web/src/players/playersModel.test.ts
git commit -m "feat(operator-clients): модель богатой истории, человеч. пакетов и стабильных сегментов"
```

---

### Task 4: `HistorySection.tsx` (+ тест) — богатый рендер истории поверх `recentEntries`

Чистый презентационный компонент: рендерит проекции `LedgerEntryView` (дата/время, тип, описание+причина, сумма со знаком/цветом, пометка сторно). EmptyState при пустом списке. БЕЗ фильтра/пагинации (S1b).

**Files:**
- Create: `src/players/HistorySection.tsx`
- Create: `src/players/HistorySection.test.tsx`

**Interfaces:**
- Consumes: `LedgerEntryView`, `projectLedgerEntry` из `./playersModel`; `LedgerEntryDto` из `../operatorApiClients`; `formatMinorUnits` из `../operatorHelpers`; `EmptyState` из `../operatorPrimitives`; `useI18n` из `@afk4/i18n`.
- Produces: `function HistorySection({ entries, currencyCode }: { entries: LedgerEntryDto[]; currencyCode: string }): JSX.Element`.

- [ ] **Step 1: Написать падающий тест** `src/players/HistorySection.test.tsx`

```tsx
import { describe, expect, it } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { afterEach } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HistorySection } from './HistorySection';
import type { LedgerEntryDto } from '../operatorApiClients';

afterEach(cleanup);

const entry = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

const renderSection = (entries: LedgerEntryDto[]) =>
  render(
    <I18nProvider initialLocale="ru">
      <HistorySection entries={entries} currencyCode="TJS" />
    </I18nProvider>
  );

describe('HistorySection', () => {
  it('renders localized type, description, reason and reversal badge', () => {
    renderSection([
      entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' }),
      entry({ ledgerEntryId: 'le-2', entryType: 'refund', amount: { currencyCode: 'TJS', minorUnits: -2500 }, reversesLedgerEntryId: 'le-1', description: 'Возврат' })
    ]);
    expect(screen.getByText('Пополнение')).toBeInTheDocument();       // ledger.type.top_up
    expect(screen.getByText('Пополнение кошелька')).toBeInTheDocument();
    expect(screen.getByText(/Касса/)).toBeInTheDocument();
    expect(screen.getByText('Возврат')).toBeInTheDocument();          // ledger.type.refund
    expect(screen.getByText('сторно')).toBeInTheDocument();           // reversal badge
  });

  it('renders the EmptyState when there are no entries', () => {
    renderSection([]);
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('applies credit/debit class by amount sign', () => {
    const { container } = renderSection([
      entry({ ledgerEntryId: 'c', amount: { currencyCode: 'TJS', minorUnits: 5000 } }),
      entry({ ledgerEntryId: 'd', amount: { currencyCode: 'TJS', minorUnits: -1200 } })
    ]);
    expect(container.querySelector('.client-history-row.is-credit')).not.toBeNull();
    expect(container.querySelector('.client-history-row.is-debit')).not.toBeNull();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/HistorySection.test.tsx`
Expected: FAIL — `Cannot find module './HistorySection'`.

- [ ] **Step 3: Создать `src/players/HistorySection.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { History } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { EmptyState } from '../operatorPrimitives';
import { projectLedgerEntry } from './playersModel';

// Богатый журнал операций клиента поверх снимка wallet-summary.recentEntries. Источник правды
// (paged ledger-эндпоинт) и фильтр/пагинация — S1b; здесь только человекочитаемый рендер.
export function HistorySection({ entries, currencyCode }: { entries: LedgerEntryDto[]; currencyCode: string }) {
  const { t } = useI18n();

  if (entries.length === 0) {
    return (
      <EmptyState
        icon={<History size={20} aria-hidden="true" />}
        title={t('op.players.history.emptyTitle')}
        description={t('op.players.history.emptyDescription')}
      />
    );
  }

  return (
    <div className="clients-history-list">
      {entries.map((raw) => {
        const view = projectLedgerEntry(raw, t);
        const sign = view.isCredit ? '+' : '−';
        const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
        return (
          <article key={view.id} className={`client-history-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
            <span className="client-history-time">{view.timeLabel}</span>
            <div className="client-history-body">
              <strong>
                {view.typeLabel}
                {view.isReversal && <em className="client-history-reversal">{t('op.players.history.reversalBadge')}</em>}
              </strong>
              {(view.description || view.reason) && (
                <span className="client-history-detail">
                  {[view.description, view.reason].filter(Boolean).join(' · ')}
                </span>
              )}
            </div>
            <b className="client-history-amount">{sign}{amount}</b>
          </article>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/HistorySection.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS; `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/HistorySection.tsx src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx
git commit -m "feat(operator-clients): HistorySection — богатый рендер истории операций"
```

---

### Task 5: `PackagesSection.tsx` (+ тест) — человекочитаемые пакеты + инлайн-покупка

Чистый компонент: список активных пакетов через `projectPlayerPackage` (остаток мин/бонус/срок), EmptyState; инлайн-покупка (select опций + превью цены/минут/бонуса/срока/хватает-ли-депозита + кнопка купить). Заменяет хардкод `<b>active</b>`.

**Files:**
- Create: `src/players/PackagesSection.tsx`
- Create: `src/players/PackagesSection.test.tsx`

**Interfaces:**
- Consumes: `PlayerPackageView`, `projectPlayerPackage` из `./playersModel`; `PlayerPackageDto`, `PackageOptionDto` из `../operatorApiClients`; `formatMinorUnits`, `packageOptionLabel`, `readString`, `readNumber` из `../operatorHelpers`; `EmptyState` из `../operatorPrimitives`; `useI18n` из `@afk4/i18n`.
- Produces:
```ts
function PackagesSection(props: {
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  selectedPackageDefinitionId: string;
  balanceMinorUnits: number;
  currencyCode: string;
  canPurchase: boolean;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
}): JSX.Element
```

- [ ] **Step 1: Написать падающий тест** `src/players/PackagesSection.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PackagesSection } from './PackagesSection';
import type { PlayerPackageDto, PackageOptionDto } from '../operatorApiClients';

afterEach(cleanup);

const pkg = (over: Partial<PlayerPackageDto>): PlayerPackageDto => ({
  playerPackageId: 'pp-1', packageDefinitionId: 'pd-1', playerAccountId: 'p',
  name: 'Ночной 5ч', purchasedPrice: { currencyCode: 'TJS', minorUnits: 25000 },
  includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800,
  purchasedAtUtc: '2026-06-21T09:00:00Z', expiresAtUtc: null, ...over
});

const option = (over: Partial<PackageOptionDto>): PackageOptionDto => ({
  packageDefinitionId: 'pd-2', name: 'Утренний 2ч', currencyCode: 'TJS',
  priceMinorUnits: 12000, includedSeconds: 7200, bonusSeconds: 1800, expiresAfterDays: 14
} as PackageOptionDto);

const renderSection = (over: Partial<Parameters<typeof PackagesSection>[0]> = {}) => {
  const onSelectOption = mock(() => {});
  const onBuy = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <PackagesSection
        packages={[pkg({})]}
        options={[option({})]}
        selectedPackageDefinitionId="pd-2"
        balanceMinorUnits={50000}
        currencyCode="TJS"
        canPurchase
        onSelectOption={onSelectOption}
        onBuy={onBuy}
        {...over}
      />
    </I18nProvider>
  );
  return { onSelectOption, onBuy };
};

describe('PackagesSection', () => {
  it('renders human-readable package remaining minutes and bonus', () => {
    renderSection();
    expect(screen.getByText('Ночной 5ч')).toBeInTheDocument();
    expect(screen.getByText(/150 мин в пакете/)).toBeInTheDocument();
    expect(screen.getByText(/\+30 бонусных мин/)).toBeInTheDocument();
    expect(screen.getByText('бессрочно')).toBeInTheDocument();
  });

  it('renders the EmptyState when there are no packages', () => {
    renderSection({ packages: [] });
    expect(screen.getByText('Нет активных пакетов')).toBeInTheDocument();
  });

  it('calls onBuy when the purchase button is clicked and affordable', () => {
    const { onBuy } = renderSection();
    fireEvent.click(screen.getByRole('button', { name: /Купить пакет/ }));
    expect(onBuy).toHaveBeenCalled();
  });

  it('disables purchase when balance is below the option price', () => {
    renderSection({ balanceMinorUnits: 0 });
    expect(screen.getByRole('button', { name: /Купить пакет/ })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/PackagesSection.test.tsx`
Expected: FAIL — `Cannot find module './PackagesSection'`.

- [ ] **Step 3: Создать `src/players/PackagesSection.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { Package, TimerReset } from 'lucide-react';
import type { PlayerPackageDto, PackageOptionDto } from '../operatorApiClients';
import { formatMinorUnits, packageOptionLabel, readNumber, readString } from '../operatorHelpers';
import { EmptyState } from '../operatorPrimitives';
import { projectPlayerPackage } from './playersModel';

// Человекочитаемые активные пакеты + инлайн-покупка. Заменяет хардкод <b>active</b>.
export function PackagesSection({
  packages,
  options,
  selectedPackageDefinitionId,
  balanceMinorUnits,
  currencyCode,
  canPurchase,
  onSelectOption,
  onBuy
}: {
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  selectedPackageDefinitionId: string;
  balanceMinorUnits: number;
  currencyCode: string;
  canPurchase: boolean;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
}) {
  const { t, locale } = useI18n();

  const selectedOption = options.find((option) => readString(option, 'packageDefinitionId') === selectedPackageDefinitionId)
    ?? options[0]
    ?? null;
  const priceMinorUnits = selectedOption === null ? 0 : readNumber(selectedOption, 'priceMinorUnits', 0);
  const optionCurrency = selectedOption === null ? currencyCode : readString(selectedOption, 'currencyCode', currencyCode);
  const includedMinutes = selectedOption === null ? 0 : Math.floor(readNumber(selectedOption, 'includedSeconds', 0) / 60);
  const bonusMinutes = selectedOption === null ? 0 : Math.floor(readNumber(selectedOption, 'bonusSeconds', 0) / 60);
  const totalMinutes = includedMinutes + bonusMinutes;
  const expiresDays = selectedOption === null ? 0 : readNumber(selectedOption, 'expiresAfterDays', 0);
  const canAfford = selectedOption !== null && balanceMinorUnits >= priceMinorUnits;

  return (
    <div className="clients-packages-section">
      {packages.length === 0 ? (
        <EmptyState
          icon={<Package size={20} aria-hidden="true" />}
          title={t('op.players.packages.emptyTitle')}
          description={t('op.players.packages.emptyDescription')}
        />
      ) : (
        <div className="client-package-list" aria-label={t('op.players.profile.packagesLabel')}>
          {packages.map((raw) => {
            const view = projectPlayerPackage(raw, t, locale);
            const expiry = view.isExpired
              ? t('op.players.packages.expired')
              : view.expiryLabel
                ? t('op.players.packages.expiresOn', { date: view.expiryLabel })
                : t('op.players.packages.perpetual');
            return (
              <article key={view.id} className={`client-package-row${view.isExpired ? ' is-expired' : ''}`}>
                <strong>{view.name}</strong>
                <span>{t('op.players.packages.includedMinutes', { minutes: view.remainingIncludedMinutes })}</span>
                {view.remainingBonusMinutes > 0 && (
                  <span className="client-package-bonus">{t('op.players.packages.bonusMinutes', { minutes: view.remainingBonusMinutes })}</span>
                )}
                <b>{expiry}</b>
              </article>
            );
          })}
        </div>
      )}

      <div className="clients-package-buy">
        <strong className="clients-section-title">{t('op.players.packages.buyTitle')}</strong>
        <label className="clients-package-select">
          {t('op.players.actions.packageSelectLabel')}
          <select
            value={selectedOption === null ? '' : readString(selectedOption, 'packageDefinitionId')}
            disabled={!canPurchase || options.length === 0}
            onChange={(event) => onSelectOption(event.currentTarget.value)}
          >
            {options.length === 0 && <option value="">{t('op.map.panel.noPackages')}</option>}
            {options.map((option) => (
              <option key={readString(option, 'packageDefinitionId')} value={readString(option, 'packageDefinitionId')}>
                {packageOptionLabel(option, currencyCode, t)}
              </option>
            ))}
          </select>
        </label>
        <div className="clients-package-preview" aria-label={t('op.players.actions.packagePreviewLabel')}>
          <span><strong>{t('op.players.actions.packagePrice')}</strong><b>{formatMinorUnits(priceMinorUnits, optionCurrency)}</b></span>
          <span><strong>{t('op.players.actions.packageMinutes')}</strong><b>{totalMinutes}</b></span>
          <span><strong>{t('op.players.actions.packageBonus')}</strong><b>{bonusMinutes}</b></span>
          <span><strong>{t('op.players.actions.packageExpiry')}</strong><b>{expiresDays > 0 ? t('op.players.actions.packageExpiryDays', { count: expiresDays }) : t('op.players.actions.packageNoExpiry')}</b></span>
          <span className={canAfford ? undefined : 'attention'}><strong>{t('op.pos.payment.methodDeposit')}</strong><b>{canAfford ? t('op.players.actions.depositOk') : t('op.players.actions.depositLow')}</b></span>
        </div>
        <button
          type="button"
          className="clients-primary-action"
          aria-label={t('op.players.actions.packageSelectLabel')}
          disabled={!canPurchase || options.length === 0 || !canAfford}
          onClick={onBuy}
        >
          <TimerReset size={15} aria-hidden="true" />{t('op.players.actions.buyPackageBtn')}
        </button>
      </div>
    </div>
  );
}
```

Примечание: `aria-label` кнопки покупки не критичен; тест ищет кнопку по тексту `Купить пакет` (`op.players.actions.buyPackageBtn` = «Купить пакет»). Можно убрать `aria-label`, если конфликтует с App.test (см. Task 9 — App.test ожидает `getByRole('button', { name: /Купить пакет/ })`, текст кнопки это покрывает). **Удалить `aria-label` с кнопки покупки**, чтобы accessible name был ровно «Купить пакет» (иконка `aria-hidden`).

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/PackagesSection.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS; `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/PackagesSection.tsx src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx
git commit -m "feat(operator-clients): PackagesSection — человеч. пакеты + инлайн-покупка"
```

---

### Task 6: `WalletSection.tsx` (+ тест) — баланс/долг + раздельные формы

Чистый компонент: крупно баланс/долг + ДВЕ раздельные инлайн-формы (Пополнить: сумма+причина; Погасить долг: сумма+причина, видна/активна только при debt>0). Ссылку «Ручная корректировка» НЕ добавляем (S2). Состояние полей и сабмит держит оркестратор; компонент — контролируемый.

**Files:**
- Create: `src/players/WalletSection.tsx`
- Create: `src/players/WalletSection.test.tsx`

**Interfaces:**
- Consumes: `formatMinorUnits` из `../operatorHelpers`; `useI18n` из `@afk4/i18n`.
- Produces:
```ts
function WalletSection(props: {
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
}): JSX.Element
```

- [ ] **Step 1: Написать падающий тест** `src/players/WalletSection.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletSection } from './WalletSection';

afterEach(cleanup);

const renderSection = (over: Partial<Parameters<typeof WalletSection>[0]> = {}) => {
  const onTopUp = mock(() => {});
  const onPayDebt = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <WalletSection
        balanceMinorUnits={46000}
        debtMinorUnits={0}
        currencyCode="TJS"
        topUpAmount="100.00"
        topUpReason="пополнение через кассу"
        debtAmount=""
        debtReason="оплата долга через кассу"
        canTopUp
        canPayDebt={false}
        onChangeTopUpAmount={() => {}}
        onChangeTopUpReason={() => {}}
        onChangeDebtAmount={() => {}}
        onChangeDebtReason={() => {}}
        onTopUp={onTopUp}
        onPayDebt={onPayDebt}
        {...over}
      />
    </I18nProvider>
  );
  return { onTopUp, onPayDebt };
};

describe('WalletSection', () => {
  it('renders balance and debt amounts', () => {
    renderSection({ debtMinorUnits: 3500 });
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина пополнения')).toBeInTheDocument();
  });

  it('keeps the debt form disabled when there is no debt', () => {
    renderSection({ debtMinorUnits: 0, canPayDebt: false });
    expect(screen.getByRole('button', { name: /Списать долг/ })).toBeDisabled();
  });

  it('fires onTopUp when the top-up button is clicked', () => {
    const { onTopUp } = renderSection();
    fireEvent.click(screen.getByRole('button', { name: /Пополнить депозит/ }));
    expect(onTopUp).toHaveBeenCalled();
  });

  it('shows the debt form fields and fires onPayDebt when debt is present', () => {
    const { onPayDebt } = renderSection({ debtMinorUnits: 3500, canPayDebt: true, debtAmount: '35.00' });
    expect(screen.getByLabelText('Сумма долга')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина долга')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Списать долг/ }));
    expect(onPayDebt).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/WalletSection.test.tsx`
Expected: FAIL — `Cannot find module './WalletSection'`.

- [ ] **Step 3: Создать `src/players/WalletSection.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { CircleDollarSign, ReceiptText } from 'lucide-react';
import { formatMinorUnits } from '../operatorHelpers';

// Кошелёк: крупно баланс/долг + две раздельные формы (Пополнить / Погасить долг).
// Долговая форма активна только при debt>0 (управляется canPayDebt из оркестратора).
// «Ручная корректировка» — S2, здесь не добавляем.
// feedback показывается глобально в оркестраторе — единый источник, здесь не дублируем.
export function WalletSection({
  balanceMinorUnits,
  debtMinorUnits,
  currencyCode,
  topUpAmount,
  topUpReason,
  debtAmount,
  debtReason,
  canTopUp,
  canPayDebt,
  onChangeTopUpAmount,
  onChangeTopUpReason,
  onChangeDebtAmount,
  onChangeDebtReason,
  onTopUp,
  onPayDebt
}: {
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;

  return (
    <div className="clients-wallet-section">
      <div className="clients-wallet-figures">
        <div className="clients-wallet-figure">
          <span>{t('op.players.wallet.balanceLabel')}</span>
          <strong>{formatMinorUnits(balanceMinorUnits, currencyCode)}</strong>
        </div>
        <div className={`clients-wallet-figure${hasDebt ? ' is-debt' : ''}`}>
          <span>{t('op.players.wallet.debtLabel')}</span>
          <strong>{formatMinorUnits(debtMinorUnits, currencyCode)}</strong>
        </div>
      </div>

      <form className="clients-wallet-form" onSubmit={(event) => { event.preventDefault(); onTopUp(); }}>
        <strong className="clients-section-title">{t('op.players.wallet.topUpTitle')}</strong>
        <label>{t('op.players.actions.topUpAmountLabel')}
          <input inputMode="decimal" value={topUpAmount} disabled={!canTopUp} onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)} />
        </label>
        <label>{t('op.players.actions.topUpReasonLabel')}
          <input value={topUpReason} disabled={!canTopUp} onChange={(event) => onChangeTopUpReason(event.currentTarget.value)} />
        </label>
        <button type="submit" className="clients-primary-action" disabled={!canTopUp}>
          <CircleDollarSign size={15} aria-hidden="true" />{t('op.players.actions.topUpBtn')}
        </button>
      </form>

      <form className={`clients-wallet-form${hasDebt ? '' : ' is-muted'}`} onSubmit={(event) => { event.preventDefault(); onPayDebt(); }}>
        <strong className="clients-section-title">{t('op.players.wallet.payDebtTitle')}</strong>
        <label>{t('op.players.actions.debtAmountLabel')}
          <input inputMode="decimal" value={debtAmount} disabled={!canPayDebt} onChange={(event) => onChangeDebtAmount(event.currentTarget.value)} />
        </label>
        <label>{t('op.players.actions.debtReasonLabel')}
          <input value={debtReason} disabled={!canPayDebt} onChange={(event) => onChangeDebtReason(event.currentTarget.value)} />
        </label>
        <button type="submit" className="clients-primary-action clients-debt-action" disabled={!canPayDebt}>
          <ReceiptText size={15} aria-hidden="true" />{t('op.players.actions.writeOffDebtBtn')}
        </button>
      </form>

    </div>
  );
}
```

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/WalletSection.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS; `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/WalletSection.tsx src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx
git commit -m "feat(operator-clients): WalletSection — баланс/долг + раздельные формы"
```

---

### Task 7: `ClientList.tsx` (+ тест) — поиск/сегменты/строки/skeleton/empty

Чистый компонент списка (master): поле поиска, сегмент-чипы (стабильные id), строки клиентов (статус-бейдж, имя, телефон/detail, баланс, индикатор долга), Skeleton при загрузке (через `useDeferredFlag` уровнем выше — компонент принимает `showSkeleton`), EmptyState при пустом результате.

**Files:**
- Create: `src/players/ClientList.tsx`
- Create: `src/players/ClientList.test.tsx`

**Interfaces:**
- Consumes: `PlayerClientItem` из `../operatorHelpers`; `ClientSegment`, `ClientSegmentId`, `playerStatusLabel` из `./playersModel`; `formatMinorUnits` из `../operatorHelpers`; `Skeleton`, `EmptyState` из `../operatorPrimitives`; `useI18n` из `@afk4/i18n`; `Search` из `lucide-react`.
- Produces:
```ts
function ClientList(props: {
  clients: PlayerClientItem[];
  segments: ClientSegment[];
  activeSegment: ClientSegmentId;
  selectedClientId: string | null;
  search: string;
  showSkeleton: boolean;
  emptyDescription: string;
  currencyCode: string;
  onSearchChange: (value: string) => void;
  onSelectSegment: (id: ClientSegmentId) => void;
  onSelectClient: (playerAccountId: string | null) => void;
}): JSX.Element
```

- [ ] **Step 1: Написать падающий тест** `src/players/ClientList.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientList } from './ClientList';
import type { PlayerClientItem } from '../operatorHelpers';
import type { ClientSegment } from './playersModel';

afterEach(cleanup);

const client = (over: Partial<PlayerClientItem>): PlayerClientItem => ({
  playerAccountId: 'p1', name: 'Madina S.', status: 'vip', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'vip', detail: '+992 90 555 22 11', phoneNumber: '+992 90 555 22 11',
  source: 'backend', ...over
});

const segments: ClientSegment[] = [
  { id: 'all', label: 'Все', count: 2 },
  { id: 'vip', label: 'VIP', count: 1 },
  { id: 'debt', label: 'Есть долг', count: 1 },
  { id: 'inactive', label: 'Неактивные', count: 0 }
];

const renderList = (over: Partial<Parameters<typeof ClientList>[0]> = {}) => {
  const onSearchChange = mock(() => {});
  const onSelectSegment = mock(() => {});
  const onSelectClient = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ClientList
        clients={[client({}), client({ playerAccountId: 'p2', name: 'Olim K.', status: 'debt', tone: 'debt', debtMinorUnits: 3500 })]}
        segments={segments}
        activeSegment="all"
        selectedClientId="p1"
        search=""
        showSkeleton={false}
        emptyDescription="По текущему поиску клиентов нет."
        currencyCode="TJS"
        onSearchChange={onSearchChange}
        onSelectSegment={onSelectSegment}
        onSelectClient={onSelectClient}
        {...over}
      />
    </I18nProvider>
  );
  return { onSearchChange, onSelectSegment, onSelectClient };
};

describe('ClientList', () => {
  it('renders client rows with debt indicator', () => {
    const { container } = renderList();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('Olim K.')).toBeInTheDocument();
    expect(container.querySelector('.client-row.debt')).not.toBeNull();
  });

  it('fires onSelectClient when a row is clicked', () => {
    const { onSelectClient } = renderList();
    fireEvent.click(screen.getByRole('button', { name: /Olim K\./ }));
    expect(onSelectClient).toHaveBeenCalledWith('p2');
  });

  it('fires onSelectSegment when a segment chip is clicked', () => {
    const { onSelectSegment } = renderList();
    fireEvent.click(screen.getByRole('button', { name: /VIP/ }));
    expect(onSelectSegment).toHaveBeenCalledWith('vip');
  });

  it('fires onSearchChange on input', () => {
    const { onSearchChange } = renderList();
    fireEvent.change(screen.getByPlaceholderText('Игрок, телефон, карта'), { target: { value: 'Mad' } });
    expect(onSearchChange).toHaveBeenCalledWith('Mad');
  });

  it('shows the EmptyState when there are no clients', () => {
    renderList({ clients: [] });
    expect(screen.getByText('Клиенты не найдены')).toBeInTheDocument();
  });

  it('shows skeleton rows when loading', () => {
    const { container } = renderList({ clients: [], showSkeleton: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/ClientList.test.tsx`
Expected: FAIL — `Cannot find module './ClientList'`.

- [ ] **Step 3: Создать `src/players/ClientList.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { Search, Users } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { formatMinorUnits } from '../operatorHelpers';
import { Skeleton, EmptyState } from '../operatorPrimitives';
import { playerStatusLabel, type ClientSegment, type ClientSegmentId } from './playersModel';

// Master-список клиентов: поиск + сегмент-чипы (стабильные id) + строки + skeleton/empty.
export function ClientList({
  clients,
  segments,
  activeSegment,
  selectedClientId,
  search,
  showSkeleton,
  emptyDescription,
  currencyCode,
  onSearchChange,
  onSelectSegment,
  onSelectClient
}: {
  clients: PlayerClientItem[];
  segments: ClientSegment[];
  activeSegment: ClientSegmentId;
  selectedClientId: string | null;
  search: string;
  showSkeleton: boolean;
  emptyDescription: string;
  currencyCode: string;
  onSearchChange: (value: string) => void;
  onSelectSegment: (id: ClientSegmentId) => void;
  onSelectClient: (playerAccountId: string | null) => void;
}) {
  const { t } = useI18n();

  return (
    <section className="clients-panel clients-list-panel">
      <header className="clients-panel-title">
        <span>{t('op.players.list.title')}</span>
        <strong>{t('op.players.list.subtitle')}</strong>
      </header>

      <label className="clients-search">
        <Search size={14} aria-hidden="true" />
        <input
          placeholder={t('op.players.list.searchPlaceholder')}
          value={search}
          onChange={(event) => onSearchChange(event.currentTarget.value)}
        />
      </label>

      <div className="clients-segment-chips" role="group" aria-label={t('op.players.segments.title')}>
        {segments.map((segment) => (
          <button
            key={segment.id}
            type="button"
            className={`clients-segment-chip${activeSegment === segment.id ? ' active' : ''}`}
            onClick={() => onSelectSegment(segment.id)}
          >
            {segment.label}
            <b>{segment.count}</b>
          </button>
        ))}
      </div>

      <div className="clients-list">
        {showSkeleton ? (
          <div className="clients-list-skeleton" aria-hidden="true">
            {Array.from({ length: 5 }).map((_, index) => (
              <Skeleton key={index} className="client-row-skel" />
            ))}
          </div>
        ) : clients.length === 0 ? (
          <EmptyState
            icon={<Users size={20} aria-hidden="true" />}
            title={t('op.players.list.emptyTitle')}
            description={emptyDescription}
          />
        ) : (
          clients.map((client) => (
            <button
              key={client.playerAccountId ?? client.name}
              type="button"
              className={`client-row ${client.tone}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
              onClick={() => onSelectClient(client.playerAccountId ?? null)}
            >
              <span>{playerStatusLabel(client.status, t)}</span>
              <div>
                <strong>{client.name}</strong>
                <em>{client.detail}</em>
              </div>
              <b>{formatMinorUnits(client.balanceMinorUnits, currencyCode)}</b>
              {client.debtMinorUnits > 0 && <small className="client-row-debt">{formatMinorUnits(client.debtMinorUnits, currencyCode)}</small>}
            </button>
          ))
        )}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/ClientList.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS; `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/ClientList.tsx src/AFK4.Operator.App.Web/src/players/ClientList.test.tsx
git commit -m "feat(operator-clients): ClientList — master-список с поиском/сегментами/skeleton"
```

---

### Task 8: `ClientDetail.tsx` + `NewClientModal.tsx` (+ тесты) — карточка с табами и модалка создания

`ClientDetail`: пустое состояние (EmptyState) при отсутствии выбранного; иначе карточка — шапка (аватар-инициалы, имя, телефон, статус-бейдж, источник `dataSourceLabel`) + чипы Баланс/Долг(красный при >0)/Пакеты + кнопка «Бронь» (под `manageReservations`); таб-стрип Кошелёк/Пакеты/История; контент активного таба (делегирует в Wallet/Packages/History). `NewClientModal`: через `PanelModal` — поля имя/телефон + кнопка «Создать».

**Files:**
- Create: `src/players/ClientDetail.tsx`
- Create: `src/players/ClientDetail.test.tsx`
- Create: `src/players/NewClientModal.tsx`
- Create: `src/players/NewClientModal.test.tsx`

**Interfaces:**
- `ClientDetail` Consumes: `PlayerClientItem`, `dataSourceLabel` из `../operatorHelpers`; `PlayerPackageDto`, `PackageOptionDto`, `LedgerEntryDto`, `WalletSummaryDto` из `../operatorApiClients`; `playerStatusLabel` из `./playersModel`; `WalletSection`/`PackagesSection`/`HistorySection`; `useI18n`.
- `ClientDetail` Produces:
```ts
type ClientDetailTab = 'wallet' | 'packages' | 'history';
function ClientDetail(props: {
  client: PlayerClientItem | null;
  activeTab: ClientDetailTab;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  recentEntries: LedgerEntryDto[];
  selectedPackageDefinitionId: string;
  // wallet form state
  topUpAmount: string; topUpReason: string; debtAmount: string; debtReason: string;
  // capability gates
  canTopUp: boolean; canPayDebt: boolean; canPurchase: boolean; canCreateReservation: boolean;
  // handlers
  onSelectTab: (tab: ClientDetailTab) => void;
  onChangeTopUpAmount: (v: string) => void; onChangeTopUpReason: (v: string) => void;
  onChangeDebtAmount: (v: string) => void; onChangeDebtReason: (v: string) => void;
  onTopUp: () => void; onPayDebt: () => void;
  onSelectOption: (packageDefinitionId: string) => void; onBuy: () => void;
  onCreateReservation: () => void;
}): JSX.Element
```
- `NewClientModal` Produces:
```ts
function NewClientModal(props: {
  name: string; phone: string;
  onChangeName: (v: string) => void; onChangePhone: (v: string) => void;
  onClose: () => void; onSubmit: () => void;
}): JSX.Element
```
Экспортируется также `type ClientDetailTab` для оркестратора.

- [ ] **Step 1: Написать падающий тест** `src/players/NewClientModal.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { NewClientModal } from './NewClientModal';

afterEach(cleanup);

const renderModal = () => {
  const onChangeName = mock(() => {});
  const onChangePhone = mock(() => {});
  const onClose = mock(() => {});
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <NewClientModal name="" phone="" onChangeName={onChangeName} onChangePhone={onChangePhone} onClose={onClose} onSubmit={onSubmit} />
    </I18nProvider>
  );
  return { onChangeName, onChangePhone, onClose, onSubmit };
};

describe('NewClientModal', () => {
  it('renders the name and phone fields inside a dialog', () => {
    renderModal();
    expect(screen.getByRole('dialog', { name: 'Новый клиент' })).toBeInTheDocument();
    expect(screen.getByLabelText('Имя нового клиента')).toBeInTheDocument();
    expect(screen.getByLabelText('Телефон нового клиента')).toBeInTheDocument();
  });

  it('fires onSubmit when create button is clicked', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Создать/ }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('fires onChangeName when typing the name', () => {
    const { onChangeName } = renderModal();
    fireEvent.change(screen.getByLabelText('Имя нового клиента'), { target: { value: 'Zarina N.' } });
    expect(onChangeName).toHaveBeenCalledWith('Zarina N.');
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/NewClientModal.test.tsx`
Expected: FAIL — `Cannot find module './NewClientModal'`.

- [ ] **Step 3: Создать `src/players/NewClientModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { UserRoundPlus } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Создание клиента через готовую модалку (вместо вечно-раскрытой формы). Реальное действие
// createPlayer держит оркестратор; здесь — контролируемые поля + сабмит.
export function NewClientModal({
  name,
  phone,
  onChangeName,
  onChangePhone,
  onClose,
  onSubmit
}: {
  name: string;
  phone: string;
  onChangeName: (value: string) => void;
  onChangePhone: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
}) {
  const { t } = useI18n();
  return (
    <PanelModal title={t('op.players.newClient.title')} subtitle={t('op.players.newClient.subtitle')} onClose={onClose}>
      <form className="clients-new-form" onSubmit={(event) => { event.preventDefault(); onSubmit(); }}>
        <label>{t('op.players.actions.newNameLabel')}
          <input value={name} autoFocus onChange={(event) => onChangeName(event.currentTarget.value)} />
        </label>
        <label>{t('op.players.actions.newPhoneLabel')}
          <input value={phone} inputMode="tel" onChange={(event) => onChangePhone(event.currentTarget.value)} />
        </label>
        <button type="submit" className="clients-primary-action">
          <UserRoundPlus size={15} aria-hidden="true" />{t('op.players.newClient.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Написать падающий тест** `src/players/ClientDetail.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientDetail } from './ClientDetail';
import type { PlayerClientItem } from '../operatorHelpers';

afterEach(cleanup);

const client: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Madina S.', status: 'vip', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'vip', detail: '', phoneNumber: '+992 90 555 22 11', source: 'backend'
};

const baseProps = {
  client,
  activeTab: 'wallet' as const,
  balanceMinorUnits: 46000,
  debtMinorUnits: 0,
  packageCount: 1,
  currencyCode: 'TJS',
  packages: [],
  options: [],
  recentEntries: [],
  selectedPackageDefinitionId: '',
  topUpAmount: '100.00', topUpReason: 'пополнение через кассу',
  debtAmount: '', debtReason: 'оплата долга через кассу',
  canTopUp: true, canPayDebt: false, canPurchase: true, canCreateReservation: true,
  onSelectTab: () => {}, onChangeTopUpAmount: () => {}, onChangeTopUpReason: () => {},
  onChangeDebtAmount: () => {}, onChangeDebtReason: () => {}, onTopUp: () => {}, onPayDebt: () => {},
  onSelectOption: () => {}, onBuy: () => {}, onCreateReservation: () => {}
};

const renderDetail = (over: Partial<typeof baseProps> = {}) =>
  render(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} {...over} /></I18nProvider>);

describe('ClientDetail', () => {
  it('shows the empty state when no client is selected', () => {
    renderDetail({ client: null });
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
  });

  it('renders the header, chips and reservation button for a selected client', () => {
    renderDetail();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('+992 90 555 22 11', { exact: false })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Бронь/ })).toBeInTheDocument();
  });

  it('switches tab content when a tab is clicked', () => {
    const onSelectTab = mock(() => {});
    renderDetail({ onSelectTab });
    fireEvent.click(screen.getByRole('tab', { name: 'История' }));
    expect(onSelectTab).toHaveBeenCalledWith('history');
  });

  it('renders the wallet section on the wallet tab', () => {
    renderDetail({ activeTab: 'wallet' });
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
  });

  it('renders the history section on the history tab', () => {
    renderDetail({ activeTab: 'history', recentEntries: [] });
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('fires onCreateReservation when the reservation button is clicked', () => {
    const onCreateReservation = mock(() => {});
    renderDetail({ onCreateReservation });
    fireEvent.click(screen.getByRole('button', { name: /Бронь/ }));
    expect(onCreateReservation).toHaveBeenCalled();
  });
});
```

- [ ] **Step 5: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/ClientDetail.test.tsx`
Expected: FAIL — `Cannot find module './ClientDetail'`.

- [ ] **Step 6: Создать `src/players/ClientDetail.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { dataSourceLabel, formatMinorUnits } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { playerStatusLabel } from './playersModel';
import { WalletSection } from './WalletSection';
import { PackagesSection } from './PackagesSection';
import { HistorySection } from './HistorySection';

export type ClientDetailTab = 'wallet' | 'packages' | 'history';

function initials(name: string): string {
  return name.split(' ').map((part) => part[0]).join('').slice(0, 2).toUpperCase() || '—';
}

export function ClientDetail(props: {
  client: PlayerClientItem | null;
  activeTab: ClientDetailTab;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  recentEntries: LedgerEntryDto[];
  selectedPackageDefinitionId: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canPurchase: boolean;
  canCreateReservation: boolean;
  onSelectTab: (tab: ClientDetailTab) => void;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
  onCreateReservation: () => void;
}) {
  const { t } = useI18n();
  const { client } = props;

  if (client === null) {
    return (
      <section className="clients-panel clients-detail-panel">
        <EmptyState
          title={t('op.players.profile.empty')}
          description={t('op.players.profile.emptyNote')}
        />
      </section>
    );
  }

  const hasDebt = props.debtMinorUnits > 0;
  const tabs: Array<{ id: ClientDetailTab; label: string }> = [
    { id: 'wallet', label: t('op.players.tabs.wallet') },
    { id: 'packages', label: t('op.players.tabs.packages') },
    { id: 'history', label: t('op.players.tabs.history') }
  ];

  return (
    <section className="clients-panel clients-detail-panel">
      <header className="client-detail-head">
        <div className="client-avatar">{initials(client.name)}</div>
        <div className="client-detail-ident">
          <span className="client-detail-status">{playerStatusLabel(client.status, t)}</span>
          <strong>{client.name}</strong>
          <em>{client.phoneNumber || t('op.pos.cart.clientNoPhone')} · {dataSourceLabel(client.source, t)}</em>
        </div>
        <button
          type="button"
          className="client-detail-reservation"
          disabled={!props.canCreateReservation}
          onClick={props.onCreateReservation}
        >
          <CalendarClock size={15} aria-hidden="true" />{t('op.players.detail.reservationBtn')}
        </button>
      </header>

      <div className="client-detail-chips">
        <div className="client-chip">
          <span>{t('op.players.chip.balance')}</span>
          <strong>{formatMinorUnits(props.balanceMinorUnits, props.currencyCode)}</strong>
        </div>
        <div className={`client-chip${hasDebt ? ' is-debt' : ''}`}>
          <span>{t('op.players.chip.debt')}</span>
          <strong>{formatMinorUnits(props.debtMinorUnits, props.currencyCode)}</strong>
        </div>
        <div className="client-chip">
          <span>{t('op.players.chip.packages')}</span>
          <strong>{props.packageCount}</strong>
        </div>
      </div>

      <div className="client-detail-tabs" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={props.activeTab === tab.id}
            className={`client-detail-tab${props.activeTab === tab.id ? ' active' : ''}`}
            onClick={() => props.onSelectTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="client-detail-content">
        {props.activeTab === 'wallet' && (
          <WalletSection
            balanceMinorUnits={props.balanceMinorUnits}
            debtMinorUnits={props.debtMinorUnits}
            currencyCode={props.currencyCode}
            topUpAmount={props.topUpAmount}
            topUpReason={props.topUpReason}
            debtAmount={props.debtAmount}
            debtReason={props.debtReason}
            canTopUp={props.canTopUp}
            canPayDebt={props.canPayDebt}
            onChangeTopUpAmount={props.onChangeTopUpAmount}
            onChangeTopUpReason={props.onChangeTopUpReason}
            onChangeDebtAmount={props.onChangeDebtAmount}
            onChangeDebtReason={props.onChangeDebtReason}
            onTopUp={props.onTopUp}
            onPayDebt={props.onPayDebt}
          />
        )}
        {props.activeTab === 'packages' && (
          <PackagesSection
            packages={props.packages}
            options={props.options}
            selectedPackageDefinitionId={props.selectedPackageDefinitionId}
            balanceMinorUnits={props.balanceMinorUnits}
            currencyCode={props.currencyCode}
            canPurchase={props.canPurchase}
            onSelectOption={props.onSelectOption}
            onBuy={props.onBuy}
          />
        )}
        {props.activeTab === 'history' && (
          <HistorySection entries={props.recentEntries} currencyCode={props.currencyCode} />
        )}
      </div>
    </section>
  );
}
```

`feedback` показывается глобально в оркестраторе (Task 9), секции его не рендерят — единый источник (#32).

- [ ] **Step 7: Прогнать тесты Task 8 + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/NewClientModal.test.tsx src/players/ClientDetail.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: оба теста PASS; `bun run build` чисто.

- [ ] **Step 8: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx src/AFK4.Operator.App.Web/src/players/NewClientModal.tsx src/AFK4.Operator.App.Web/src/players/NewClientModal.test.tsx
git commit -m "feat(operator-clients): ClientDetail (табы/чипы/шапка) + NewClientModal"
```

---

### Task 9: Переписать `BackendPlayersWorkspace.tsx` в тонкий оркестратор + обновить App.test

Переписать монолит: сохранить весь state/effects/actions (поиск-дебаунс, wallet-loader, runClientAction), добавить `activeTab` + стабильные сегменты (`ClientSegmentId` вместо локализованной строки), рендерить master-detail через `ClientList` + `ClientDetail`, создание клиента — через `NewClientModal` по кнопке «+ Новый клиент». Обновить players-регион `App.test.tsx` под новый DOM, сохранив покрытие поведения. Удалить мёртвые i18n-ключи сегментов.

**Files:**
- Modify: `src/BackendPlayersWorkspace.tsx` (полная перепись рендера; логика сохранена)
- Modify: `src/App.test.tsx` (players-регион: head-тест ~862-872, action-тесты ~1840-2005, empty-тест ~1591-1613)
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.ts` (удаление мёртвых ключей)

**Interfaces:**
- Consumes: всё из `./players/*` (ClientList, ClientDetail+ClientDetailTab, NewClientModal, playersModel: `buildClientSegments`/`matchesSegment`/`ClientSegmentId`); готовые хелперы/примитивы как сейчас; `useDeferredFlag`.
- Produces: `BackendPlayersWorkspace` с прежней сигнатурой `({ currencyCode, backend })`.

- [ ] **Step 1: Переписать `src/BackendPlayersWorkspace.tsx`**

Сохранить (скопировать как есть из текущего файла, строки 33-179, 203-350): объявления state (КРОМЕ `activeSegment` — заменить тип), оба `useEffect` (поиск-дебаунс 180мс и wallet-loader), производные `selectedClient`/`balance`/`debt`/`recentEntries`/`selectedClientPackageCount`/`selectedPackageOption`/…/`canTopUpWallet`/`canPayDebt`/`canPurchasePackage`/`canCreatePlayer`/`canCreateClientReservation`, `requireSelectedBackendClient`, `runClientAction` (все ветки `topUp`/`writeOffDebt`/`buyPackage`/`booking`/`newCard`). Заменить:

Импорты (заменить строку 28 и блок примитивов):

```ts
import { fixturePlayers, playerPackageLabel, playerStatusLabel, projectPlayerClient, buildClientSegments, matchesSegment, type PlayerClientItem, type ClientSegmentId } from './players/playersModel';
import { FeedbackNotice, StateFlag } from './operatorPrimitives';
import { useDeferredFlag } from './useDeferredFlag';
import { ClientList } from './players/ClientList';
import { ClientDetail, type ClientDetailTab } from './players/ClientDetail';
import { NewClientModal } from './players/NewClientModal';
```

State: заменить `activeSegment` и добавить `activeTab` + `newClientOpen`:

```ts
  const [activeSegment, setActiveSegment] = useState<ClientSegmentId>('all');
  const [activeTab, setActiveTab] = useState<ClientDetailTab>('wallet');
  const [newClientOpen, setNewClientOpen] = useState(false);
```

Заменить блок `segmentAll..segmentSleeping` + `visibleClients` (строки 140-154) на:

```ts
  const segments = buildClientSegments(clients, t);
  const visibleClients = clients.filter((client) => {
    const searchMatches = `${client.name} ${playerStatusLabel(client.status, t)} ${client.detail} ${client.last}`
      .toLowerCase()
      .includes(clientSearch.trim().toLowerCase());
    return matchesSegment(client, activeSegment) && searchMatches;
  });
```

Заменить блок `playerActions`/`segments`-массивов (строки 352-431) — удалить целиком (больше не нужны: действия теперь живут в секциях/детали). Оставить `formatTypedAmount`? Нет — он использовался только для `playerActions.detail`; удалить.

Добавить скелетон-флаг и обёртку createPlayer для модалки:

```ts
  const showSkeleton = useDeferredFlag(loadStatus === 'loading');
  const emptyDescription = loadStatus === 'backend' ? t('op.players.list.emptyBackend') : t('op.players.list.emptyConnect');

  const submitNewClient = async () => {
    await runClientAction('newCard', t('op.pos.cart.newCardLabel'));
    setNewClientOpen(false);
  };
```

(Примечание: `runClientAction('newCard', …)` уже использует `newPlayerName`/`newPlayerPhone` из state и чистит их при успехе; закрываем модалку после. Если действие упало, feedback покажет ошибку — модалку всё равно закрываем, ошибка видна в глобальном FeedbackNotice. Альтернатива: закрывать только при успехе — но runClientAction не возвращает результат; для S1 закрываем всегда, ошибка остаётся в feedback. Это приемлемо.)

Заменить весь `return (…)` (строки 433-642) на:

```tsx
  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>{t('op.players.title')}</span>
          <h1>{t('op.players.heading')}</h1>
        </div>
        <div className="screen-actions clients-head-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.pos.platformConnected'), t)}</span>
          <StateFlag label={t('op.players.strip.clients')} value={String(clients.length)} />
          <StateFlag label={t('op.players.strip.platform')} value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
          <button type="button" className="clients-new-client-btn" disabled={!canCreatePlayer} onClick={() => setNewClientOpen(true)}>
            <UserRoundPlus size={15} aria-hidden="true" />{t('op.players.newClient.openBtn')}
          </button>
        </div>
      </section>

      <FeedbackNotice feedback={feedback} />

      <section className="clients-layout">
        <ClientList
          clients={visibleClients}
          segments={segments}
          activeSegment={activeSegment}
          selectedClientId={selectedClient?.playerAccountId ?? null}
          search={clientSearch}
          showSkeleton={showSkeleton}
          emptyDescription={emptyDescription}
          currencyCode={currencyCode}
          onSearchChange={setClientSearch}
          onSelectSegment={setActiveSegment}
          onSelectClient={setSelectedClientId}
        />

        <ClientDetail
          client={selectedClient}
          activeTab={activeTab}
          balanceMinorUnits={balance}
          debtMinorUnits={debt}
          packageCount={selectedClientPackageCount}
          currencyCode={currencyCode}
          packages={selectedClientPackages}
          options={packageOptions}
          recentEntries={recentEntries}
          selectedPackageDefinitionId={selectedPackageDefinitionId}
          topUpAmount={walletTopUpAmount}
          topUpReason={walletTopUpReason}
          debtAmount={debtPaymentAmount}
          debtReason={debtPaymentReason}
          canTopUp={canTopUpWallet}
          canPayDebt={canPayDebt}
          canPurchase={canPurchasePackage}
          canCreateReservation={canCreateClientReservation}
          onSelectTab={setActiveTab}
          onChangeTopUpAmount={setWalletTopUpAmount}
          onChangeTopUpReason={setWalletTopUpReason}
          onChangeDebtAmount={setDebtPaymentAmount}
          onChangeDebtReason={setDebtPaymentReason}
          onTopUp={() => runClientAction('topUp', t('op.players.actions.topUpBtn'))}
          onPayDebt={() => runClientAction('writeOffDebt', t('op.players.actions.writeOffDebtBtn'))}
          onSelectOption={setSelectedPackageDefinitionId}
          onBuy={() => runClientAction('buyPackage', t('op.players.actions.buyPackageBtn'))}
          onCreateReservation={() => runClientAction('booking', t('op.players.actions.bookingBtn'))}
        />
      </section>

      {newClientOpen && (
        <NewClientModal
          name={newPlayerName}
          phone={newPlayerPhone}
          onChangeName={setNewPlayerName}
          onChangePhone={setNewPlayerPhone}
          onClose={() => setNewClientOpen(false)}
          onSubmit={() => void submitNewClient()}
        />
      )}
    </main>
  );
```

Важно про типы:
- `recentEntries` сейчас `readArray(walletSummary, 'recentEntries')` (тип `unknown[]`). `ClientDetail` ожидает `LedgerEntryDto[]`. Заменить производную на типизированную: `const recentEntries = walletSummary?.recentEntries ?? [];` (через DTO `WalletSummaryDto.recentEntries: LedgerEntryDto[]`). Удалить хак-приведение в JSX выше — просто `recentEntries={recentEntries}`. Обнови строку производной (была 157):
  ```ts
  const recentEntries = walletSummary?.recentEntries ?? [];
  ```
- Удалить теперь неиспользуемые импорты: `CalendarClock, CircleDollarSign, ReceiptText, Search, TimerReset` из lucide (остаётся только `UserRoundPlus`); `dataSourceLabel`, `formatMinorUnits`, `formatMoney`, `formatTime`, `packageOptionLabel`, `playerPackageLabel`, `readArray`, `readMoney` — если больше нигде в файле не используются (проверить tsc). `formatMoney`/`formatTime` ушли в секции; `readMoney`/`readArray` — производные balance/debt теперь можно оставить через `readMoney` (они валидны), либо переписать на `walletSummary?.walletBalance.minorUnits ?? …`. **Рекомендация:** оставить `readMoney`-производные как есть (строки 155-156), убрать только реально неиспользуемые импорты — финальный `bun run build` покажет точный список лишних.

- [ ] **Step 2: Прогнать тесты секций (регресс) + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/ && /home/fedya/.bun/bin/bun run build`
Expected: все `src/players/*.test.*` PASS; `bun run build` без ошибок (убрать оставшиеся неиспользуемые импорты, если tsc/`noUnusedLocals` ругается).

- [ ] **Step 3: Обновить players-регион в `src/App.test.tsx`**

Изменения под новый DOM (СОХРАНИТЬ покрытие):

(a) Head-тест (~строки 862-872). Теперь нет панелей «Список клиентов»/«Карточка клиента»/«Операции»/«История клиента» как заголовков grid-панелей; вместо них — `ClientList` header (`op.players.list.title` = «Список клиентов» остаётся), `ClientDetail` (нет заголовка-панели — карточка), табы. Кнопка «Пополнить депозит» теперь внутри `WalletSection` (таб Кошелёк — он активен по умолчанию). Заменить блок на:

```ts
    fireEvent.click(screen.getByTitle('Клиенты'));
    const clientsHead = screen.getByRole('heading', { name: /Клиенты/ }).closest('.screen-head');
    expect(clientsHead).toBeInTheDocument();
    // глобальные метрики в шапке — но НЕ per-client сегменты/числа
    expect(clientsHead).not.toHaveTextContent('Долг');
    expect(screen.getByText('Список клиентов')).toBeInTheDocument();
    // master-detail: табы карточки
    expect(screen.getByRole('tab', { name: 'Кошелёк' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Пакеты' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'История' })).toBeInTheDocument();
    // кнопка действия на активном табе (Кошелёк)
    expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeInTheDocument();
```

(Примечание: `clientsHead` теперь содержит `StateFlag` «Клиенты»/«Сервер» — проверка `not.toHaveTextContent('Долг')` остаётся валидной, т.к. чип «Долг» — в карточке, не в шапке. Проверки `not.toHaveTextContent('Все'/'VIP')` УДАЛИТЬ — сегменты теперь в списке, не в шапке; но они и раньше не были в шапке. Сохранить смысл: per-client/сегментные числа не в шапке.)

(b) Empty-тест (~1591-1613). EmptyState теперь рендерит `op.players.list.emptyTitle` = «Клиенты не найдены» и `description` = `op.players.list.emptyBackend` = «По текущему поиску клиентов нет.». Профиль-пустышка → `ClientDetail` EmptyState: title `op.players.profile.empty` = «Нет выбранного клиента», description `op.players.profile.emptyNote` = «Пустой ответ сервера не подменяется локальной карточкой». Эти тексты СОХРАНЕНЫ в каталоге. Тест почти не меняется; проверить, что строки совпадают:

```ts
    expect(await screen.findByText('Клиенты не найдены')).toBeInTheDocument();
    expect(await screen.findByText('По текущему поиску клиентов нет.')).toBeInTheDocument();
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
    expect(screen.getByText('Пустой ответ сервера не подменяется локальной карточкой')).toBeInTheDocument();
    expect(screen.queryByText('Madina S.')).not.toBeInTheDocument();
```

(Строку `expect(...'Сервер вернул пустой список...')` — удалить, такого ключа в новой структуре нет; она и в текущем коде проверяет ОТСУТСТВИЕ — можно оставить `queryByText(...).not.toBeInTheDocument()` если текст не используется. Безопаснее удалить эту строку.)

(c) Top-up тест (~1923-1949). Поля «Сумма пополнения»/«Причина пополнения» теперь в `WalletSection` на активном табе Кошелёк (открыт по умолчанию). Кнопка «Пополнить депозит». Селекторы те же — тест должен пройти без правок. Убедиться, что после `fireEvent.click(screen.getByTitle('Клиенты'))` таб Кошелёк активен (да, дефолт). Тест НЕ менять.

(d) Pay-debt тест (~1951-1979). Нужно выбрать клиента «Olim K.» (с долгом), затем поля «Сумма долга»/«Причина долга». Они теперь на табе Кошелёк (формы Пополнить+Погасить вместе на табе Кошелёк). Селекторы те же. После выбора Olim K. долг>0 → форма долга активна. Тест НЕ менять (поля и кнопка «Списать долг» те же).

(e) Buy-package тест (~1881-1906). Селект «Пакет для покупки» и кнопка «Купить пакет» теперь на табе Пакеты. Тест сейчас НЕ переключает таб. **Добавить переключение на таб Пакеты** перед взаимодействием:

```ts
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect(await screen.findByTitle(/Сервер на связи/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Купить пакет/ })).toBeEnabled());
    fireEvent.change(await screen.findByLabelText('Пакет для покупки'), { target: { value: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd' } });
    const purchasePackageButton = await screen.findByRole('button', { name: /Купить пакет/ });
```
(остальное тела теста без изменений).

(f) Active-packages тест (~1908-1921) — проверяет «180 мин» на профиле. Теперь пакеты на табе Пакеты, и рендер другой: `op.players.packages.includedMinutes` = «{minutes} мин в пакете» (10800/60 = 180). **Переключить на таб Пакеты** и проверить новый формат:

```ts
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect(await screen.findByTitle(/Сервер на связи/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    expect(await screen.findByText(/180 мин в пакете/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/packages') &&
      init?.method !== 'POST')).toBe(true);
```
(Примечание: App.test `createPlayerPackage` имеет `remainingIncludedSeconds: 10800` → 180; `remainingBonusSeconds: 0` → бонус-строки нет. ОК.)

(g) Create-player тест (~1981-2005). Форма создания теперь в `NewClientModal` по кнопке «Новый клиент». Раньше поля «Имя нового клиента»/«Телефон нового клиента» были всегда видны + кнопка «Новая карта». Теперь: открыть модалку, заполнить, нажать «Создать». **Переписать взаимодействие:**

```ts
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect(await screen.findByTitle(/Сервер на связи/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Новый клиент/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Новый клиент' });
    fireEvent.change(within(dialog).getByLabelText('Имя нового клиента'), { target: { value: 'Zarina N.' } });
    fireEvent.change(within(dialog).getByLabelText('Телефон нового клиента'), { target: { value: '+992 90 777 88 99' } });
    fireEvent.click(within(dialog).getByRole('button', { name: /Создать/ }));

    expect(await screen.findByText('Новая карта: подтверждено')).toBeInTheDocument();
```
(остальное — проверка POST `/players` — без изменений; `runClientAction('newCard', t('op.pos.cart.newCardLabel'))` шлёт тот же POST с теми же полями и idempotencyKey `player-create-*`, а feedback label = «Новая карта», т.к. передаём `t('op.pos.cart.newCardLabel')`. Убедиться, что `within` импортирован в App.test — он уже используется (строки 1824+).)

(h) Reservation-from-card тест (~1840-1879). Кнопка теперь «Бронь» (`op.players.detail.reservationBtn`) в шапке карточки, не «Создать бронь». **Заменить селектор кнопки** с `/Создать бронь/` на `/Бронь/`:

```ts
    const createReservationButton = screen.getByRole('button', { name: 'Бронь' });
```
(и в disabled-тесте ~1867-1879 тоже `name: 'Бронь'`). Тело POST-проверки без изменений (`runClientAction('booking', …)` шлёт тот же `reservations.create`).

(i) Top-up тест #2 (~1923) уже покрыт (c). Проверить, что нет других ссылок на удалённые элементы: `grep` «Новая карта» (как текст кнопки — теперь только feedback label), «Создать бронь» (кнопка → «Бронь»), сегменты-панель.

- [ ] **Step 4: Удалить мёртвые i18n-ключи сегментов + регенерить**

Теперь, когда оркестратор не ссылается на `op.players.segments.new`/`sleeping`/`fromSearch`/`clients`/`strip.deposit`/`strip.entries`/`strip.label`, проверить grep и удалить неиспользуемые:

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && grep -rn "segments.new\|segments.sleeping\|segments.fromSearch\|segments.clients\|strip.deposit\|strip.entries\|strip.label\|strip.label\|op.players.actions.title\|op.players.actions.subtitle\|op.players.profile.title\|op.players.profile.subtitle\|op.players.profile.emptyHint\|op.players.history.title\|op.players.history.subtitle" src/
```

Для КАЖДОГО ключа без ссылок — удалить из `locales/{ru,en,tg}.json` (все три). Кандидаты на удаление (подтвердить grep'ом 0 ссылок): `op.players.segments.new`, `op.players.segments.sleeping`, `op.players.segments.fromSearch`, `op.players.segments.clients`, `op.players.segments.subtitle`, `op.players.segments.title` (если чипы не используют title — но `ClientList` использует `op.players.segments.title` для aria-label group → ОСТАВИТЬ), `op.players.strip.deposit`, `op.players.strip.entries`, `op.players.strip.label`, `op.players.profile.subtitle`, `op.players.profile.emptyHint`, `op.players.profile.title`, `op.players.history.subtitle`. **Удалять ТОЛЬКО подтверждённые grep'ом нулевые** — не вслепую. Затем:

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen`

- [ ] **Step 5: Полный прогон App.test (ОТДЕЛЬНО) + остальные тесты + сборка**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Expected: весь players-регион зелёный.

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test && cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: i18n parity/voice/tg-guard зелёные (parity не упадёт после удаления — ключи удалены во всех трёх локалях синхронно); `bun run build` без ошибок (нет ссылок на удалённые ключи; `noUnusedLocals` чист).

- [ ] **Step 6: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/App.test.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(operator-clients): тонкий оркестратор master-detail + актуализация App.test"
```

---

### Task 10: CSS `styles/12-players.css` — master-detail (класс-контракт + baseline)

Переписать CSS под новую структуру: layout `clients-layout` (список слева / карточка справа), список с сегмент-чипами/строками/skeleton/empty, карточка с шапкой/чипами/таб-стрипом/контентом, секции Кошелёк/Пакеты/История (строки истории с цветом суммы, строки пакетов, формы). Следовать визуальному языку оператора (токены/паттерны из `10-booking.css`, акцент синий `var(--accent)`, тёмная тема). CSS в jsdom не тестируется — критерий приёмки: сборка зелёная + визуальная проверка в превью.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (полная перепись)

**Класс-контракт (имена, которые рендерят компоненты Tasks 4-9 — CSS обязан их покрыть):**
- Экран/шапка: `.clients-screen`, `.clients-head`, `.clients-head-actions`, `.clients-new-client-btn`, `.map-load-state` (наследуется из map-css — не дублировать), `.state-flag` (примитив — не дублировать).
- Layout: `.clients-layout` (grid: список + карточка), `.clients-panel`, `.clients-list-panel`, `.clients-detail-panel`.
- Список: `.clients-panel-title`, `.clients-search`, `.clients-segment-chips`, `.clients-segment-chip` (+`.active`), `.clients-list`, `.clients-list-skeleton`, `.client-row-skel`, `.client-row` (+тоны `.vip/.active/.debt/.regular`, +`.selected`), `.client-row-debt`.
- Карточка: `.client-detail-head`, `.client-avatar`, `.client-detail-ident`, `.client-detail-status`, `.client-detail-reservation`, `.client-detail-chips`, `.client-chip` (+`.is-debt`), `.client-detail-tabs`, `.client-detail-tab` (+`.active`), `.client-detail-content`.
- Кошелёк: `.clients-wallet-section`, `.clients-wallet-figures`, `.clients-wallet-figure` (+`.is-debt`), `.clients-wallet-form` (+`.is-muted`), `.clients-section-title`, `.clients-primary-action` (+`.clients-debt-action`).
- Пакеты: `.clients-packages-section`, `.client-package-list`, `.client-package-row` (+`.is-expired`), `.client-package-bonus`, `.clients-package-buy`, `.clients-package-select`, `.clients-package-preview` (+`span.attention`).
- История: `.clients-history-list`, `.client-history-row` (+`.is-credit`/`.is-debit`), `.client-history-time`, `.client-history-body`, `.client-history-detail`, `.client-history-reversal`, `.client-history-amount`.
- Модалка: `.clients-new-form` (внутри `.panel-modal` — backdrop/modal стили из PanelModal-css, не дублировать).
- EmptyState/Skeleton — примитивы (`.empty-state`, `.skeleton-block`), их базовый CSS уже есть; локальные подгонки опциональны.

**Визуальные требования:**
- Layout `.clients-layout` — grid `minmax(0, 360px) minmax(0, 1fr)` (список фикс-ширины, карточка тянется), flex/grid-высота на остаток экрана (как `.booking-layout` — `flex:1; min-height:0`), без хрупкого `calc(100vh − …)`.
- Тёмная тема: фоны через `var(--surface-*)`, текст `var(--text-*)`, рамки `var(--border-*)`, акцент `var(--accent)`/`var(--accent-rgb)` (синий оператора). Долг — `var(--danger)`/`var(--danger-text)`.
- История: `.is-credit .client-history-amount` зелёный (`var(--success-text)`), `.is-debit .client-history-amount` красный (`var(--danger-text)`); `.client-history-reversal` — пилюля приглушённая.
- Таб-стрип: активный таб — нижний кант акцентом, hover/focus-visible как у booking-кнопок.
- Hover/focus-visible/active на всех интерактивных (`.client-row`, `.clients-segment-chip`, `.client-detail-tab`, `.clients-primary-action`, `.clients-new-client-btn`, `.client-detail-reservation`) — зеркалить паттерны `10-booking.css` (`outline: 2px solid rgba(var(--accent-rgb), 0.82)` на focus-visible; `transform: translateY(-1px)` hover; `scale(0.97)` active; disabled — `var(--surface-muted)`/`var(--text-quaternary)`/`cursor:not-allowed`).
- `@media (prefers-reduced-motion: reduce)` — снять transition/animation с интерактивов (как booking).

- [ ] **Step 1: Переписать `src/styles/12-players.css`**

Полный baseline-CSS (имплементер вставляет целиком, заменяя текущее содержимое; токены и паттерны зеркалят `10-booking.css`):

```css
/* ── Вкладка «Клиенты»: master-detail (список + карточка с табами) ──────────── */

.clients-screen {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.clients-head {
  margin-bottom: 8px;
}

.clients-head-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.clients-new-client-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 30px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  padding: 0 12px;
  background: var(--accent);
  color: var(--text-on-accent);
  font-size: 12px;
  font-weight: 700;
  transition: background 100ms ease, transform 100ms ease;
}

.clients-new-client-btn:hover:not(:disabled) {
  background: var(--accent-hover);
  transform: translateY(-1px);
}

.clients-new-client-btn:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.clients-new-client-btn:active:not(:disabled) {
  transform: scale(0.97);
}

.clients-new-client-btn:disabled {
  cursor: not-allowed;
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-quaternary);
}

/* Layout: список фиксированной ширины + карточка на остаток; тянется на остаток высоты. */
.clients-layout {
  display: grid;
  grid-template-columns: minmax(0, 360px) minmax(0, 1fr);
  gap: 10px;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.clients-panel {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
  border: 1px solid var(--border-soft);
  border-radius: 7px;
  background: var(--surface-elevated);
}

.clients-panel-title {
  display: grid;
  gap: 3px;
  min-width: 0;
  padding: 12px 12px 0;
}

.clients-panel-title span {
  overflow: hidden;
  color: var(--text-primary);
  font-size: 15px;
  font-weight: 700;
  line-height: 1.08;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.clients-panel-title strong {
  overflow: hidden;
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  line-height: 1.15;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── Список (master) ────────────────────────────────────────────────────────── */
.clients-search {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 30px;
  margin: 8px 12px 7px;
  border: 1px solid var(--border-default);
  border-radius: 5px;
  padding: 0 10px;
  background: var(--surface-sunken);
  color: var(--text-tertiary);
}

.clients-search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-primary);
  font-size: 12px;
}

.clients-segment-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 0 12px 8px;
}

.clients-segment-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 26px;
  border: 1px solid var(--border-default);
  border-radius: 999px;
  padding: 0 11px;
  background: var(--surface-card);
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, transform 120ms ease;
}

.clients-segment-chip b {
  color: var(--text-tertiary);
  font-size: 11px;
  font-weight: 700;
}

.clients-segment-chip:hover:not(.active),
.clients-segment-chip:focus-visible {
  border-color: var(--border-accent);
  color: var(--accent-bright);
  outline: none;
  transform: translateY(-1px);
}

.clients-segment-chip:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.clients-segment-chip.active {
  border-color: var(--accent);
  background: var(--surface-accent-soft);
  color: var(--accent-bright);
}

.clients-segment-chip.active b {
  color: var(--accent-bright);
}

.clients-list {
  display: grid;
  align-content: start;
  gap: 5px;
  padding: 0 12px 10px;
  overflow-y: auto;
  min-height: 0;
}

.clients-list-skeleton {
  display: grid;
  gap: 5px;
}

.client-row-skel {
  height: 44px;
  border-radius: 6px;
}

.client-row {
  display: grid;
  grid-template-columns: 70px minmax(0, 1fr) 90px;
  grid-template-rows: auto;
  align-items: center;
  gap: 4px 9px;
  min-height: 40px;
  border: 1px solid var(--border-default);
  border-left: 3px solid var(--text-tertiary);
  border-radius: 6px;
  padding: 5px 9px;
  background: var(--surface-card);
  color: var(--text-strong);
  text-align: left;
  transition: border-color 120ms ease, background 120ms ease, transform 120ms ease;
}

.client-row:hover,
.client-row:focus-visible {
  border-color: var(--border-accent);
  background: var(--surface-hover);
  outline: none;
  transform: translateY(-1px);
}

.client-row:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.client-row.vip { border-left-color: var(--accent-hover); }
.client-row.active { border-left-color: var(--success); }
.client-row.regular { border-left-color: var(--text-tertiary); }
.client-row.debt { border-left-color: var(--danger); }

.client-row.selected {
  border-color: var(--border-accent);
  background: var(--surface-hover);
  box-shadow: inset 0 0 0 1px rgba(var(--accent-rgb), 0.22);
}

.client-row > span {
  overflow: hidden;
  color: var(--accent-on-soft);
  font-size: 11px;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-row div {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.client-row strong {
  overflow: hidden;
  color: var(--text-primary);
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-row em {
  overflow: hidden;
  color: var(--text-secondary);
  font-size: 11px;
  font-style: normal;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-row b {
  overflow: hidden;
  color: var(--text-primary);
  font-size: 12px;
  text-align: right;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-row-debt {
  grid-column: 3;
  color: var(--danger-text);
  font-size: 11px;
  font-weight: 600;
  text-align: right;
}

/* ── Карточка (detail) ──────────────────────────────────────────────────────── */
.clients-detail-panel {
  gap: 10px;
}

.client-detail-head {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  margin: 12px 12px 0;
  border: 1px solid var(--border-default);
  border-radius: 7px;
  padding: 12px;
  background: var(--surface-card);
}

.client-avatar {
  display: grid;
  place-items: center;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: var(--accent);
  color: var(--text-on-accent);
  font-size: 16px;
  font-weight: 700;
}

.client-detail-ident {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.client-detail-status {
  color: var(--accent-on-soft);
  font-size: 11px;
  font-weight: 600;
}

.client-detail-ident strong {
  overflow: hidden;
  color: var(--text-primary);
  font-size: 20px;
  line-height: 1.05;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-detail-ident em {
  overflow: hidden;
  color: var(--text-secondary);
  font-size: 11px;
  font-style: normal;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-detail-reservation {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 0 12px;
  background: var(--surface-card);
  color: var(--text-strong);
  font-size: 12px;
  font-weight: 600;
  transition: border-color 100ms ease, background 100ms ease, color 100ms ease, transform 100ms ease;
}

.client-detail-reservation:hover:not(:disabled) {
  border-color: var(--border-accent);
  background: var(--surface-accent-soft);
  color: var(--accent-bright);
  transform: translateY(-1px);
}

.client-detail-reservation:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.client-detail-reservation:disabled {
  cursor: not-allowed;
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-quaternary);
}

.client-detail-chips {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  padding: 0 12px;
}

.client-chip {
  display: grid;
  gap: 3px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 10px;
  background: var(--surface-elevated);
}

.client-chip span {
  color: var(--text-secondary);
  font-size: 11px;
}

.client-chip strong {
  color: var(--text-primary);
  font-size: 18px;
  line-height: 1;
}

.client-chip.is-debt {
  border-color: var(--danger-soft-border);
  background: var(--danger-soft-bg);
}

.client-chip.is-debt strong {
  color: var(--danger-text);
}

.client-detail-tabs {
  display: flex;
  gap: 4px;
  margin: 0 12px;
  border-bottom: 1px solid var(--border-soft);
}

.client-detail-tab {
  height: 32px;
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  padding: 0 10px;
  transition: color 100ms ease, border-color 100ms ease;
}

.client-detail-tab:hover {
  color: var(--text-primary);
}

.client-detail-tab:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: -2px;
}

.client-detail-tab.active {
  color: var(--accent-bright);
  border-bottom-color: var(--accent);
}

.client-detail-content {
  overflow-y: auto;
  min-height: 0;
  padding: 0 12px 12px;
}

/* ── Кошелёк ─────────────────────────────────────────────────────────────────── */
.clients-wallet-section {
  display: grid;
  gap: 12px;
}

.clients-wallet-figures {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.clients-wallet-figure {
  display: grid;
  gap: 3px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 10px;
  background: var(--surface-elevated);
}

.clients-wallet-figure span {
  color: var(--text-secondary);
  font-size: 11px;
}

.clients-wallet-figure strong {
  color: var(--text-primary);
  font-size: 22px;
  font-weight: 800;
  line-height: 1;
}

.clients-wallet-figure.is-debt strong {
  color: var(--danger-text);
}

.clients-wallet-form {
  display: grid;
  gap: 7px;
  border: 1px solid var(--border-default);
  border-radius: 7px;
  padding: 10px 12px;
  background: var(--surface-card);
}

.clients-wallet-form.is-muted {
  opacity: 0.62;
}

.clients-section-title {
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 700;
}

.clients-wallet-form label,
.clients-package-select,
.clients-new-form label {
  display: grid;
  gap: 4px;
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
}

.clients-wallet-form input,
.clients-package-select select,
.clients-new-form input {
  height: 30px;
  min-width: 0;
  border: 1px solid var(--border-default);
  border-radius: 5px;
  padding: 0 9px;
  outline: 0;
  background: var(--surface-sunken);
  color: var(--text-primary);
  color-scheme: dark;
  font-size: 12px;
  transition: border-color 100ms ease, box-shadow 100ms ease;
}

.clients-wallet-form input:focus,
.clients-package-select select:focus,
.clients-new-form input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 2px rgba(var(--accent-rgb), 0.22);
}

.clients-wallet-form input:disabled,
.clients-package-select select:disabled {
  color: var(--text-quaternary);
  cursor: not-allowed;
}

.clients-primary-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 36px;
  margin-top: 2px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  background: var(--accent);
  color: var(--text-on-accent);
  font-size: 12px;
  font-weight: 700;
  transition: background 100ms ease, transform 100ms ease;
}

.clients-primary-action:hover:not(:disabled) {
  background: var(--accent-hover);
  transform: translateY(-1px);
}

.clients-primary-action:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.clients-primary-action:active:not(:disabled) {
  transform: scale(0.98);
}

.clients-primary-action:disabled {
  cursor: not-allowed;
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-quaternary);
}

.clients-debt-action {
  border-color: var(--danger-soft-border);
  background: transparent;
  color: var(--danger-text);
}

.clients-debt-action:hover:not(:disabled) {
  border-color: var(--danger);
  background: var(--danger-soft-bg);
  color: var(--danger-text);
}

/* ── Пакеты ──────────────────────────────────────────────────────────────────── */
.clients-packages-section {
  display: grid;
  gap: 12px;
}

.client-package-list {
  display: grid;
  gap: 6px;
}

.client-package-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 2px 10px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 9px 11px;
  background: var(--surface-elevated);
}

.client-package-row.is-expired {
  opacity: 0.6;
}

.client-package-row strong {
  grid-column: 1;
  overflow: hidden;
  color: var(--text-primary);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-package-row span {
  grid-column: 1;
  color: var(--text-secondary);
  font-size: 11px;
}

.client-package-bonus {
  color: var(--accent-on-soft) !important;
}

.client-package-row b {
  grid-row: 1 / 3;
  grid-column: 2;
  align-self: center;
  color: var(--text-tertiary);
  font-size: 11px;
  text-align: right;
  white-space: nowrap;
}

.clients-package-buy {
  display: grid;
  gap: 8px;
  border: 1px solid var(--border-default);
  border-radius: 7px;
  padding: 10px 12px;
  background: var(--surface-card);
}

.clients-package-preview {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 6px;
}

.clients-package-preview span {
  display: grid;
  gap: 2px;
  min-width: 0;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 7px 8px;
  background: var(--surface-elevated);
}

.clients-package-preview span.attention {
  border-color: var(--danger-soft-border);
  background: var(--danger-soft-bg);
}

.clients-package-preview strong,
.clients-package-preview b {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.clients-package-preview strong {
  color: var(--text-secondary);
  font-size: 10px;
}

.clients-package-preview b {
  color: var(--text-primary);
  font-size: 12px;
}

/* ── История ─────────────────────────────────────────────────────────────────── */
.clients-history-list {
  display: grid;
  gap: 4px;
}

.client-history-row {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  min-height: 36px;
  border-bottom: 1px solid var(--border-soft);
  padding: 6px 2px;
}

.client-history-time {
  color: var(--text-tertiary);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
}

.client-history-body {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.client-history-body strong {
  display: flex;
  align-items: center;
  gap: 7px;
  overflow: hidden;
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-history-reversal {
  flex: none;
  padding: 0 6px;
  border-radius: 999px;
  background: var(--surface-muted);
  color: var(--text-secondary);
  font-size: 9px;
  font-style: normal;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.client-history-detail {
  overflow: hidden;
  color: var(--text-secondary);
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-history-amount {
  font-size: 13px;
  font-weight: 700;
  text-align: right;
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}

.client-history-row.is-credit .client-history-amount {
  color: var(--success-text);
}

.client-history-row.is-debit .client-history-amount {
  color: var(--danger-text);
}

/* ── Модалка нового клиента (поверх .panel-modal) ───────────────────────────── */
.clients-new-form {
  display: grid;
  gap: 10px;
  min-width: 280px;
}

/* ── Reduced motion ───────────────────────────────────────────────────────────── */
@media (prefers-reduced-motion: reduce) {
  .client-row,
  .clients-segment-chip,
  .client-detail-tab,
  .clients-primary-action,
  .clients-new-client-btn,
  .client-detail-reservation {
    transition: none;
  }
}
```

(Если каких-то токенов нет — `--danger-soft-border`/`--success-text`/`--accent-on-soft`/`--accent-bright` и т.п. — они уже используются в `10-booking.css`/`12-players.css` (текущем), значит существуют в `@afk4/tokens`. Не вводить новых токенов.)

- [ ] **Step 2: Сборка + визуальная проверка превью**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: без ошибок.

Превью (mock-режим): `bun run dev` → отдать URL `http://127.0.0.1:5174/` → вкладка «Клиенты». Проверить глазами: master-detail (список слева, карточка справа), сегмент-чипы со счётчиками, выбор клиента подсвечивается, табы Кошелёк/Пакеты/История переключаются, история цветная (кредит зелёный/дебет красный, пометка сторно), пакеты человеческие (мин/бонус/срок), формы Пополнить/Погасить, кнопка «+ Новый клиент» открывает модалку, EmptyState при пустом поиске, skeleton при загрузке. Тёмная тема, акцент синий.

- [ ] **Step 3: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): CSS master-detail для раздела «Клиенты»"
```

---

### Task 11: Полная верификация S1

Прогнать весь тест-сьют (App.test отдельно), тайпчек, i18n-гарды; убедиться в ноль-регрессии и полном покрытии спеки S1.

**Files:** нет изменений (только проверки; при находках — фиксы в рамках затронутых задач).

- [ ] **Step 1: Все тесты фронта (КРОМЕ App.test)**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test --test-name-pattern '.*' src/ 2>&1 | tail -30` (или просто `bun test` если App.test изолирован конфигом)
Альтернатива (надёжно): `/home/fedya/.bun/bin/bun test src/players/`
Expected: все `src/players/*` зелёные.

- [ ] **Step 2: App.test ОТДЕЛЬНЫМ прогоном**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Expected: зелёно (весь players-регион + остальное).

- [ ] **Step 3: i18n-гарды**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test`
Expected: parity (ru=en=tg), voice (нет caps/«компьютер»), tg≠ru guard — все зелёные.

- [ ] **Step 4: Тайпчек + сборка**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: без ошибок.

- [ ] **Step 5: Финальный статус**

Run: `cd /home/fedya/projects/afk4.net && git log --oneline feat/operator-clients-s1 -12`
Expected: видны коммиты Tasks 1-10. Сообщить пользователю: S1 готов к ревью/PR.

---

## Self-Review

**Spec coverage (S1-часть спеки):**
- «Редизайн в master-detail (список + богатая карточка)» → Tasks 7 (ClientList) + 8 (ClientDetail) + 9 (оркестратор) + 10 (CSS).
- «Богатый рендер записей истории поверх wallet-summary.RecentEntries (без фильтра/пагинации)» → Task 3 (`projectLedgerEntry`/`ledgerTypeLabel`) + Task 4 (HistorySection).
- «Человеческие пакеты» → Task 3 (`projectPlayerPackage`) + Task 5 (PackagesSection); заменяет `<b>active</b>`.
- «Честные сегменты на стабильных id (Все/VIP/С долгом/Неактивные; «Новые»/«Спящие» удалены)» → Task 3 (`ClientSegmentId`/`buildClientSegments`/`matchesSegment`) + Task 9 (оркестратор хранит id, не строку) + Task 2/9 (удаление мёртвых ключей).
- «Шапка: глобальные метрики + кнопка + Новый клиент; per-client числа убраны» → Task 9.
- «Кошелёк: две раздельные формы; БЕЗ ручной корректировки» → Task 6.
- «Модалка Новый клиент через PanelModal (реальное createPlayer); НЕ строим drawer-каркас power-tools» → Task 8 (NewClientModal) + Task 9.
- «Скелетоны через useDeferredFlag, EmptyState» → Tasks 7/4/5 (примитивы) + Task 9 (showSkeleton).
- «БЕЗ нового бэкенда/ledger-эндпоинта/PIN/правки профиля» → не затрагивается; источник истории — `walletSummary.recentEntries` (S1b переключит на paged).
- «i18n ru/en/tg, tg реальный, bun run gen» → Task 2.
- «Behavior-preservation (App.test покрытие)» → Task 9 Step 3 (все 9 поведенческих тестов обновлены под новый DOM, покрытие сохранено).

**Архитектурное отклонение от буквы спеки (обосновано #29/#35):** спека предлагала `op.players.ledger.type.*`; план переиспользует существующие `ledger.type.*` (ru/en/tg, native Tajik, покрывают 11/13 типов) и добавляет только 2 недостающих ключа + fallback. Это убирает дубль готовой инфраструктуры.

**Placeholder scan:** код приведён полностью в каждом шаге (компоненты, тесты, оркестратор, CSS-baseline). Ноль «TBD»/«аналогично Task N». CSS — единственное оправданное отступление (полный baseline + класс-контракт + визуальные требования, приёмка = сборка + превью, т.к. CSS в jsdom не тестируется).

**Type consistency между тасками:**
- `LedgerEntryView`/`PlayerPackageView`/`ClientSegment`/`ClientSegmentId` (Task 3) — единственный источник, импортируются в Tasks 4/5/7/8/9.
- `ClientDetailTab` объявлен в `ClientDetail.tsx` (Task 8), импортируется оркестратором (Task 9).
- `LedgerEntryDto`/`PlayerPackageDto`/`PackageOptionDto`/`WalletSummaryDto` — из `operatorApiClients` (S0 уже типизировал), потребляются секциями и деталью.
- `recentEntries` приведён к `LedgerEntryDto[]` через `walletSummary?.recentEntries ?? []` (Task 9) — совпадает с входом `HistorySection` (Task 4).
- Класс-контракт CSS (Task 10) перечисляет ровно те классы, что рендерят компоненты Tasks 4-9.

**Open risks / неоднозначности (вынесено пользователю отдельно в возврате):**
- App.test players-регион оказался обширнее «нескольких тестов»: 9 поведенческих тестов + head/empty. Все перечислены и обновлены в Task 9 Step 3; риск — если в файле есть ещё косвенные ссылки на старые тексты («Создать бронь» как кнопка, «Новая карта» как кнопка) — Task 9 Step 3(i) включает финальный grep-проход.
- `feedback` показывается только в оркестраторе (Task 9) — единый источник (#32); секции его не дублируют.
- Закрытие NewClientModal при ошибке createPlayer — закрываем всегда, ошибка в глобальном FeedbackNotice (Task 9 Step 1) — приемлемо для S1, помечено.
