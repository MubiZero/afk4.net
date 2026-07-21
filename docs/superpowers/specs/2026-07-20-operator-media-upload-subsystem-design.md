# Медиа-загрузка (MinIO) — дизайн под-проекта 1

Дата: 2026-07-20
Часть эпика: «Оператор = единая админка» (первый инфра-кирпич)
Затрагивает: `AFK4.Platform.Api` (бэкенд), `AFK4.Operator.App.Web` (UI-компонент), деплой (MinIO bucket)

## Цель

Дать приложению возможность **загружать пользовательские изображения из браузера** (первый
потребитель — логотип клуба; следом — картинки Новостей вместо голого URL). Хранилище — **MinIO**,
который уже есть в архитектуре для раздачи update-пакетов (staging:
`updates.afk4.staging.mubi.dev`). Это переиспользование существующей инфры, не новый сервис.

## Почему MinIO, а не bytea/том

- MinIO уже обслуживает раздачу пакетов (`AFK4.Update.Publisher` → S3/MinIO; агенты качают бинари
  напрямую из публичного MinIO-URL). Хранить медиа там же — консистентно.
- Планируется 20+ клубов: центральный объектный стор масштабируется; раздувать Postgres бинарями
  (bytea) — плохо; том на контейнере не бэкапится с БД и рискован при редеплое.

## Ключевое отличие от раздачи пакетов

Пакеты заливает **релиз-раннер/CI** (publish-side, S3-креды у раннера) — Platform.Api хранит только
подписанные метаданные + URL. Логотип же грузит **оператор из браузера в рантайме**. Значит нужен
новый путь **server-mediated upload**: Platform.Api принимает multipart от аутентифицированного
сотрудника → кладёт объект в media-бакет MinIO → возвращает публичный URL. Presigned-direct-to-MinIO
(браузер грузит напрямую) — НЕ сейчас (сложнее: CORS, presign); отложено до «больших медиа».

## Архитектура

### Хранилище
- Новый **media-бакет**, public-read (как updates-бакет «publicly readable by Agents»):
  `afk4-media-staging` / prod `afk4-media`. **Ops-пререквизит:** провижн бакета в prod MinIO.
- Ключ объекта: `{organizationId}/{branchId}/{mediaId}.{ext}` (mediaId = Guid → URL неугадываем;
  для логотипов/новостей контент и так публичный, поэтому public-read допустим).
- Публичный URL: `{Media:S3:PublicBaseUri}/{objectKey}` — грузится браузером напрямую из MinIO
  (`<img src>`), Platform.Api раздачей НЕ занимается (как агенты качают пакеты напрямую).

### Конфиг (env, по образцу существующих `AFK4_*_MINIO_*`)
`Media:S3:Endpoint`, `Media:S3:Bucket`, `Media:S3:AccessKey`, `Media:S3:SecretKey`,
`Media:S3:PublicBaseUri`, `Media:MaxBytes` (дефолт 10 МБ). Секреты — только через env/секрет-менеджер,
НЕ в коде/репо (инвариант проекта).

### S3-клиент
Рекомендация: добавить `AWSSDK.S3` в `AFK4.Platform.Api` (зрелый, работает с MinIO через
`ServiceURL` + `ForcePathStyle=true`, умеет Put/Delete/при желании presign). В репо сейчас S3
хендлится hand-rolled SigV4 в `AFK4.Update.Publisher` (чтобы не тащить тяжёлый dep в мелкую CLI).
Альтернатива — вынести тот SigV4 в общий хелпер и переиспользовать (без нового nuget). **Развилка на
ревью спеки:** новый dep `AWSSDK.S3` (быстро, надёжно) vs расшарить hand-rolled SigV4 (без dep,
больше своего кода). Рекомендую `AWSSDK.S3`.

### Реестр загруженного (lifecycle)
Таблица `UploadedMediaEntity` (Platform.Api + миграция): `MediaId` (Guid, PK), `OrganizationId`,
`BranchId`, `ObjectKey`, `ContentType`, `SizeBytes`, `PublicUrl`, `Purpose` (`branch-logo` |
`news-image` | …), `CreatedByStaffUserId`, `CreatedAtUtc`. Нужна для: замены (удалить старый объект
при загрузке нового), очистки сирот, аудита. Без неё старые логотипы утекают в бакете.

### Endpoint (Platform.Api)
`POST /api/branches/{branchId}/media` — multipart/form-data, поля: `file`, `purpose`.
- Авторизация: валидный staff-токен + право (для `branch-logo` → `manageBranchSettings`; маппинг
  purpose→permission на сервере, не доверяя клиенту).
- IDOR-guard: `branchId` сверяется со `StaffContext.OrganizationId`/branch (инвариант org-эндпоинтов).
- Валидация: размер ≤ `Media:MaxBytes`; тип по **magic-byte sniff** (не доверять заголовку
  Content-Type): png/jpeg/webp. **SVG — исключаем в v1** (script-вектор; логотип-растр достаточно;
  SVG вернём отдельно с санитайзом, если понадобится).
- Действие: залить в MinIO (private-ключ объекта public-read через bucket-policy), записать
  `UploadedMediaEntity`, вернуть `{ mediaId, url, contentType, sizeBytes }`.
- Kestrel: поднять лимит тела запроса на этом маршруте до `Media:MaxBytes` + запас.

`DELETE /api/branches/{branchId}/media/{mediaId}` — удалить объект + запись (право как на upload).
Замена логотипа: клиент грузит новый → бэкенд по `Purpose=branch-logo` для этого branchId удаляет
прежний объект (или клиент явно вызывает DELETE старого).

### Контракты (`AFK4.Shared.Contracts`)
`UploadedMediaDto(Guid MediaId, string Url, string ContentType, long SizeBytes)`; enum-имена purpose
в `MediaPurposeNames`.

### UI-компонент (`AFK4.Operator.App.Web`)
Переиспользуемый `MediaUpload` (feature-shape): `<input type=file accept="image/png,image/jpeg,
image/webp">` → POST multipart через api-client → превью загруженного (`<img src=url>`) + кнопка
«Удалить». Клиентская валидация типа/размера ДО отправки (мгновенный feedback, <100ms), серверная —
авторитетна. Состояния: idle / uploading (skeleton+прогресс, спиннер отложен 150–300мс) / error
(конкретный текст ошибки от сервера, не generic) / done (превью). Тач-таргеты ≥44px (готовность к
мобильной обёртке).

## Безопасность
- Креды MinIO только через env; в коде/репо/логах — никогда.
- magic-byte sniff вместо доверия Content-Type.
- Лимит размера на сервере (не только в UI).
- Public-read бакет: URL с Guid неугадываем; контент логотипов/новостей публичный по природе →
  приемлемо. Приватных медиа тут нет.
- Право на upload гейтит purpose (branch-logo → `manageBranchSettings`).

## Тестирование
- Platform.Api: тесты endpoint — happy path (S3-клиент замокан), отказ по размеру, отказ по типу
  (magic-byte), IDOR (чужой branchId → 403), отсутствие права → 403, DELETE. S3-интеграция — за
  интерфейсом `IMediaStorage` (мок в юнит-тестах; реальный MinIO — в staging smoke).
- Web: `bun test` на `MediaUpload` (клиентская валидация, состояния, вызов клиента) — bun-моки
  типизировать (build тайпчекает тесты).
- Гейт: серверные тесты + `bun test`/`bun run build` фронта зелёные.

## Вне скоупа (YAGNI)
- Presigned direct-to-MinIO upload (вернём при больших медиа).
- Ресайз/тумбнейлы/CDN.
- Миграция Новостей с URL на upload (отдельная задача — подсистема готовит почву, но News трогаем
  потом).
- SVG-логотипы (v2 с санитайзом).

## Global constraints (наследуются реализацией)
- i18n: строки UI — через `@afk4/i18n`, источник `/locales/{ru,en,tg}.json` + `cd packages/i18n &&
  bun run gen` (messages.ts генерируется); ru/en/tg паритет, tg — настоящий таджикский.
- Деньги/время — не затрагиваются.
- Секреты — только env.
- Фронт: `bun test` (happy-dom), `bun run build` тайпчекает и тесты.
