---
name: afk4-coolify-token-location
description: "Where the Coolify API bearer token for staging lives on this box, and how to use it."
metadata: 
  node_type: memory
  type: reference
  originSessionId: 06f4ec90-ea92-4239-88c1-ca8a361777b9
---

Coolify API bearer token for **cool.mubi.dev** is stored by the user at **`~/.config/afk4/coolify.token`** (single line, raw token, `chmod 600`, outside the repo). Read it with e.g. `TOKEN=$(cat ~/.config/afk4/coolify.token)` and call `curl -H "Authorization: Bearer $TOKEN" https://cool.mubi.dev/api/v1/...`. Staging app uuids and runbook are in [[afk4-env-quirks]] (Coolify section): platform-api-staging `d3fm17hl6kb7sossg1kj8buq`. rtk mangles JSON → put curl in a `/tmp/x.sh` and `bash` it, save responses to a file and parse. Set up 2026-06-11 to fix the staging session-lease signing key gap (see [[afk4-client-demo-runbook]]).
