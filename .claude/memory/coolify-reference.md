---
name: coolify-reference
description: "AFK4 staging is hosted on the user's personal Coolify at cool.mubi.dev — API token is short-lived and provided per session, never persisted"
metadata: 
  node_type: memory
  type: reference
  originSessionId: b9f23254-d21d-433d-ac9e-309517ac9404
---

AFK4 staging deployment lives on the user's personal Coolify instance at `https://cool.mubi.dev/`.

API base: `https://cool.mubi.dev/api/v1/`
Auth: `Authorization: Bearer <token>`

**Token handling:** The user pastes a bearer token into chat when they want you to interact with Coolify. Tokens are short-lived / regeneratable — do NOT write them to memory or any committed file. Use them ephemerally from the chat context. If the token isn't in the current session, ask the user to paste a fresh one rather than searching for it.

The user is solo dev on this pet project and has explicitly said not to worry about credential security here; the rule is operational hygiene, not their threat model.

**What's deployed on it** (as of 2026-05-23): Postgres (the Slice 1 migration foundation should be applied here for staging smoke). Workflows file at `.github/workflows/coolify-staging-deploy.yml` references the deploy hook.
