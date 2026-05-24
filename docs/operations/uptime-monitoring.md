# Uptime Monitoring Runbook

Status: Phase 14 pilot operations runbook
Last updated: 2026-05-24

## Purpose

This runbook records the agreed external uptime monitoring setup for AFK4
staging and (eventually) production. The goal is to find out about an
outage from a monitoring alert, not from a club operator on a Saturday
night.

## Setup Decision

AFK4 uses a **Cloudflare Worker on a Cron Trigger** as the external
uptime probe. The Worker source lives at
[`deploy/cloudflare/staging-uptime-monitor/worker.js`](../../deploy/cloudflare/staging-uptime-monitor/worker.js)
and the deploy helper at
[`scripts/deploy-staging-uptime-monitor.sh`](../../scripts/deploy-staging-uptime-monitor.sh).

Reasons over off-the-shelf SaaS (UptimeRobot, Better Stack, etc.):

- AFK4 already has a Cloudflare account; no new third-party signup or
  monitor-count free-tier ceiling.
- Cloudflare's edge POP network is denser in Central Asia than the
  free-tier probe networks of US-headquartered SaaS monitors, which
  matters once Tajikistan or Uzbekistan pilots come online.
- Workers Cron Triggers and Workers KV are both inside the Cloudflare
  free tier (100k requests/day, 1k KV writes/day) with massive headroom
  for the 288 invocations / 2304 KV ops the worker actually performs
  per day.
- Alert logic lives in code under version control rather than in a SaaS
  console. Adding a "do not alert during deploy" window, swapping the
  Telegram channel, or extending the monitor list is a worker.js diff +
  redeploy, not a console click.

The known trade-off is no out-of-the-box public status page. If pilot
clubs need a shareable status URL, add a static page hosted on
Cloudflare Pages that reads the same KV namespace as the worker, or
upgrade to a SaaS that provides one.

## What Is Monitored

Four probes cover the staging surface that a real outage would break.
They run from Cloudflare's network on a 5-minute cron schedule.

| # | Endpoint | Method | Expected status | Expected body keyword |
|---|---|---|---|---|
| 1 | `https://afk4.staging.mubi.dev/api/health` | GET | 200 | `"status":"ok"` |
| 2 | `https://afk4.staging.mubi.dev/api/platform/auth/sign-in` (POST body `{}`) | POST | **401** | - |
| 3 | `https://platform.afk4.staging.mubi.dev/healthz` | GET | 200 | `ok` |
| 4 | `https://platform.afk4.staging.mubi.dev/` | GET | 200 | `AFK4 Platform Control Plane` |

Monitor 2 deliberately submits an empty body to the platform-admin
sign-in endpoint. The endpoint MUST answer 401. A 5xx means the auth
pipeline or DB is broken even though `/api/health` may still look
healthy. A 200/204 means the platform-admin guard regressed and must
be investigated immediately.

## Alert Behaviour

- The worker tracks consecutive failures per monitor in a KV namespace
  keyed `fail-streak:<monitor-name>`.
- A Telegram alert fires only when:
  - the failure streak crosses `FAIL_CONSEC_THRESHOLD = 2` (i.e. the
    second consecutive failed probe), or
  - a previously-down monitor returns to OK.
- All other runs are silent. This avoids the noisy "alert every 5
  minutes while the outage drags on" pattern.
- If the Telegram secrets are unset the worker still probes, logs to
  the Workers `tail` stream, and silently no-ops on alert sends. That
  means it is safe to deploy the worker before the Telegram bot is
  provisioned.

## First-Time Setup

### 1. Cloudflare API token

Create an account-scoped token at
[https://dash.cloudflare.com/profile/api-tokens](https://dash.cloudflare.com/profile/api-tokens)
with the following policies. Both scoped to the AFK4 Cloudflare
account.

- `Workers Scripts: Read + Edit`
- `Account Settings: Read`

Token expiration: 90 days is fine; rotate from the same screen.

Treat the token like an SSH key: never commit it, never paste it into
chat, write it to your password manager.

### 2. Telegram bot for alert delivery

1. DM `@BotFather` on Telegram, run `/newbot`, follow prompts, save the
   bot token (`123456789:AAxxx...`).
2. DM the new bot once with `/start` so it can message you.
3. Get your chat id:
   ```bash
   curl -sS "https://api.telegram.org/bot<bot-token>/getUpdates" |
     python3 -c "import sys,json; print(json.load(sys.stdin)['result'][-1]['message']['chat']['id'])"
   ```
   The chat id for a private DM is your numeric Telegram user id.
4. Test the bot:
   ```bash
   curl -sS -X POST "https://api.telegram.org/bot<bot-token>/sendMessage" \
     -H 'Content-Type: application/json' \
     -d '{"chat_id": <chat-id>, "text": "AFK4 monitor test"}'
   ```
   The bot should send "AFK4 monitor test" to your private chat.

### 3. Deploy the Worker

```bash
export CF_API_TOKEN=<token from step 1>
export CF_ACCOUNT_ID=66e13dcd6a4dbd2cde1e9929e51dd126
export TELEGRAM_BOT_TOKEN=<bot token from step 2>
export TELEGRAM_CHAT_ID=<chat id from step 2>

./scripts/deploy-staging-uptime-monitor.sh
```

Expected output ends with a "Deployment complete" summary listing the
worker name, KV namespace id, and cron schedule.

### 4. Verify

- Open
  [https://dash.cloudflare.com/?to=/:account/workers/services/view/afk4-staging-uptime-monitor](https://dash.cloudflare.com/?to=/:account/workers/services/view/afk4-staging-uptime-monitor)
  and confirm the worker exists, the cron schedule shows
  `*/5 * * * *`, and `STATE` binding points to
  `afk4-staging-uptime-monitor-state`.
- Trigger a manual run from the dashboard's Cron Triggers tab, or wait
  for the next scheduled execution. The worker logs each probe in the
  Workers tail stream; while everything is green there will be no
  Telegram message.
- Force a failure by temporarily breaking one of the assertions (for
  example point monitor 1 to `/api/healthz-wrong-path` in
  `worker.js`, redeploy, wait two intervals). Expect a Telegram
  "AFK4 DOWN" message after the second consecutive failure. Revert
  the change, redeploy, expect a Telegram "AFK4 RECOVERED" message on
  the next interval.

## Incident Playbook

When a Telegram alert fires:

1. Manually reproduce the failing request from your workstation with
   `curl` (the same URL/method/body documented in the table above).
   - If reproduction succeeds, the failure was probe-network-side or
     transient. Watch the next intervals; the worker will send a
     RECOVERED message automatically once the streak resets.
   - If reproduction fails the same way, the outage is real.
2. For Platform API alerts (monitors 1 and 2), check the Coolify
   application logs for `afk4-platform-api-staging` first. The API
   container most commonly crashes on a bad migration or an
   exhausted DB connection pool.
3. For SPA alerts (monitors 3 and 4), check the Coolify application
   logs for `afk4-platform-web-staging` and the Traefik ingress
   labels. The SPA most commonly fails on a broken build or a missing
   Let's Encrypt cert renewal.
4. If the failure mode looks like a database problem, follow
   [`postgres-backup-restore.md`](postgres-backup-restore.md) before
   restoring from a backup. Most "DB is down" alerts are actually
   migration or connection-pool issues that do not need a restore.
5. After the incident, inspect the Worker tail stream
   ([Cloudflare dashboard - Workers - afk4-staging-uptime-monitor -
   Logs](https://dash.cloudflare.com/)) to confirm RECOVERED fired
   and that the KV streak counter cleared.

## Production Upgrade Notes

When the production environment exists (separate domain, separate
Coolify app, separate Postgres), add corresponding monitors to
`worker.js` with the production hostname. Keep the staging monitors
live so a staging regression is also visible.

Add a second alert channel for production:

- Either a second Telegram chat (group, not DM) so multiple humans
  see the alert, or
- a Discord webhook secret in addition to the Telegram secrets and a
  small `sendDiscord` helper next to `sendTelegram`.

For production traffic, consider raising `FAIL_CONSEC_THRESHOLD` to
`3` if the worker stays on 5-minute intervals (so an alert means a
real ~10-minute outage), or dropping to a 1-minute schedule on the
paid Cloudflare Workers plan ($5/month) if sub-5-min detection is
required.

## Known Limitations

- Cloudflare cron is best-effort. A 4-minute outage that resolves
  itself can go undetected. This is acceptable for the pilot.
- The auth-pipeline monitor (#2) is a deliberate failed sign-in and
  produces an `audit_records` row each interval. The audit table will
  accumulate ~288 such rows per day. When audit retention work lands,
  exclude these rows from the retention sweep either by actor IP
  (Cloudflare publishes its egress IPs at
  https://www.cloudflare.com/ips/) or by adding a probe header and
  filtering on it.
- The worker has no public status page. Sharing the worker's KV
  state via a Cloudflare Pages dashboard, or pointing pilot clubs at
  a hand-maintained `status.afk4.net` HTML, is a future-work item.
- The Telegram bot is a single channel. If the chat goes silent (DND,
  app uninstalled, account compromised) the alert is silently lost.
  The "second alert channel" upgrade above is the mitigation.
