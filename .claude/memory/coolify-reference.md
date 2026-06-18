---
name: coolify-reference
description: Staging на личном Coolify cool.mubi.dev — API и где лежит bearer-токен
metadata: 
  node_type: memory
  type: reference
  originSessionId: b9f23254-d21d-433d-ac9e-309517ac9404
---

Staging хостится на личном Coolify **cool.mubi.dev**; REST API под `https://cool.mubi.dev/api/v1/`, авторизация `Authorization: Bearer <token>`.

**Токен** лежит в `~/.config/afk4/coolify.token` (chmod 600, вне репо) — читать оттуда, не хардкодить. На staging давно крутится полный стек (не «Postgres + Slice 1», как было в старой заметке).

Solo-dev: credential-security здесь не угроза-модель. rtk манглит curl с JSON → класть команду в `/tmp/x.sh` (см. [[afk4-env-quirks]]).
