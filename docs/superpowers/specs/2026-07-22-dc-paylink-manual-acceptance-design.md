# DC (DushanbeCity) приём по pay-link/QR + ручное подтверждение — дизайн

**Дата:** 2026-07-22
**Эпик:** «Рефокус Клуб + Платежи/Лояльность» → под-проект 3.
**Статус:** дизайн принят пользователем, спека на ревью → writing-plans.
**Предшествующие решения:** `2026-07-21-dc-paylink-manual-acceptance-decisions.md` (формат ссылки,
полуручная схема), `2026-07-20-operator-club-payments-rethink-decisions.md` (эпик).
**Чувствительность:** money-path. Старый dcgate был заморожен из-за внешнего bank-bot; пользователь
подтвердил, что bank-bot **ретайрен** → снос безопасен. Prod afk4 не задеплоен.

## Goal

Дать киберклубу **вторичный** способ приёма пополнений кошелька — перевод на карту DushanbeCity
через pay-link/QR, с **ручным подтверждением кассиром**. Основной онлайн-приём — Eskhata (сделан).
Заодно **полностью удалить** старый dcgate (сломанное Telegram-автоподтверждение), чтобы не тащить
мёртвый money-path.

## Реальность приёма (почему так)

- **Банк DC НЕ сообщает о факте оплаты** (в отличие от Eskhata и старого dcgate-через-Telegram).
  Значит подтверждает человек. Самый чистый контекст для этого — **Касса**: игрок у стойки, кассир
  видит поступление в своём банке и подтверждает на месте. Онлайн-самообслуживание с ручным
  подтверждением даёт зазор «заплатил, но не зачислено» — не берём.
- Новый DC **не использует API/webhook**: это «тупая» платёжная ссылка. Формат:
  `http://pay.dc.tj/?A={card}&s={amount}&c={comment}&f1=133`
  - `A` — номер карты приёма, `s` — сумма, `c` — комментарий-референс, `f1=133` и прочее — константа.
  QR = сам URL (игрок сканирует → открывается страница/банк-приложение DC).
- Тех-детали про поломку Telegram конечному пользователю не показываем (внутренняя информация).

## Принятые решения (из brainstorming 2026-07-22)

1. **Где живёт:** только **Касса** (Operator.App), кассир подтверждает. Не онлайн, не киоск.
2. **Модель:** **pending `PaymentIntent` + ручное подтверждение** (как eskhata/dcgate: intent→fulfill,
   но fulfill вручную). Оставляет аудит-след и ловит «кассир подтвердил позже».
3. **Старый dcgate:** **полный снос** (клиенты, resolver, webhook, entity, таблицы, онлайн-метод,
   Telegram-провижининг, UI-блок). Bank-bot ретайрен, таблицы дропаем сразу.
4. **Player.Shell (киоск):** **в бэклоге**, делаем ПОСЛЕ Operator.App. В этом под-проекте не трогаем;
   его онлайн-пополнение станет нерабочим после сноса dcgate — **принято** (переедет на Eskhata в
   своём бэклог-заходе, [[operator-club-payments-rethink]] Task для Player.Shell).
5. **Анти-фрод усыплён проектно** (`ExecuteNow`) → подтверждение мгновенное, без approval-очереди.

## Границы (scope)

- **Трогаем:** `AFK4.Platform.Api`, `AFK4.Shared.Contracts`, `AFK4.Operator.App.Web`.
- **НЕ трогаем:** `AFK4.Player.Shell.Web` (бэклог), Eskhata money-path, наличные/POS-settlement логику.

---

## Архитектура

### A. Новый DC-приём в Кассе

**Сущность конфига** — `DcPayLinkConfigEntity` (новая, лёгкая; per-branch с org-fallback как у
старого resolver'а — но без dcgate-проекта/Telegram):
- `Id`, `OrganizationId`, `BranchId?` (null = org-уровень fallback), `ReceivingCardEncrypted`
  (полный PAN, шифруется `ISecretProtector` — нужен для сборки ссылки), `CardLast4` (для UI),
  `CommentTemplate` (дефолт `AFK4-{ref}`), `IsActive` (bool), `CreatedAtUtc`/`UpdatedAtUtc`.
- **Один активный конфиг на scope** (v1; несколько карт — YAGNI, при нужде расширяемо).

**Сборщик ссылки** — `DcPayLink.Build(cardNumber, amountMinor, currencyCode, comment)` (чистая
функция, unit-тестируемая): формирует `http://pay.dc.tj/?A={card}&s={amount}&c={enc(comment)}&f1=133`.
- Сумма `s`: **мажорные единицы (сомони), 2 знака, InvariantCulture** (переиспользуем `minorToMajor`).
  ⚠️ **Единственная деталь на эмпирическое подтверждение** — не ждёт ли DC целые сомони; правится
  одной строкой, поведение по умолчанию безопасно (неверный формат → игрок увидит не ту сумму на
  странице DC, деньги не двигаются без ручного подтверждения кассира).
- Комментарий URL-энкодится.

**Эндпоинты POS** (`DcTopUpEndpoints.cs`, staff-авторизация):
- `POST /api/branches/{branchId}/pos/dc-topups` — завести пополнение. Право `billing.wallet.top_up`,
  требует **открытой смены**. Тело `{ playerAccountId, amountMinorUnits, currencyCode }`. Резолвит
  активный `DcPayLinkConfig` по branch (fallback org; нет конфига → 409), расшифровывает карту,
  собирает ссылку, `ref` = короткий идентификатор из `PaymentIntentId`, создаёт
  `PaymentIntent(Method="dc", State="pending", Purpose="wallet_topup", GatewayPayUrl=link,
  GatewayComment=ref)`. Ответ `{ intentId, payUrl, comment, amount, cardLast4 }`.
- `POST /api/branches/{branchId}/pos/dc-topups/{intentId}/confirm` — подтвердить получение. Право
  `billing.wallet.top_up`, открытая смена. Зачисляет через `TopUpWalletAsync(playerAccountId,
  branchId, actorStaffUserId, TopUpWalletRequest{ Amount, Reason="dc_counter_topup",
  IdempotencyKey=intentId.ToString("N") }, ct)`; `State→fulfilled`, `FulfilledAtUtc`. Гарды:
  `State=="pending"` (иначе idempotent no-op), `Method=="dc"`, игрок `IsActive`.
- `POST /api/branches/{branchId}/pos/dc-topups/{intentId}/cancel` — игрок не заплатил → `State→cancelled`.
- `GET /api/branches/{branchId}/pos/dc-topups?state=pending` — список ожидающих (отложенное
  подтверждение).

**UI Кассы** (Operator.App.Web): в потоке пополнения кошелька игрока добавить способ «DushanbeCity
(перевод)». Кассир вводит сумму → экран показывает **QR** (из `payUrl`), сумму, комментарий-референс,
кнопки «Оплата получена» / «Отмена». Добавить зависимость `qrcode` в `Operator.App.Web`
(сейчас есть только в `Player.Shell.Web`). Живой feedback + optimistic по гайдлайну.

**Конфиг-UI** (setup-экран «Платежи и лояльность»): заменить нынешний DC-блок
(`PaymentGatewaysWorkspace`) простой формой — номер карты приёма (в UI `••••{last4}`), шаблон
комментария, вкл/выкл. Гейт `payments.gateways.manage`.

### B. Снос старого dcgate

**Backend — удалить:**
- `Payments/DcGate/*` (`IDcGateClient`/`DcGateClient`/`Factory`, `IDcGateAdminClient`/`DcGateAdminClient`,
  `DcGateOptions`).
- `Payments/IBranchPaymentGatewayResolver.cs` + `EfBranchPaymentGatewayResolver.cs` +
  `BranchPaymentGatewayStatus.cs`.
- `Data/BranchPaymentGatewayEntity.cs`, `Data/DcGateWebhookEventEntity.cs` + их `DbSet`/конфиги.
- `Endpoints/PaymentGatewayEndpoints.cs` целиком (webhook `/api/public/payments/dcgate/webhook` + все
  owner payment-gateway эндпоинты).
- `EndpointHelpers.Http.cs::DcGateSignatureIsValid` (HMAC-хелпер).
- Ветку `method=="dcgate"` в `PlayerSelfServiceEndpoints` `top-up-intent` (оставить `counter`+`eskhata`).
- Регистрацию DI (`Program.cs`), секцию конфига `DcGate`, named `HttpClient "dcgate"`.
- Контракты `DcGateWebhookPayload.cs`, `OwnerPaymentGatewayDtos.cs`, dcgate-часть `PlayerTopUpIntentRequest`.

**Миграция:** дроп таблиц `BranchPaymentGateways` и `DcGateWebhookEvents`. `PaymentIntentEntity`
оставляем (используется counter/eskhata/dc); dcgate-специфичные поля (`GatewayPaymentId`, `Disputed`)
оставляем — они nullable и переиспользуются/безвредны (чистку полей — отдельно, не в money-path-сносе).

**Frontend (Operator.App.Web) — удалить:** `PaymentGatewaysWorkspace.tsx`,
`api/clients/paymentGateways.ts`, dcgate-часть `PaymentMethodsSection.tsx`, связанные i18n-ключи и CSS.

## Поток данных (happy path)

1. Игрок у стойки хочет пополнить 50 сомони переводом DC.
2. Кассир: способ «DushanbeCity», сумма 50 → `POST .../dc-topups`.
3. Сервер собирает `pay.dc.tj/?A=<card>&s=50.00&c=AFK4-<ref>&f1=133`, создаёт pending-intent, отдаёт QR.
4. Касса рендерит QR + «AFK4-<ref>» + 50 сомони.
5. Игрок сканирует, платит переводом со своего банк-приложения (комментарий = референс).
6. Кассир видит поступление в своём банке → «Оплата получена» → `POST .../confirm`.
7. `TopUpWalletAsync` зачисляет кошелёк (смена кассира, идемпотентно) → `fulfilled`. Баланс > 0 →
   будущие онлайн-брони игрока авто-подтверждаются (существующий гейт по балансу, бесплатно).

## Обработка ошибок

- Нет активного `DcPayLinkConfig` для branch/org → `409` при заведении (кассиру: «DC не настроен»).
- Закрытая смена → `409`/`400` (как остальные POS-действия).
- Двойное подтверждение → идемпотентно (второй confirm — no-op, деньги один раз).
- Игрок неактивен → отказ до кредита (IsActive-guard).
- Игрок не заплатил / ушёл → кассир жмёт «Отмена» → `cancelled` (кошелёк не тронут).
- Сборка ссылки при пустой/битой карте в конфиге → `409`/`500` до создания intent (деньги не двигаются).

## Тестирование

**Platform.Api (xUnit, `PlatformApiFactory`):**
- Завести DC-intent: создаётся pending, `payUrl` содержит карту/сумму/референс, `Method="dc"`.
- Confirm зачисляет кошелёк ровно раз; повторный confirm идемпотентен (баланс не растёт).
- Confirm при закрытой смене → отказ.
- Confirm неактивного игрока → отказ, кредита нет.
- Cancel → `cancelled`, кошелёк не тронут; confirm после cancel → отказ.
- Нет конфига → `409` при заведении.
- Снос: тесты удалённых dcgate-эндпоинтов/классов убраны; проект компилируется; миграция дропа
  применяется; grep не находит висячих ссылок на dcgate-типы.

**Unit:** `DcPayLink.Build` — формат URL, 2 знака суммы, URL-энкод комментария, константа `f1=133`.

**Operator.App.Web (`bun test` + `bun run build`):** тест формы конфига DC (валидация карты, вкл/выкл,
`••••last4`); тест DC-компонента в Кассе (рендер QR из payUrl, кнопки confirm/cancel, feedback-тост).

## Вне scope / отложено

- Player.Shell киоск online top-up (бэклог, после Operator.App; переезд на Eskhata).
- Eskhata inbound webhook (регистрация CallbackUrl + IP allowlist — отдельно).
- DC как метод оплаты POS-продажи (сейчас только пополнение кошелька).
- Несколько карт приёма на branch / отложенная админ-очередь подтверждений (YAGNI).
- Чистка nullable dcgate-полей `PaymentIntentEntity` (не в money-path-сносе).
