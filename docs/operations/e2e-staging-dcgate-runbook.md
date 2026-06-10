# E2E staging runbook — shared AFK4 Telegram app + dcgate online top-up

Живой прогон оплаты реальной картой на staging. Owner-кабинет живёт в десктопном
WPF (Operator.App.Web), которого на staging нет, поэтому e2e гоняется через API/curl.

## Константы (staging, на 2026-06-08)

```
API       = https://afk4.staging.mubi.dev
OWNER     = e2eowner / E2eOwner!2026
ORG       = 0169044b-2f74-46a7-8e52-7656a39a8f8c
GATEWAY   = 93eda272-93b8-40be-931f-40618cc0a5d2   (dcgate project cmq50ockc0000nw01ltbhfegp, card •1953)
PLAYER    = +992900000001 / PIN 112233
```

Секреты (телефон Telegram-аккаунта, карта) НЕ хранить в этом файле — передаются по
месту и используются эфемерно. Общие `api_id`/`api_hash` приложения AFK4 живут в env
самого dcgate (`TELEGRAM_API_ID` / `TELEGRAM_API_HASH`), afk4 их не передаёт.

## 0. Owner login → токен

```bash
curl.exe -s -X POST "$API/api/auth/staff/sign-in-by-login" \
  -H "Content-Type: application/json" \
  --data-binary '{"login":"e2eowner","password":"E2eOwner!2026"}'
# → accessToken (живёт ~8 ч). Сохрани в OWNER_TOKEN.
```

## 1. Привязка Telegram-сессии к шлюзу

Передаём только телефон Telegram-аккаунта, который получает банковские уведомления по
карте •1953. Логин в Telegram dcgate выполняет общим приложением AFK4 (`api_id`/`api_hash`
из своего env) — отдельные ключи на владельца больше не нужны.

```bash
# start: передаём только телефон
curl.exe -s -X POST "$API/api/owner/payment-gateways/$GATEWAY/telegram/start" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  --data-binary '{"phone":"+992XXXXXXXXX"}'
# → {"loginAttemptId":"...","state":"code_required"}  (или state:"attached" если уже привязан → шаг 2 пропустить)
```

Telegram пришлёт код в приложение.

```bash
# verify-code
curl.exe -s -X POST "$API/api/owner/payment-gateways/$GATEWAY/telegram/verify-code" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  --data-binary '{"loginAttemptId":"<ID>","code":"<КОД_ИЗ_TELEGRAM>"}'
# → state:"attached" | "password_required"

# verify-password (только если включён 2FA / cloud password)
curl.exe -s -X POST "$API/api/owner/payment-gateways/$GATEWAY/telegram/verify-password" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  --data-binary '{"loginAttemptId":"<ID>","password":"<2FA_PASSWORD>"}'
# → state:"attached"
```

Когда state=attached, шлюз автоматически переходит pending_telegram → active.

```bash
# проверка статуса
curl.exe -s "$API/api/owner/payment-gateways" -H "Authorization: Bearer $OWNER_TOKEN"
# → gateways[0].status == "active"
```

## 2. Рестарт воркера dcgate

Чтобы сессия поднялась online (см. dcgate-проект cmq50ockc на Coolify — restart application).

## 3. Player: top-up через dcgate → реальная оплата

```bash
# player login
curl.exe -s -X POST "$API/api/public/player/sign-in" \
  -H "Content-Type: application/json" \
  --data-binary '{"organizationId":"0169044b-2f74-46a7-8e52-7656a39a8f8c","phoneNumber":"+992900000001","password":"112233"}'
# → accessToken → PLAYER_TOKEN

# баланс ДО (ожидаем 0)
curl.exe -s "$API/api/me/dashboard" -H "Authorization: Bearer $PLAYER_TOKEN"
# → walletBalance.amountMinorUnits

# top-up-intent, method=dcgate (сумма в МИНОРНЫХ единицах; 10 TJS = 1000)
curl.exe -s -X POST "$API/api/me/wallet/top-up-intent" \
  -H "Authorization: Bearer $PLAYER_TOKEN" -H "Content-Type: application/json" \
  --data-binary '{"amountMinorUnits":1000,"currencyCode":"TJS","method":"dcgate"}'
# → payUrl  ← открыть и реально оплатить картой
```

## 4. Вебхук → зачисление

После реальной оплаты dcgate шлёт `payment.paid` на webhook → баланс растёт.

```bash
# баланс ПОСЛЕ (ожидаем +сумму)
curl.exe -s "$API/api/me/dashboard" -H "Authorization: Bearer $PLAYER_TOKEN"

# история интентов (state должен стать fulfilled)
curl.exe -s "$API/api/me/wallet/top-up-intents" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Успех = walletBalance вырос на сумму пополнения и интент в state=fulfilled.
