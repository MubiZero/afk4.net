---
name: operator-club-payments-rethink
description: Рефокус Клуб + Платежи/Лояльности (не косметика). Решения зафиксированы; медиа-загрузка (MinIO) — под-проект 1 первым.
metadata: 
  node_type: memory
  type: project
  originSessionId: 5821be94-92d6-4025-9110-d29b28e7c3be
  modified: 2026-07-22T05:09:41.773Z
---

Первый кирпич эпика [[operator-as-unified-admin-epic]]. Пользователь потребовал не полировку, а
переосмысление «Клуб» и «Платежи и лояльность» (остальное из kit-миграции —
[[operator-laggard-screens-kit-migration]] — остаётся).

**Три под-проекта, каждый со своей спекой→планом:**
1. **Медиа-загрузка (инфра) — ЗАКРЫТА (по словам юзера, 2026-07-21).** MinIO (уже в архитектуре для раздачи update-пакетов —
   `AFK4.Update.Publisher`, staging `updates.afk4.staging.mubi.dev`). Server-mediated upload:
   Platform.Api принимает multipart → media-бакет MinIO (public-read) → возвращает URL. Лимит 10 МБ,
   png/jpeg/webp (SVG v2). Реестр `UploadedMediaEntity` для lifecycle. S3-клиент: рекомендация
   `AWSSDK.S3` (в репо сейчас hand-rolled SigV4 в publisher). Спека:
   `docs/superpowers/specs/2026-07-20-operator-media-upload-subsystem-design.md`. Ops: провижн
   media-бакета в prod MinIO.
2. **Клуб:** ✅ ЗАВЕРШЁН ПО КОДУ (T1-T7 + фикс, HEAD fc3c9fc4, finance ветка `feat/operator-management-redesign`; финальный review ✅ ready-to-merge). Полный профиль (лицо игрока+описание+логотип · адрес/контакты · 7-дневные часы · часовой пояс/язык/валюта-RO), гейт `manageBranchSettings`, превью «как видит игрок». 8 nullable-колонок branches + миграция `AddBranchClubProfile`; `/profile` эндпоинт переведён с `ManageLayout` на `ManageBranchSettings` (был рассинхрон). `logoMediaId` хранится рядом с `logoUrl` И реально проведён в `MediaUpload` (опциональный проп `mediaId`, предпочитается явному URL-парсингу — фикс мёртвого груза из финального review). Часы = JSON, `BranchWorkingHours.Serialize/Deserialize/Validate`; закрытый день шлёт null-времена. Часовой пояс храним, НЕ перепроводим в lease/биллинг. **Пост-review полировка UX (визуальные итерации с юзером):** `contentWidth="full"` + L-раскладка (профиль+превью сверху, часы+настройки во всю ширину под превью); единая сетка полей `minmax(280px)`; валюта = read-only input (не строка под сепаратором); save-bar скруглён + «Отменить» (сброс к baseline; ключ `op.management.save.discard`, добавлен в save-бар `ManagementScreen`); правый gutter 16px у `.management-screen-body`; тайм-инпуты `color-scheme` привязан к `[data-theme]`; лицо игрока = Название½+Логотип½ сверху, Описание textarea во всю ширину снизу; лого-кнопка без hint, высотой `--control-md`; **добавлено поле Instagram** (колонка `branches.Instagram` + миграция `AddBranchInstagram` — ops: применить на staging вместе с `AddBranchClubProfile`); **редактор часов переписан по best-practice** — тумблер работает/выходной + диапазон, закрытый день сворачивается, кнопка «Применить ко всем» (`op.club.hours.applyToAll`). **Гейт merge = визуальная приёмка (как вся ветка).** Follow-up (не блок): серверный allow-list tz/locale; часы через полночь (продуктовое решение); round-trip тест; план=`docs/superpowers/plans/2026-07-20-operator-club-profile-redesign.md`.
**Под-проект 2 — Eskhata Merchant (приём денег в кошелёк) — PHASE 1 BACKEND В MAIN (merge `d6287c9f`, 2026-07-22).**
Реализовано (SDD, 7 кодовых задач, per-task + финальное ревью чистые, 1456/0/13): `EskhataSigner` (SHA-256 hex, вектор доки), `EskhataMerchantClient`+фабрика per-org (create/status тип 3, `GetOrderStatus`→null на любой неуспех), конфиг `posId`→`merchantId` (миграция RenameColumn), PaymentIntent `GatewayQrPayload`/`GatewayPosId`, eskhata-ветка `top-up-intent` (→qr+deeplink `eskhata://pay/<ref>`), публичный webhook `/api/public/payments/eskhata/webhook` (перепроверка `/status` перед кредитом, идемпотентно), player-поллинг `/api/me/wallet/top-up-intents/{id}/eskhata-status`. Кредит через существующий `CreditOnlineTopUpAsync` (dcgate money-path НЕ тронут). **Код ДОРМАНТНЫЙ** — не активен, пока нет доступов банка. **ПОРЯДОК ПОДПИСИ TYPE 3 ПОДТВЕРЖДЁН банком (fix merge `9a73c788`, 2026-07-22):** create = `invoiceId·amount·currency·description·posId·orderTypeId·merchantId` (совпал 1:1 с обкатанным эталоном `~/projects/nj-cosmetics.com` type 2 + merchantId в хвост); status/cancel/refund = `invoiceId·orderId·amount·currency·posId` (уже был верный). В create `posId=0` («банк назначает кассу») идёт в тело и хеш как `"0"` — тест-вектор `a2640b8b…` замораживает порядок. Адреса сред: prod `https://connect.eskhata.com` (резолвится в внутренний `10.10.2.67` — мы в корп-сети банка), тест `https://connecttest.eskhata.com` (A-записи НЕТ отсюда → тестовый сегмент недоступен). **Task 8 ЗАКРЫТ ПО ИСХОДЯЩЕЙ СТОРОНЕ (2026-07-22, живая проба об `connecttest`):** create тип 3 с `posId=0`/`"0"` → `code:0 "Успешно"`, банк создал заказ, назначил кассу (32549), отдал `qr` + `invoiceUrl`; `/status` (`invoiceId·orderId·amount·currency·posId`) → `code:0 COMPLETED`. Конкатенация пробы побайтово = C#-код, т.е. подпись подтверждена эмпирически, не только по спеке. Вариант «без posId» → `-13 нет свободной кассы` (posId=0 — правильный). **Тестовая среда АВТО-завершает заказы** (NEW→COMPLETED за секунды) — удобно для e2e Phase 2/3. **Как достучались до `connecttest`:** он публичный (`162.120.21.10`), но корп-DNS его прячет (split-horizon, NXDOMAIN); вышли через Tailscale exit-node на телефоне (мобильный инет, апрув маршрута в админке TS). prod `connect.eskhata.com`=внутр `10.10.2.67`. **Остался только INBOUND webhook:** CallbackUrl в ЛК Eskhata + IP сервера в allowlist банка — но polling-путь (`/status`) уже подтверждён, так что зачисление можно гнать и без webhook. Тест-креды (companyId=`5107ba47-…913bb`, merchantId=28652, hashKey=`4f7f…40b`) — ТОЛЬКО тест; **Phase 2** = Customer.Web PWA (кнопка deeplink `initiateEskhata`, эталон `~/projects/nj-cosmetics.com`); **Phase 3** = Player.Shell.Web киоск (QR-рендер `TopUpScreen.tsx`) — обе отдельными планами ПОСЛЕ Task 8. Deferred Minor'ы (не блок): двойной запрос config за merchantId (T5), webhook 500-на-структурно-странный-JSON (T6, fail-safe), null-guard/coverage (T1/T2). План=`docs/superpowers/plans/2026-07-21-eskhata-merchant-acceptance.md`.

**Под-проект 2 (историческое) — СПЕКА+ПЛАН делались на ветке `feat/eskhata-merchant-acceptance`.**
Eskhata = ОСНОВНОЙ онлайн-приём, DC = вторичный/полуручной (под-проект 3). Решение: **`orderTypeId=3` (DynamicPos)** — один
create даёт `qr` (киоск/ПК) И `invoiceUrl` (→ deeplink `eskhata://pay/<ref>` для мобилы), банк сам назначает кассу, нужен
`merchantId` (тест=`28652`, posId НЕ вводится). **Эталон целиком есть в `~/projects/nj-cosmetics.com`** (Laravel, обкатан на
живом API): подпись = голый `SHA256(concat(values)+"."+hashKey)` hex (НЕ HMAC!); `X-CompanyId`=Base64(companyId); webhook БЕЗ
подписи → перепроверять `/orders/status` перед кредитом; deeplink = last-segment invoiceUrl. Переиспользуем money-path dcgate:
`PaymentIntentEntity` (`Method="eskhata"`, invoiceId=PaymentIntentId), webhook→`CreditOnlineTopUpAsync` (идемпотентно по intent-id),
dcgate money-path НЕ трогаем (freeze не нарушен). Уже в репо: только конфиг (`EskhataMerchantConfigEntity`+`/api/owner/eskhata-config`+
`EskhataGatewayForm`), самого приёма нет. **Единственная неизвестность — порядок хеша для типа 3** (эталон использует тип 2 с posId;
док-пример только тип 1) → эмпирически об тестовый endpoint (Task 8). План=8 задач TDD, Phase 1 = бэкенд money-core (тестируется API-
тестами без UI); Phase 2 (Customer.Web PWA deeplink) + Phase 3 (Player.Shell.Web киоск QR) — отдельными планами ПОСЛЕ валидации хеша.
Спека `docs/superpowers/specs/2026-07-21-eskhata-merchant-acceptance-design.md`, план `docs/superpowers/plans/2026-07-21-eskhata-merchant-acceptance.md`.
Тест-доступы юзер прислал в чат (ТОЛЬКО тест: org `5107ba47-fc70-4180-ae2d-18a8eee913bb`, merchID 28652, hash 4f7f…) — прод-ключи только через форму, не в чат/код.
Ждёт: тестовый base_url + CallbackUrl в ЛК банка + staging-IP в белый список. Тесты Platform.Api: образцы `DcGateWebhookEndpointTests`/`DcGateClientTests`/`EskhataConfigEndpointsTests` (`PlatformApiFactory`,`TestIds`,`StaffAuthTestHelper`,`StubHandler`).

3. **Платежи/Лояльность:** структурная расшивка из табов — в main (84aa3a8b). **ВИЗУАЛЬНЫЙ РЕДИЗАЙН
   /interface-limb — СДЕЛАН, ПРИНЯТ юзером и СМЕРЖЕН в main (PR #129, squash `c7920389`).** Durable-урок (3 захода): (1) band+зоны поверх форм → «полная хуйня»;
   (2) язык флагманских вкладок Карта/Касса (концепты A cockpit / B карта-потока, делал 2 sonnet-агента
   HTML-макетами) → «оверкомпликейтед для экрана, куда заходят раз в месяцы»; (3) ПРИНЯТО — **спокойный
   самообъясняющий setup-экран**. **КЛЮЧ: контекст решает (interface-limb #30) — редко-посещаемый setup
   ≠ ежедневный рабочий инструмент; НЕ копировать язык daily-вкладок на rare-config; приоритет спокойствие/
   воздух/человеческие лид-пояснения (юзер забывает как работает между визитами). Проблема была не «форма»,
   а плохо сделанная форма.** Реализация: `PaymentsSetupSection` (иконка+заголовок+лид) × 2 секции в
   `contentWidth="form"`; приём = карты-строки со статус-чипами `.ui-chip` + пунктирная «Добавить карту»
   (раскрывает форму) + Telegram-привязка в строке; Eskhata = тихий резервный блок с disclosure «Настроить»;
   возврат = правила с тактильным switch + живой пример + лимиты. CSS `styles/24-payments-setup.css`
   (namespace `payset-*`, переиспользует `.ui-chip`/`.ui-btn`/токены). Данные/бэкенд/`useLoyaltySettings`
   не тронуты. 16/16 тестов + build зелёные. Спека `docs/superpowers/specs/2026-07-21-operator-payments-loyalty-flow-map-redesign.md`.
   Флейта: `bun test` оператора флакает 1 тест (process-wide моки), повторный прогон чист.

Решения-док: `docs/superpowers/specs/2026-07-20-operator-club-payments-rethink-decisions.md`.
Статус: спека под-проекта 1 написана, ждёт ревью пользователя → writing-plans. Экраны (2/3) —
детальная спека перед их очередью. Всё адаптивно (тач ≥44px) под будущую мобильную обёртку.
