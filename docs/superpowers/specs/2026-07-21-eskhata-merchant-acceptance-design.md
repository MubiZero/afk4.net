# Eskhata Merchant — приём платежей (пополнение кошелька игрока)

**Дата:** 2026-07-21
**Под-проект:** 2 из эпика [Рефокус Клуб + Платежи/Лояльность] → часть стратегии «Оператор = единая админка».
**Статус:** дизайн (spec), ждёт ревью пользователя перед планом.

---

## 1. Цель и контекст

Eskhata Merchant («Эсхата-Мерчант», ЭМ) становится **основным** способом онлайн-приёма
денег от игрока в кошелёк. DushanbeCity (dcgate) остаётся вторичным/полуручным (под-проект 3).

Сейчас в репозитории от Eskhata есть только **хранение доступов** (`EskhataMerchantConfigEntity`
+ `/api/owner/eskhata-config` + форма `EskhataGatewayForm`), статус максимум «настроен · не активен».
Самого приёма нет: ни HTTP-клиента с подписью, ни создания заказа, ни webhook, ни зачисления.

Эталон реализации целиком есть во внешнем проекте `nj-cosmetics.com` (Laravel) — он **обкатан на
живом API банка** и снимает почти все технические неизвестности (подпись, статус, webhook, deeplink).
Существующий money-path под dcgate (`PaymentIntentEntity` → публичный webhook → `CreditOnlineTopUpAsync`)
переиспользуется почти без швов.

### Не-цели (YAGNI)
- Возврат/отмена через Merchant API (`/refund`, `/cancel`) — эндпоинты банка реализуем в клиенте,
  но UI возврата **не строим** в этом под-проекте (возврат кошелька у нас уже есть отдельным money-action).
- Приём за товары/POS-чек напрямую — только **пополнение кошелька** (`Purpose = "wallet_topup"`).
- Мультивалютность приёма — только TJS (`currency=972`); маппинг кодов заложим, но продуктово TJS.
- Изменение money-path dcgate — **не трогаем** (внешний freeze). Eskhata добавляется рядом как
  второй провайдер, зачисление идёт через тот же `CreditOnlineTopUpAsync`.

---

## 2. Ключевое решение: `orderTypeId = 3` (DynamicPos)

Один тип заказа закрывает **оба** пользовательских сценария одним create-запросом:

- Ответ отдаёт **`qr`** (текст Единого QR — для киоска/игрового ПК) **и** **`invoiceUrl`**
  (ссылка на оплату — из неё строим deeplink для телефона игрока).
- Банк **сам назначает свободную кассу** — не нужно заводить и держать `posId` по филиалам.
- Требует `merchantId` (тестовый = `28652`); `posId` при типе 3 **не передаётся** (банк вернёт его в ответе).

### Два сценария
| Где платит игрок | Механика | UI |
|---|---|---|
| **Телефон игрока (PWA)** | из `invoiceUrl` строим `eskhata://pay/<ref>` → кнопка открывает приложение банка → поллинг статуса | кнопка «Оплатить» |
| **Игровой ПК / киоск самообслуживания (Player.Shell)** | показываем `qr` как QR-код на экране → игрок сканит **своим** телефоном → webhook/поллинг | QR на экране |

ПК не может открыть `eskhata://` на телефоне игрока — поэтому там именно QR.

---

## 3. Подпись и вызовы API (durable-факты из эталона)

**Подпись — обычный `SHA256(конкатенация_значений + "." + hashKey)`, НЕ HMAC** (спека банка вводит в
заблуждение словом «HMAC»; живой API и эталон используют голый SHA-256). Значения всех скалярных
параметров (кроме массивов и самого `hash`) склеиваются **в порядке спецификации**, затем `. + hashKey`.

- **create** (порядок из рабочего примера доки и эталона): `invoiceId + amount(2 знака) + currency + description + posId + orderTypeId`
- **status / cancel / refund**: `invoiceId + orderId + amount(2 знака) + currency + posId`
- `amount` в теле запроса — число (`23.20`); в строке хеша — строка **ровно с 2 знаками** после точки.
- Заголовок `X-CompanyId` = **Base64(companyId)**, где companyId = id организации из ЛК.

### ⚠️ Единственная реальная неизвестность — хеш для `orderTypeId=3`
Эталон `nj-cosmetics` использует тип **2** с `posId`; пример подписи в доке — только для типа **1**.
Точный порядок склейки для типа 3 (куда встаёт `merchantId`, выпадает ли `posId` из строки хеша)
док прямо не показывает. **Определяется эмпирически** об тестовый endpoint в начале Фазы 1:
итерируем порядок, ориентируясь на код ответа `-2` (ошибка контроля суммы) / `-12` (неправильный тип
заказа) / `0` (успех). Это тестируемо, дизайн не блокирует, но помечено честно как открытый пункт.

### Эндпоинты банка
- `POST <base>/merchant/api/v1/orders/create` — создать заказ (тип 3) → `{orderId, orderStatus:NEW, qr, invoiceUrl}`
- `POST <base>/merchant/api/v1/orders/status` — статус (перепроверка перед зачислением)
- `POST <base>/merchant/api/v1/orders/cancel` — отмена (только в статусе NEW)
- `POST <base>/merchant/api/v1/orders/refund` — возврат (только внутрибанковский)

### Коды статуса заказа
`NEW` (создан) · `IN PROCESS` · `COMPLETED` (оплачен) · `CANCELED` · `REFUNDED`.

---

## 4. Архитектура (маппинг на существующее)

### 4.1 HTTP-клиент — новый
`IEskhataMerchantClient` + `EskhataMerchantClient` (зеркало `IDcGateClient`/`DcGateClient`),
плюс `IEskhataMerchantClientFactory` (читает `EskhataMerchantConfigEntity`, расшифровывает hashKey
через `ISecretProtector`, собирает клиент под org). Методы:
- `CreateOrderAsync(invoiceId, amountMinor, currency, description, merchantId, ct)` → `{ OrderId, Qr, InvoiceUrl, RawStatus }`
- `GetOrderStatusAsync(invoiceId, orderId, amountMinor, currency, posId, ct)` → `orderStatus`
- `CancelOrderAsync(...)`, `RefundOrderAsync(...)` (реализуем, UI позже).

Клиент инкапсулирует подпись и `X-CompanyId`. `amount` форматируется инвариантно с 2 знаками
и для тела, и для строки хеша.

### 4.2 PaymentIntent — переиспользуем, минимальная правка
`PaymentIntentEntity` уже держит онлайн-топап (dcgate). Для Eskhata:
- `Method = "eskhata"`.
- `invoiceId` банка = `PaymentIntentId.ToString("N")` (как `ExternalOrderId` у dcgate).
- `GatewayPaymentId` = Eskhata `orderId`.
- `GatewayPayUrl` = `invoiceUrl`.
- **Новая nullable-колонка `GatewayQrPayload`** (текст QR из ответа типа 3) + миграция.
- `posId`, который банк вернёт в ответе, при желании сохраняем (нужен для последующих status/cancel/refund) —
  новая nullable-колонка `GatewayPosId` (int?) + миграция. **Важно:** status-хеш требует `posId`,
  поэтому его надо сохранить с момента create.

### 4.3 Конфиг — правка только что смерженного
- `EskhataMerchantConfigEntity`: поле-ввод `PosId (int)` **заменяем на `MerchantId (int)`**
  (миграция: добавить `MerchantId`, `PosId` оставить как хранилище последнего известного posId или
  удалить — решим в плане; безопаснее оставить nullable и не использовать как ввод).
- Контракты `EskhataMerchantConfigDto` / `UpdateEskhataMerchantConfigRequest`: `posId → merchantId`.
- Эндпоинт `/api/owner/eskhata-config`: валидация `merchantId > 0` вместо `posId`.
- Форма `EskhataGatewayForm`: поле «POS ID» → «Merchant ID»; i18n-ключи (`op.eskhata.posId` → `op.eskhata.merchantId`).
- **Активация:** статус `configured` → `active` появляется, когда провайдер реально включён
  (после успешного тест-заказа против банка). Модель статусов расширим: `inactive | configured | active`.

### 4.4 Приём — мобильный self-topup (Фаза 1)
Расширяем существующий `POST /api/me/wallet/top-up-intent` (сейчас `counter|dcgate`):
- Разрешить `method == "eskhata"`.
- При `eskhata`: резолвим `EskhataMerchantConfig` по org → `factory.Create()` →
  `CreateOrderAsync(...)` → сохраняем `orderId/invoiceUrl/qr/posId` в intent.
- Ответ (`PlayerTopUpIntentDto`) дополняем полями `Qr` и `DeepLink` (строится из `invoiceUrl`:
  `eskhata://pay/<basename(path)>`).

Клиентский хендофф (эталон проверен, зеркалим в Player PWA):
- Мобила + deeplink → `window.location.href = deepLink` (кастомная схема не выгружает вкладку) →
  поллинг статуса раз в 3с + на `visibilitychange` (вернулись из банка) → дедлайн ~5 мин.
- Десктоп → hosted-страница `invoiceUrl` в новой вкладке по клику (иначе popup-блок).

Статус-поллинг: `POST /api/me/wallet/top-up-intents/{id}/status` → вызывает
`GetOrderStatusAsync`, на `COMPLETED` (после верификации) кредитит и возвращает `paid`.

### 4.5 Webhook — новый публичный эндпоинт
`POST /api/public/payments/eskhata/webhook`, rate-limit `player-public` (как dcgate).
Тело: `{ status, code, data:{ orderId, orderStatus, invoiceId, amount, currency, posId } }`.

**Money-safety (webhook приходит БЕЗ подписи — защита только IP allowlist банка):**
1. Игнор всего, кроме `orderStatus == "COMPLETED"` (ack-ом `200`, чтобы банк не ретраил).
2. Резолвим intent по `invoiceId` (= `PaymentIntentId "N"`); проверяем совпадение `orderId` с сохранённым.
3. Если intent уже `fulfilled` → `200` без повторного зачисления (идемпотентность).
4. **Перепроверяем статус запросом `/orders/status`** (там наша подпись) — только `COMPLETED` от API даёт зачисление.
5. Кредит через `CreditOnlineTopUpAsync` с ключом идемпотентности = `PaymentIntentId "N"` →
   `intent.State = "fulfilled"`. Ключ идемпотентности биллинга — авторитетная защита от двойного кредита.

Отдельная webhook-event-таблица (как `DcGateWebhookEvents`) **не нужна**: у Eskhata webhook нет
`EventId` и один тип события (COMPLETED); идемпотентность обеспечивают `intent.State` + ключ биллинга.

Опционально (не для v1): фоновая «жатва» — по спеке при webhook-настройке банк НЕ шлёт неуспехи,
срок жизни платежа сторожим сами. У нас уже есть 24-часовой expiry на intent — переиспользуем.

### 4.6 Киоск/ПК QR (Фаза 2)
Player.Shell (игровой ПК) — экран пополнения показывает `qr` из intent как QR-код (клиентский рендер,
как lookup штрихкода — библиотека рендера уже в UI-стеке; уточнить в плане). Тот же intent/webhook/поллинг.
Оплата подтверждается webhook-ом (игрок платит со своего телефона), UI ПК поллит статус.

---

## 5. Фазировка
- **Фаза 1 — мобильный deeplink self-topup** (максимум переиспользования, быстрый живой money-path):
  клиент+подпись, конфиг→merchantId+активация, `top-up-intent` eskhata-ветка, deeplink, webhook, status-поллинг.
- **Фаза 2 — киоск/ПК QR** (`qr`-рендер в Player.Shell). Один тип заказа → можно слить в одну фазу,
  если Фаза 1 стабильна; решаем в плане.

---

## 6. Безопасность и деньги
- Hash-Key наружу не отдаётся (GET возвращает только `hashKeySet`), пишется шифровано через `ISecretProtector`
  (как apiKey dcgate). Продакшн-ключи только через форму, не в код/логи/чат.
- Аудит: активация/деактивация конфига, тест-заказ — в audit-лог (Hash-Key не логируем, только факт ротации).
- Webhook доверяем только по IP allowlist банка + обязательная перепроверка `/orders/status` перед кредитом.
- Money-path dcgate не изменяется; зачисление Eskhata идёт тем же `CreditOnlineTopUpAsync`.

---

## 7. Тестирование
- **Unit:** сборка подписи (эталонный вектор из доки для типа 1 → должен дать
  `9b9a4663…`; фиксирует алгоритм), Base64 companyId, форматирование amount (2 знака, инвариант).
- **Интеграция об тестовый API** (Фаза 1, вручную/скриптом): create тип 3 → подобрать порядок хеша
  типа 3 (open item), получить `qr`+`invoiceUrl`, оплатить тестовым кошельком, проверить webhook+status.
- **API-тесты:** webhook идемпотентность (двойная доставка → один кредит), COMPLETED без совпадения
  orderId → отказ, неоплаченный → нет кредита.
- Гейты проекта: `bun test` + `bun run build` (Operator/PWA), `dotnet test` (Platform.Api).

---

## 8. i18n
Новые ключи (ru/en/tg, tg — реально таджикский, не копия): `op.eskhata.merchantId` (замена posId),
статус «активен», кнопка «Оплатить», экран QR-пополнения, тексты ошибок приёма. Через `@afk4/i18n`
(ICU), генерация `bun run gen`.

---

## 9. Нужно от пользователя (не блокирует старт кода, нужно к тесту приёма)
1. **`merchantId`** тестового аккаунта = `28652` (получен). `posId` при типе 3 не требуется на ввод.
2. **`CallbackUrl`** webhook прописать в ЛК Eskhata + наш staging-IP в белый список банка.
3. Тестовый `base_url` Merchant API (боевой из доки: `https://online3.eskhata.com:1444/...` — уточнить тестовый).

---

## 10. Открытые пункты (трекать)
- [ ] Порядок склейки хеша для `orderTypeId=3` — определить эмпирически (Фаза 1).
- [ ] Тестовый `base_url` + IP allowlist + CallbackUrl (внешняя зависимость от банка).
- [ ] `PosId` в конфиге: оставить nullable-хранилищем или удалить (решить в плане).
- [ ] QR-рендер в Player.Shell: подтвердить библиотеку (Фаза 2).
