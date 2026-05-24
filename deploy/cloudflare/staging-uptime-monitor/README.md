# Staging Uptime Monitor Worker

A single Cloudflare Worker, scheduled by Cron Triggers, that probes the four
AFK4 staging endpoints described in
[`docs/operations/uptime-monitoring.md`](../../../docs/operations/uptime-monitoring.md)
and sends Telegram alerts on state change only (first DOWN + RECOVERED).

## Files

- `worker.js` - the Worker module. Endpoint list is inline so the
  monitor configuration sits with the script that uses it.

## Deploy

Use [`scripts/deploy-staging-uptime-monitor.sh`](../../../scripts/deploy-staging-uptime-monitor.sh)
from the repo root:

```bash
export CF_API_TOKEN=<account-scoped token with Workers Scripts: Edit>
export CF_ACCOUNT_ID=66e13dcd6a4dbd2cde1e9929e51dd126
# Optional, set both to enable Telegram alerts:
export TELEGRAM_BOT_TOKEN=<BotFather token>
export TELEGRAM_CHAT_ID=<chat id from getUpdates>

./scripts/deploy-staging-uptime-monitor.sh
```

The script is idempotent. It creates the KV namespace on first run,
uploads the latest `worker.js` (with KV binding), and (re)installs the
`*/5 * * * *` cron schedule on every run. Telegram secrets are only
pushed when both env vars are set; omit them to update worker code
without rotating credentials.

## Runtime

- Worker name: `afk4-staging-uptime-monitor`
- KV namespace: `afk4-staging-uptime-monitor-state`
- Cron: every 5 minutes (Cloudflare best-effort)
- HTTP surface: none (worker exists for cron only; a hand `fetch` returns
  a static text shell for diagnostics)
- Bindings: `STATE` (KV) for consecutive-failure counters,
  `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` (secrets) for alerts.

## Alert behaviour

- Only state changes generate Telegram messages: the first DOWN that
  crosses `FAIL_CONSEC_THRESHOLD = 2` consecutive failed probes, and the
  next RECOVERED after the streak resets.
- If both Telegram secrets are unset the worker still probes and logs to
  the Workers `tail` stream, but suppresses alert sends so it is safe to
  deploy before the Telegram bot is provisioned.
- Cloudflare cron is best-effort and may skip an interval under load -
  do not rely on this worker for sub-5-min SLOs.
