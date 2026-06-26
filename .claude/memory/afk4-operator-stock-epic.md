---
name: afk4-operator-stock-epic
description: "Реворк склада оператора — вынос в отдельную секцию рейла «Склад»; слайсы S0–S4; S0 PR #116, S1 Приёмка PR #117, S2 Журнал PR #118"
metadata: 
  node_type: memory
  type: project
  originSessionId: 3d48e586-79f2-4723-89c6-5315e7fc7aea
---

Эпик «реворк склада оператора»: склад вынесен из вкладки Кассы в **отдельный корневой раздел рейла «Склад»** (`WorkspaceId 'stock'`, виден при `inventory.view` | `inventory.stock.manage`). Раздел = 4 вкладки (наполняются по слайсам) + правая «Сводка» (StockWorkspace владеет своим внутренним layout'ом в колонке 2 — 3-я колонка shell НЕ задействована, она бы связала App.tsx с табами Склада).

**Каноны:**
- Спека: `docs/superpowers/specs/2026-06-25-operator-inventory-rework-design.md`
- План S0: `docs/superpowers/plans/2026-06-25-operator-stock-s0-rail-and-levels.md`
- Мокапы (живые, dark-тема оператора): `.superpowers/brainstorm/3236934-1782385059/content/*.html` (stock-shell-v5=Остатки, receiving=Приёмка, journal=Журнал, inventory-count=Инвентаризация, pos-scan=сканер в корзину, product-barcodes=штрихи в Товарах).

**Решения (durable):**
- Себестоимость — **средневзвешенная скользящая** (`PosProductEntity.AvgCostMinorUnits`, пересчёт в `CreateStockMovementAsync` только при `purchase`; первый приход → avg=себест прихода). Стоимость остатка = остаток×avg; маржа=(цена−avg)/цена.
- **Money на проводе:** `price` = nested `MoneyDto` (`readMoney(p,'price')?.minorUnits`), а `avgCostMinorUnits` = ПЛОСКОЕ число (`readNumber`). Не перепутать — на этом был prod-баг (Цена/Маржа пусты), пойман финальным ревью.
- Сканер штрихов — **USB-HID-клавиатура** (печатает цифры+Enter, без API); **несколько штрихов на товар** (коллекция, новая таблица `ProductBarcodeEntity`); пик того же товара = +1 к кол-ву.
- Без справочника поставщиков (текстовое поле на накладной).
- Разведение: `Управление → Товары` = только каталог (имя/цена/категория/порог/штрихи); складские движения/история уезжают в раздел Склад.

**Слайсы:** S0 каркас+Остатки v2 (**PR #116**, бэк avg-cost+миграция + фронт раздел/экран; реальный per-product порог вместо хардкода `LOW_STOCK_THRESHOLD=2`); **S1 Приёмка — ЗАКРЫТ, PR #117** (почти чистый фронт, бэк `purchase`+avg-cost готов с S0); **S2 Журнал — ЗАКРЫТ, PR #118**; S3 Штрих-коды (модель N-штрихов + Управление→Товары + сканер в POS-корзине + приёмке); S4 Инвентаризация (пересчёт→расхождения→`adjustment`).

**S2 durable (PR #118):** вкладка «Журнал» (read-only, `viewInventory|manageInventoryStock`) — лента движений, фильтр по типу/поиск, период справа (Сегодня/7дней/Все, дефолт Все), день-группы, итоги, экспорт CSV. Чистая логика в `stock/journalModel.ts` (map/filter/period/group/summary/buildCsv; **детерминизм — `nowMs` параметром, период/группы по UTC**, не `Date.now()` внутри pure). Бэк: `StockMovementDto.CreatedByDisplayName` резолвит сервер в `GetStockMovementsAsync` (справочник `/staff` закрыт правом `ManageBranchStaff` — оператор не достанет → серверный резолв обходит permission; один запрос, org-guard). Период фильтруется КЛИЕНТОМ на окне ≤200 (эндпоинт даты игнорит, `Take(Math.Min(limit,200))`) — кап показан честной нотой. CSV — машинно-читаемый (`minor/100` без NBSP/символа валюты, ISO-дата, UTF-8 BOM для кириллицы в Excel), НЕ через `formatMinorUnits`. Типы-фильтры = реальные `movementType` (мокапный сплит Списания/Корректировки не подделан — оба `adjustment`). Уборка: история движений убрана из `Управление→Товары` (форма записи оставлена до S4), сироты `op.settings.stock.history.*`+`reasonFallback` вычищены. Грабли: финал гейт пропустил красный tg-guard в пакете `packages/i18n` (см. [[frontends-on-bun-test]]).

**S1 durable (PR #117):** вкладка «Приёмка» (видна при `manageInventoryStock`); чистая логика накладной в `stock/receivingModel.ts` (себестоимость строки = сырой `unitCostText`, парс в minor по требованию — иначе инпут переформатируется на keystroke); проведение = N **последовательных** идемпотентных `createStockMovement(purchase)` (свежий ключ на POST, кнопка дизейбл на время; частичный сбой → `remaining.shift()` по успеху оставляет непроведённые строки, «Проведено X из N»); себест преподставлена из `avgCostMinorUnits`. Остатки: ＋→Приёмка с товаром (preload через поднятый `activeTab`+`receivePreload` в `StockWorkspace`, `onConsumePreload` одноразово), «Оформить приёмку»→Приёмка, − → `WriteOffDialog` (`adjustment` −qty, себест из avg, рефетч через `reloadNonce` в deps useEffect). **Все S0-заглушки ＋/−/«Оформить» убраны**, сирота-ключ `orderBtnSoon` вычищен. Сканер НЕ здесь (S3) — полоса = честный ручной поиск, без фейкового «сканер активен»; `addOrAccumulate` = шов под сканер. Грабли: `tsc -b` тайпчекает тесты → типизировать bun-моки (см. [[frontends-on-bun-test]]); финальный гейт обязан включать `bun run build`, не только `bun test`.

**Бэклог (отложено):** разбивка Сводки по категориям (нужно имя категории в каталог-DTO); отрицательная маржа рендерится emerald-цветом; видимость экономич. колонок по роли; `.cash-stock-*` имена классов в `/stock/` (техдолг); **S1-минорки в долг** — приёмка с нулевой/непроставленной себестоимостью проходит молча (нет UX-валидации «себест не указана»); пустое поле кол-ва молча откатывается к 1.

См. [[operator-redesign-phase0-decisions]] (Касса/Этапы), [[operator-theme-and-preview]] (токены/emerald-акцент), [[afk4-auto-merge-authorized]].
