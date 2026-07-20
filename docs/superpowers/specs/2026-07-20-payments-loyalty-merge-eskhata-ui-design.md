# Объединение «Оплата» + «Лояльность» и UI-подготовка шлюза Eskhata

Дата: 2026-07-20
Ветка: `feat/operator-management-redesign`
Статус: одобрено (brainstorming), готово к реализации

## 1. Цель и контекст

В разделе «Управление» два тонких пункта — «Оплата» (провижининг dcgate-карт + привязка
Telegram) и «Лояльность» (правила кэшбэка) — объединяются в один раздел с вкладками. Параллельно
готовится UI под будущую интеграцию банковского шлюза **Eskhata Merchant** (официальная замена
серому userbot-механизму dcgate).

Три куска запроса:
- **A.** Объединить «Оплату» и «Лояльность» в один раздел с вкладками.
- **B.** Убрать из лояльности панель «Как это работает» (собрана ранее, теперь не нужна при мердже).
- **C.** Добавить тип шлюза Eskhata — **только UI + хранение реквизитов** (не приём платежей).

### Что НЕ входит в слайс (отложенный эпик Eskhata)
Реализация Merchant API (подпись SHA-256, создание счёта, статус, webhook), flow пополнения
кошелька, кредитование ledger, deep link. Требует реальных тест-реквизитов банка, согласования
webhook-адреса/IP-allowlist. Референс механики — `nj-cosmetics.com/docs/integrations/eskhata-merchant.md`
(другой проект: интернет-магазин с заказами; у нас пополнение кошелька — flow проектируется заново).

### Инвариант безопасности (money-path freeze)
Онлайн money-path (dcgate: карта + Telegram-userbot) **заморожен внешним bank-bot**. Слайс только
**добавляет** новую сущность + эндпоинт для конфига Eskhata. dcgate, billing, ledger, checkout —
не трогаются. Секрет hash key шифруется тем же `ISecretProtector`, что и dcgate apiKey.

## 2. Навигация (кусок A)

`managementNav.ts`: пункты `payment` и `loyalty` заменяются одним `payments` →
«Платежи и лояльность». Рейл 9→8 пунктов.

- Права раздела = объединение: виден при `managePaymentGateways` **или** `manageLoyaltySettings`.
- Вкладки гейтятся по правам:
  - «Платёжные шлюзы» — `managePaymentGateways`;
  - «Лояльность» — `manageLoyaltySettings`.
- Дефолтная активная вкладка = первая доступная по правам. Если у сессии только одно право —
  видна только одна вкладка (без пустой второй). Право не размывается.

`ManagementDestinationId`: убрать `'payment' | 'loyalty'`, добавить `'payments'`.

## 3. Контейнер (архитектура фронта, куски A + B)

Новый `PaymentsLoyaltyDestination` по эталону `HallsDevicesDestination`: владеет `ManagementScreen`
и таб-стрипом (`.mgmt-tabs`, `role="tablist"`).

- Вкладка **«Платёжные шлюзы»**: `contentWidth="wide"`, **без** save-бара. Содержит существующий
  `PaymentGatewaysWorkspace` (карты dcgate + Telegram) и новый блок `EskhataGatewayForm` (своя
  кнопка «Сохранить» — модель «мгновенных действий»).
- Вкладка **«Лояльность»**: `contentWidth="form"`, **с** save-баром. Логика лояльности выносится
  из `LoyaltyDestination` в `LoyaltyTab` (без своего `ManagementScreen`). Save-состояние
  (`SaveState` + `onSave` + `dirty`) поднимается в контейнер; контейнер передаёт `save` в
  `ManagementScreen` **только когда активна вкладка лояльности** → конфликт save-моделей снят.
- **Кусок B:** `LoyaltyTab` теряет `aside.loyalty-preview` («Как это работает»), калькулятор
  `previewCashback`, sample-константы, `resultText`, `examples`, импорт `formatMinorUnits`.
  Возврат к компактной одноколоночной форме (правила + лимиты стопкой). Удаляются:
  - i18n-ключи `op.loyalty.preview.*` (7 ключей × 3 локали). `op.loyalty.rules.title` **остаётся**
    как заголовок блока правил в компактной форме (парный к `op.loyalty.limits.title`);
  - CSS `.loyalty-grid`, `.loyalty-settings`, `.loyalty-preview*` и связанные.
  - Дев-мок loyalty-settings и percentShort/aria-дедуп процента — **оставляем** (это фикс NaN и
    дедуп подписи, полезны сами по себе).
- `LoyaltyDestination.tsx` удаляется (логика переехала в `LoyaltyTab`); `PaymentDestination.tsx`
  удаляется (обёртка переехала в контейнер).

## 4. Бэкенд Eskhata (кусок C, только хранение)

Отдельная сущность (не расширяем `BranchPaymentGatewayEntity` — у dcgate форма карты, у Eskhata
форма мерчанта; полиморфные колонки = грязь):

`EskhataMerchantConfigEntity`:
- `Guid EskhataMerchantConfigId`
- `Guid OrganizationId`
- `Guid? BranchId` (null = org-fallback, как dcgate)
- `string BaseUrl` (без хвостового `/`)
- `string CompanyId` (в исходном виде, base64 — забота будущего клиента)
- `int PosId`
- `string HashKeyEncrypted` (через `ISecretProtector`)
- `string Status` — `configured` | `inactive`. **Никогда `active` в этом слайсе.**
- `DateTimeOffset CreatedAtUtc`, `UpdatedAtUtc`

Миграция `AddEskhataMerchantConfig` (после `dotnet build`, `--no-build`).

Эндпоинт по эталону `LoyaltySettingsEndpoints` (owner-scope, IDOR-guard через
`StaffContext.OrganizationId`):
- `GET /api/owner/eskhata-config` → DTO: `baseUrl, companyId, posId, hashKeySet: bool, status`.
  **Секрет не возвращается** (как dcgate не возвращает apiKey) — только `hashKeySet`.
- `POST /api/owner/eskhata-config` → сохраняет. `HashKey` в запросе опционален: пустой = оставить
  существующий, непустой = перешифровать и заменить. При заполнении всех 4 полей `Status` →
  `configured`, иначе `inactive`.

Права: `managePaymentGateways` (тот же гейт, что dcgate).

Валидация (по §6.2/§7 доки):
- все четыре реквизита обязательны для `status=configured`;
- `PosId` > 0;
- `BaseUrl` — абсолютный http(s) URL без хвостового `/`;
- при первом сохранении (нет существующего секрета) `HashKey` обязателен.

Контракты: `AFK4.Shared.Contracts.Payments` — `EskhataMerchantConfigDto`,
`UpdateEskhataMerchantConfigRequest`.

## 5. Форма Eskhata (UI внутри вкладки «Платёжные шлюзы»)

`EskhataGatewayForm` под списком dcgate-карт:
- поля: Base URL, Company ID, POS ID (числовой), Hash key (password-инпут; при `hashKeySet`
  показывает плейсхолдер «задан», не раскрывая значение; ввод = замена);
- своя кнопка «Сохранить» (busy/disabled по валидности);
- чип статуса **«настроен · не активен»** при `status=configured`;
- заметка: приём платежей включится после согласования реквизитов с банком (честно про
  half-presence, лень-абстракция не прячет статус).

Дев-мок: GET/POST `/owner/eskhata-config` (реалистичные значения, POST-эхо), как сделано для
loyalty-settings.

## 6. Тесты + гейт

Бэкенд (xUnit + InMemory через `PlatformApiFactory`):
- round-trip эндпоинта: сохранение, маскировка секрета (GET не отдаёт hash key, `hashKeySet=true`),
  «пустой hash key при апдейте оставляет старый»;
- валидация: 4 обязательных поля, `PosId>0`, невалидный URL, первый сейв без hash key;
- IDOR-guard (чужая org → 403/404).

Фронт (`bun test`, happy-dom):
- `PaymentsLoyaltyDestination`: переключение вкладок, гейтинг вкладок по правам, save-бар только
  на вкладке лояльности, дефолт = первая доступная вкладка;
- `EskhataGatewayForm`: валидность → доступность кнопки, сохранение, отображение «задан»;
- `LoyaltyTab`: сохранение процентов в bps (перенос существующих тестов), отсутствие preview.

i18n: паритет (новые ключи `op.management.dest.payments*`, `op.payments.tab.*`, `op.eskhata.*`;
удаление `op.loyalty.preview.*`), реальный таджикский (guard `tg!==ru`).

Полный гейт: `bun test` (оператор) + `bun run build` (tsc -b тайпчекает тесты) + i18n `bun test`
+ `dotnet test` (Platform.Api). App.test обновить под исчезнувшие destination-id `payment`/`loyalty`.

## 7. Порядок реализации (слайсы)

1. Бэкенд Eskhata: сущность + миграция + контракты + эндпоинт + тесты.
2. Дев-мок eskhata-config.
3. Навигация: `payments` вместо `payment`+`loyalty` (+ i18n раздела/вкладок).
4. Контейнер `PaymentsLoyaltyDestination` + вынос `LoyaltyTab` (кусок B: снять preview) +
   подключение save-модели по активной вкладке.
5. `EskhataGatewayForm` во вкладке шлюзов.
6. Чистка: удалить `PaymentDestination.tsx`, `LoyaltyDestination.tsx`, preview-i18n/CSS.
7. Тесты фронта + полный гейт + PR.
