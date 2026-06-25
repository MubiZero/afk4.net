---
name: afk4-operator-stock-epic
description: "Реворк склада оператора — вынос в отдельную секцию рейла «Склад»; слайсы S0–S4; S0 в PR #116"
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

**Слайсы:** S0 каркас+Остатки v2 (**PR #116**, бэк avg-cost+миграция + фронт раздел/экран; реальный per-product порог вместо хардкода `LOW_STOCK_THRESHOLD=2`); S1 Приёмка (purchase, преподстановка себест, накладная); S2 Журнал (`getStockMovements` готов на бэке); S3 Штрих-коды (модель N-штрихов + Управление→Товары + сканер в POS-корзине + приёмке); S4 Инвентаризация (пересчёт→расхождения→`adjustment`).

**S0 backlog (отложено):** строковые действия ＋/− и «Оформить приёмку» = заглушки (реальные в S1); разбивка Сводки по категориям (нужно имя категории в каталог-DTO); отрицательная маржа рендерится emerald-цветом; видимость экономич. колонок по роли; `.cash-stock-*` имена классов в `/stock/` (техдолг, CSS дедуплицирован в `22-stock.css`).

См. [[operator-redesign-phase0-decisions]] (Касса/Этапы), [[operator-theme-and-preview]] (токены/emerald-акцент), [[afk4-auto-merge-authorized]].
