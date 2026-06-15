---
name: operator-wizard-auth-phone-first
description: Operator+Wizard auth unified to phone-first; org-scoped sign-in now accepts verified phone; forgot-password copy unified & anti-enumeration. Merged PR
metadata: 
  node_type: memory
  type: project
  originSessionId: 16d55fb3-a7fa-4e36-af4f-29c4ec7b2325
---

Auth-флоу Operator и Wizard приведены к единому виду. **Merged to main via PR #82 (`6cec0b8`), 2026-06-14.**

- **Operator вход — телефон-first** (поле `+992` + маска `93 738 00 70`) + тихий переключатель на логин/почту. Тот же org-scoped эндпоинт `/api/auth/staff/sign-in` теперь резолвит **по подтверждённому телефону внутри клуба** в дополнение к username/email — `ResolveOrgUserAsync` в `PasswordHashingStaffCredentialService` (порядок: username → email → verified phone). Новых эндпоинтов НЕ заводили (в отличие от глобальной discovery-схемы Wizard с выбором клуба).
- **Бренд:** в навбаре Operator лого во всю высоту + «Оператор» справа за тонким разделителем; знак-логотип (`favicon.svg`, command-grid) над заголовком на экранах входа и сброса. У Wizard бренд-лок-ап в титлбаре уже был.
- **Копи сброса (общие ключи `auth.forgot.*`, лечит ВСЕ фронтенды разом):** обе кнопки запроса кода → «Получить код»; подтверждение короткое и одинаковое независимо от существования аккаунта.
- **Анти-enumeration уже на бэке:** `/forgot-password` и `/forgot-password-by-phone` всегда возвращают 200 для валидного ввода; реальная отправка email/SMS гейтится — уходит ТОЛЬКО известному (verified+active) аккаунту. Текст-подтверждение поэтому можно держать коротким без утечки.
- **Телефон-хелперы:** `localPhoneDigits/formatLocal/fullPhoneDigits` вынесены в **по-приложенчески** общий `src/phoneFormat.ts` (отдельный файл в Operator и в Wizard — НЕ единый пакет). Поле сброса Wizard приведено к виду его же логина. На бэк уходит `992XXXXXXXXX` (без `+`; `PhoneNumberNormalizer` всё равно срезает).

Связано: [[wizard-signin-redesign]], [[email-identity-parity]], [[phone-staff-registration]], [[tg-i18n-honesty]].
