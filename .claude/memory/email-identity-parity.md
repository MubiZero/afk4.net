---
name: email-identity-parity
description: Инвариант «где phone — там и email как равный канал»; где живут reset-экраны
metadata: 
  node_type: memory
  type: project
  originSessionId: 6b5c87d3-256e-49bb-bed9-1f02dbef9069
---

Эпик ЗАКРЫТ (PRs #57/#58/#59 в main): email — равноправная альтернатива телефону для staff login/register/reset во всех 3 фронтах. **FE forgot/reset-экраны построены** (старые заметки про «placeholder» неверны).

Durable:
- **Инвариант**: где есть phone-канал — там и email как равный (login/register/reset).
- Reset-экраны: `ForgotPassword.tsx` в Platform/Operator + `ForgotPasswordScreen.tsx` в SetupWizard.
- i18n-движок = ICU `intl-messageformat`.
- SMTP-инфра: у юзера готовый SMTP-сервер, MailKit transport в `Notifications/` — использовать его, не провиженить новый.
