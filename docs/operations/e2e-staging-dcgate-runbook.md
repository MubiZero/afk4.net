# E2E staging runbook — shared AFK4 Telegram app + dcgate online top-up

Живой прогон оплаты реальной картой на staging. Кабинет владельца организации
живёт в Organization Admin, поэтому e2e можно выполнить через API/curl.

## Константы (staging, на 2026-06-08)

```
API       = https://api.afk4.net
OWNER     = <smoke login; password comes from the secret manager>
ORGANIZATION_ID = <smoke organization UUID>
GATEWAY   = <smoke gateway UUID>
PLAYER    = <smoke phone; PIN comes from the secret manager>
```

Секреты (телефон Telegram-аккаунта, карта) НЕ хранить в этом файле — передаются по
месту и используются эфемерно. Общие `api_id`/`api_hash` приложения AFK4 живут в env
самого dcgate (`TELEGRAM_API_ID` / `TELEGRAM_API_HASH`), afk4 их не передаёт.

## 0. Organization Owner login → токен

```bash
curl.exe -s -X POST "$API/api/organizations/$ORGANIZATION_ID/auth/staff/sign-in-by-login" \
  -H "Content-Type: application/json" \
  -H "X-AFK4-Product: organization-admin" \
  -H "X-AFK4-Compatibility-Epoch: 2" \
  -H "X-AFK4-Client-Version: 0.2.0" \
  --data-binary "{\"login\":\"$SMOKE_LOGIN\",\"password\":\"$SMOKE_PASSWORD\"}"
# → accessToken (живёт ~8 ч). Сохрани в OWNER_TOKEN.
```

## 1. Привязка Telegram-сессии к шлюзу

Передаём только телефон Telegram-аккаунта, который получает банковские уведомления по
карте •1953. Логин в Telegram dcgate выполняет общим приложением AFK4 (`api_id`/`api_hash`
из своего env) — отдельные ключи на владельца больше не нужны.

```bash
# start: передаём только телефон
curl.exe -s -X POST "$API/api/organizations/$ORGANIZATION_ID/payment-gateways/$GATEWAY/telegram/start" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  -H "X-AFK4-Product: organization-admin" -H "X-AFK4-Compatibility-Epoch: 2" -H "X-AFK4-Client-Version: 0.2.0" \
  --data-binary '{"phone":"+992XXXXXXXXX"}'
# → {"loginAttemptId":"...","state":"code_required"}  (или state:"attached" если уже привязан → шаг 2 пропустить)
```

Telegram пришлёт код в приложение.

```bash
# verify-code
curl.exe -s -X POST "$API/api/organizations/$ORGANIZATION_ID/payment-gateways/$GATEWAY/telegram/verify-code" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  -H "X-AFK4-Product: organization-admin" -H "X-AFK4-Compatibility-Epoch: 2" -H "X-AFK4-Client-Version: 0.2.0" \
  --data-binary '{"loginAttemptId":"<ID>","code":"<КОД_ИЗ_TELEGRAM>"}'
# → state:"attached" | "password_required"

# verify-password (только если включён 2FA / cloud password)
curl.exe -s -X POST "$API/api/organizations/$ORGANIZATION_ID/payment-gateways/$GATEWAY/telegram/verify-password" \
  -H "Authorization: Bearer $OWNER_TOKEN" -H "Content-Type: application/json" \
  -H "X-AFK4-Product: organization-admin" -H "X-AFK4-Compatibility-Epoch: 2" -H "X-AFK4-Client-Version: 0.2.0" \
  --data-binary '{"loginAttemptId":"<ID>","password":"<2FA_PASSWORD>"}'
# → state:"attached"
```

Когда state=attached, шлюз автоматически переходит pending_telegram → active.

```bash
# проверка статуса
curl.exe -s "$API/api/organizations/$ORGANIZATION_ID/payment-gateways" -H "Authorization: Bearer $OWNER_TOKEN" \
  -H "X-AFK4-Product: organization-admin" -H "X-AFK4-Compatibility-Epoch: 2" -H "X-AFK4-Client-Version: 0.2.0"
# → gateways[0].status == "active"
```

## 2. Рестарт воркера dcgate

Чтобы сессия поднялась online (см. dcgate-проект cmq50ockc на Coolify — restart application).

## 3. Player: top-up через dcgate → реальная оплата

```bash
# player login
curl.exe -s -X POST "$API/api/public/player/sign-in" \
  -H "Content-Type: application/json" \
  --data-binary "{\"organizationId\":\"$ORGANIZATION_ID\",\"phoneNumber\":\"$PLAYER_PHONE\",\"password\":\"$PLAYER_PIN\"}"
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
