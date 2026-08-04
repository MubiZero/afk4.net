# Восстановление доступа при полной потере второго фактора платформенного администратора

Второй фактор (2FA) обязателен для всех аккаунтов `platform_admin_users` (панель
Platform Control). Сбросить чужой второй фактор умеет только другой активный
администратор с ролью `platform_admin` (permission `ManagePlatformAdmins`,
эндпоинт `POST /api/platform/admins/{platformAdminUserId}/2fa/reset` →
`PlatformAdminTwoFactorService.ResetAsync`). Если такого администратора не
осталось — например, единственный `platform_admin` потерял одновременно
телефон с приложением-аутентификатором и все коды восстановления — панель
Platform Control становится недоступна никому, потому что войти без второго
фактора нельзя, а сбросить его тоже некому.

Это единственный официально поддерживаемый способ выйти из такой ситуации:
прямое вмешательство в базу данных Platform API кем-то с доступом к
production/staging PostgreSQL.

## Как выглядит ситуация

- Администратор проходит первый шаг входа (`POST /api/platform/auth/sign-in`),
  получает челлендж второго шага, но не может подтвердить его: нет доступа ни
  к TOTP-приложению, ни к одному из резервных кодов восстановления.
- В таблице `platform_admin_users` нет ни одной другой активной строки с
  ролью `platform_admin` (роль хранится в JSON-колонке `RolesJson`), которая
  могла бы выполнить сброс через API.
- Через API решить проблему невозможно: `PlatformAdminAuthorizationService`
  требует действующую сессию, а её получить нельзя без прохождения 2FA.

Проверить, что ситуация действительно такая, можно одним запросом — он
показывает всех активных `platform_admin` и включён ли у них второй фактор:

```sql
SELECT
    "PlatformAdminUserId",
    "UserName",
    "IsActive",
    "RolesJson",
    "TotpEnabledAtUtc" IS NOT NULL AS totp_enabled
FROM platform_admin_users
WHERE "IsActive" = TRUE
  AND "RolesJson" @> '["platform_admin"]'::jsonb;
```

Если в выдаче нет ни одной строки с `totp_enabled = false` или `true`, но
доступной для входа (то есть у пострадавшего администратора это единственная
активная запись с ролью `platform_admin`) — переходите к шагам ниже.

## Прямой сброс второго фактора в базе

Выполняется вручную, с прямым доступом к PostgreSQL (`psql` или любой клиент
с правами записи в `platform_admin_users`). Перед изменением production-базы
сделайте бэкап (см. `docs/operations/postgres-backup-restore.md`).

1. Найдите точный `PlatformAdminUserId` пострадавшего администратора по
   логину:

   ```sql
   SELECT "PlatformAdminUserId", "UserName", "DisplayName", "IsActive"
   FROM platform_admin_users
   WHERE "NormalizedUserName" = UPPER('<логин администратора>');
   ```

2. Сбросьте второй фактор этой записи — обнулите зашифрованный TOTP-секрет,
   отметку о включении, коды восстановления и счётчики неудачных попыток.
   Это ровно тот же набор полей, который трогает штатный API-эндпоинт сброса
   (`PlatformAdminTwoFactorService.ResetAsync`), только выполненный SQL-ом
   напрямую:

   ```sql
   UPDATE platform_admin_users
   SET
       "TotpSecretEncrypted" = NULL,
       "TotpEnabledAtUtc" = NULL,
       "RecoveryCodeHashesJson" = '[]',
       "FailedTwoFactorAttempts" = 0,
       "TwoFactorLockedUntilUtc" = NULL,
       "UpdatedAtUtc" = now()
   WHERE "PlatformAdminUserId" = '<PlatformAdminUserId из шага 1>';
   ```

3. (Опционально, но рекомендуется) закройте все выданные ранее токены сессии
   этого администратора, чтобы старые access/refresh-токены не остались
   действующими вперемешку с новым проходом 2FA:

   ```sql
   DELETE FROM platform_admin_access_tokens WHERE "PlatformAdminUserId" = '<PlatformAdminUserId>';
   DELETE FROM platform_admin_refresh_tokens WHERE "PlatformAdminUserId" = '<PlatformAdminUserId>';
   ```

4. (Опционально) удалите зависшие незавершённые челленджи входа этого
   администратора, чтобы не путались со свежей попыткой:

   ```sql
   DELETE FROM platform_admin_sign_in_challenges WHERE "PlatformAdminUserId" = '<PlatformAdminUserId>';
   ```

После выполнения шага 2 у записи `TotpEnabledAtUtc IS NULL`, то есть с точки
зрения бэкенда второй фактор у администратора не настроен вообще — так же,
как у только что принявшего приглашение аккаунта.

## Как пройти настройку заново

1. Администратор заходит в панель Platform Control обычным логином и
   паролем (`POST /api/platform/auth/sign-in`).
2. Поскольку `TotpEnabledAtUtc` теперь `NULL`, сервер вернёт
   `twoFactorConfigured: false`, и панель откроет мастер настройки второго
   фактора (`TwoFactorSetup`) вместо запроса кода.
3. Администратор сканирует новый QR-код (или вводит секрет вручную) в
   приложении-аутентификаторе, подтверждает код и сохраняет новый набор
   резервных кодов восстановления в надёжном месте (менеджер паролей,
   сейф — не в текстовом файле в репозитории).

После этого доступ восстановлен, все данные (роль, привязка к организациям,
права) сохранились — менялся только второй фактор.

## Как этого не допустить

Держите **не менее двух** активных администраторов с ролью `platform_admin`
одновременно. Пока жив хотя бы один второй `platform_admin` с рабочим вторым
фактором, сброс делается штатно через панель
(`PlatformAdminTwoFactorService.ResetAsync`, permission
`ManagePlatformAdmins`) без прямого вмешательства в базу и без простоя. Этот
раздел — крайняя мера на случай, если правило не соблюдалось и восстанавливать
уже нечем.
