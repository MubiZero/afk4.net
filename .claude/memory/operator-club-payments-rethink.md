---
name: operator-club-payments-rethink
description: Рефокус Клуб + Платежи/Лояльности (не косметика). Решения зафиксированы; медиа-загрузка (MinIO) — под-проект 1 первым.
metadata: 
  node_type: memory
  type: project
  originSessionId: 5821be94-92d6-4025-9110-d29b28e7c3be
  modified: 2026-07-21T05:07:02.966Z
---

Первый кирпич эпика [[operator-as-unified-admin-epic]]. Пользователь потребовал не полировку, а
переосмысление «Клуб» и «Платежи и лояльность» (остальное из kit-миграции —
[[operator-laggard-screens-kit-migration]] — остаётся).

**Три под-проекта, каждый со своей спекой→планом:**
1. **Медиа-загрузка (инфра, ПЕРВЫМ):** MinIO (уже в архитектуре для раздачи update-пакетов —
   `AFK4.Update.Publisher`, staging `updates.afk4.staging.mubi.dev`). Server-mediated upload:
   Platform.Api принимает multipart → media-бакет MinIO (public-read) → возвращает URL. Лимит 10 МБ,
   png/jpeg/webp (SVG v2). Реестр `UploadedMediaEntity` для lifecycle. S3-клиент: рекомендация
   `AWSSDK.S3` (в репо сейчас hand-rolled SigV4 в publisher). Спека:
   `docs/superpowers/specs/2026-07-20-operator-media-upload-subsystem-design.md`. Ops: провижн
   media-бакета в prod MinIO.
2. **Клуб:** ✅ ЗАВЕРШЁН ПО КОДУ (T1-T7 + фикс, HEAD fc3c9fc4, finance ветка `feat/operator-management-redesign`; финальный review ✅ ready-to-merge). Полный профиль (лицо игрока+описание+логотип · адрес/контакты · 7-дневные часы · часовой пояс/язык/валюта-RO), гейт `manageBranchSettings`, превью «как видит игрок». 8 nullable-колонок branches + миграция `AddBranchClubProfile`; `/profile` эндпоинт переведён с `ManageLayout` на `ManageBranchSettings` (был рассинхрон). `logoMediaId` хранится рядом с `logoUrl` И реально проведён в `MediaUpload` (опциональный проп `mediaId`, предпочитается явному URL-парсингу — фикс мёртвого груза из финального review). Часы = JSON, `BranchWorkingHours.Serialize/Deserialize/Validate`; закрытый день шлёт null-времена. Часовой пояс храним, НЕ перепроводим в lease/биллинг. **Пост-review полировка UX (визуальные итерации с юзером):** `contentWidth="full"` + L-раскладка (профиль+превью сверху, часы+настройки во всю ширину под превью); единая сетка полей `minmax(280px)`; валюта = read-only input (не строка под сепаратором); save-bar скруглён + «Отменить» (сброс к baseline; ключ `op.management.save.discard`, добавлен в save-бар `ManagementScreen`); правый gutter 16px у `.management-screen-body`; тайм-инпуты `color-scheme` привязан к `[data-theme]`; лицо игрока = Название½+Логотип½ сверху, Описание textarea во всю ширину снизу; лого-кнопка без hint, высотой `--control-md`; **добавлено поле Instagram** (колонка `branches.Instagram` + миграция `AddBranchInstagram` — ops: применить на staging вместе с `AddBranchClubProfile`); **редактор часов переписан по best-practice** — тумблер работает/выходной + диапазон, закрытый день сворачивается, кнопка «Применить ко всем» (`op.club.hours.applyToAll`). **Гейт merge = визуальная приёмка (как вся ветка).** Follow-up (не блок): серверный allow-list tz/locale; часы через полночь (продуктовое решение); round-trip тест; план=`docs/superpowers/plans/2026-07-20-operator-club-profile-redesign.md`.
3. **Платежи/Лояльность:** один экран «деньги↔игрок» без табов (Наличные/Перевод-на-карту/Eskhata +
   лояльность-карточки), каждая секция сама сохраняется, по правам. Переиспользует карточки
   лояльности и kit-шлюзы из миграции.

Решения-док: `docs/superpowers/specs/2026-07-20-operator-club-payments-rethink-decisions.md`.
Статус: спека под-проекта 1 написана, ждёт ревью пользователя → writing-plans. Экраны (2/3) —
детальная спека перед их очередью. Всё адаптивно (тач ≥44px) под будущую мобильную обёртку.
