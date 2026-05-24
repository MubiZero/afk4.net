# Uptime Monitoring Runbook

Status: Phase 14 pilot operations runbook
Last updated: 2026-05-24

## Purpose

This runbook records the agreed external uptime monitoring setup for AFK4
staging and (eventually) production. The goal is to find out about an
outage from a monitoring email, push, or Telegram alert - not from a club
operator on a Saturday night.

## Service Choice

AFK4 uses **UptimeRobot** for external uptime monitoring.

Reasons:

- Free tier covers what the pilot needs (50 HTTP/HTTPS monitors,
  5-minute interval, unlimited email/web-push, two free integrations like
  Telegram or Slack, free public status page).
- HTTP keyword monitors support both "must contain" and "must not contain"
  assertions, which lets us monitor more than a 200 response.
- Mature service: no surprise free-tier deprecations in the last few years.

Better Stack (formerly Better Uptime) is the planned upgrade path when
incident management, on-call rotations, or sub-minute checks are needed.
UptimeRobot Pro is the cheaper upgrade path if all that is needed is
shorter intervals (30 s) and SMS.

## What To Monitor

Four monitors cover the staging surface that a real outage would break.
All probes run from the UptimeRobot probe network.

### 1. Platform API health endpoint

- URL: `https://afk4.staging.mubi.dev/api/health`
- Type: HTTPS keyword
- Method: GET
- Interval: 5 minutes
- Keyword to find: `"status":"ok"`
- Alert when: keyword is not found OR response is non-2xx

This catches API container down, DB connection lost, ASP.NET host
unhealthy, Coolify ingress misroute, Let's Encrypt cert expiry.

### 2. Platform API auth pipeline

- URL: `https://afk4.staging.mubi.dev/api/platform/auth/sign-in`
- Type: HTTPS keyword
- Method: POST
- POST body: `{}`
- POST headers: `Content-Type: application/json`
- Interval: 5 minutes
- Expected status code: 401
- Alert when: status code is anything other than 401

The empty body deliberately fails platform-admin authentication and
the endpoint must answer 401. Status code 5xx means the auth pipeline
or DB is broken even though `/api/health` may still look healthy.
200/204 means the platform-admin guard regressed and must be
investigated immediately.

### 3. Control Plane SPA healthz

- URL: `https://platform.afk4.staging.mubi.dev/healthz`
- Type: HTTPS keyword
- Method: GET
- Interval: 5 minutes
- Keyword to find: `ok`
- Alert when: keyword is not found OR response is non-2xx

This is the lightweight nginx-served health probe baked into
`deploy/coolify/platform-web.nginx.conf`. It catches SPA container down
and ingress misroute without paying the React shell render cost.

### 4. Control Plane SPA shell

- URL: `https://platform.afk4.staging.mubi.dev/`
- Type: HTTPS keyword
- Method: GET
- Interval: 5 minutes
- Keyword to find: `AFK4 Platform Control Plane`
- Alert when: keyword is not found OR response is non-2xx

This catches a deploy that left an empty `index.html`, a broken Vite
build, or a regression that stripped the document title.

## Alert Channels

Configure two notification channels at minimum:

1. **Email** to the operations owner address (free tier).
2. **Telegram bot** to the operations owner private chat. This is the
   fastest channel that does not require a phone plan with SMS.

Optional third channel for the pilot:

3. **Web push** on the operations owner's primary browser, to catch the
   case where mail is silenced and the phone is off.

Set "Down" alerts to fire after **two consecutive failed checks** rather
than one. UptimeRobot's free probe network can produce single-probe
false positives on cold starts (the SPA `/healthz` cold start has been
observed at 7-8 seconds).

## Setup Steps

1. Create an UptimeRobot account at https://uptimerobot.com with the
   operations owner email.
2. **My Settings -> Alert Contacts**: add the alert channels above.
   Verify each (email link, Telegram `/start` flow on the
   `UptimeRobotBot` chat).
3. For each of the four monitors above, **+ New Monitor -> HTTP(s) (Keyword)**:
   - Friendly name: `AFK4 Staging - <endpoint short name>`.
   - URL, method, body, headers, keyword as listed.
   - Interval: 5 minutes.
   - Monitoring timeout: 30 seconds (the SPA shell cold start has been
     measured at 7.6 s; 30 s gives headroom for cross-region probe
     latency).
   - HTTP basic auth: leave blank.
   - **Select alert contacts**: tick the channels created in step 2.
   - Threshold: 2 consecutive failed checks.
4. **My Settings -> Public Status Pages**: create one public status page
   that includes all four monitors. Suggested slug: `afk4-staging`.
   This gives a `https://stats.uptimerobot.com/<slug>` URL that can be
   shared with pilot club operators ("if you suspect AFK4 is down,
   open this page first").
5. Walk through the staging deploy runbook
   [coolify-staging-deploy.md](coolify-staging-deploy.md), wait at
   least 15 minutes, and confirm all four monitors are green in the
   UptimeRobot dashboard before treating the setup as finished.

## Incident Playbook

When an alert fires:

1. Open the UptimeRobot incident detail to see which monitor(s)
   tripped and the failed-check response body / headers / status code.
2. Manually reproduce the failing request from your workstation with
   `curl` (the same URL/method/body/headers documented above).
   - If reproduction succeeds, the failure was probe-network-side or
     transient. Wait one more interval. If alerts continue, downgrade
     the alert threshold or open a UptimeRobot probe-location ticket.
   - If reproduction fails the same way, the outage is real.
3. For Platform API alerts (monitors 1 and 2), check the Coolify
   application logs for `afk4-platform-api-staging` first - the API
   container most commonly crashes on a bad migration or an
   exhausted DB connection pool.
4. For SPA alerts (monitors 3 and 4), check the Coolify application
   logs for `afk4-platform-web-staging` and the Traefik ingress
   labels - the SPA most commonly fails on a broken build or a
   missing Let's Encrypt cert renewal.
5. If the failure mode looks like a database problem, follow
   [postgres-backup-restore.md](postgres-backup-restore.md) before
   restoring from a backup. Most "DB is down" alerts are actually
   migration or connection-pool issues that do not need a restore.

## Production Upgrade Notes

When the production environment exists (separate domain, separate
Coolify app, separate Postgres), duplicate every monitor with the
production hostname and tag them with `env:prod` in UptimeRobot. Keep
the staging monitors live so a staging regression is also visible.

Production alert channels should add at minimum:

- SMS to the on-call owner (requires UptimeRobot Pro `$7/month`).
- A second human's phone number once there is a second human on call.

Production status page should live at a stable URL the pilot clubs
trust (for example `status.afk4.net`), not at the default
`stats.uptimerobot.com/<slug>` URL.

## Known Limitations

- The free tier checks at 5-minute intervals. A 4-minute outage that
  resolves itself can go undetected. This is acceptable for the pilot.
- UptimeRobot's probe network is mostly in North America and Western
  Europe. AFK4 staging is hosted on Coolify in Europe, so probe
  latency is fine. A future Tajikistan/Central Asia pilot may want a
  Pingdom-style multi-region probe set instead.
- Keyword monitors do not validate JSON structure. The
  `"status":"ok"` keyword for monitor 1 will pass if any other field
  on the response also happens to contain that substring. Today the
  health endpoint payload is small enough that this is not a concern.
- The auth-pipeline monitor (monitor 2) is a deliberate failed
  sign-in and produces an `audit_records` row each interval. The
  audit table will accumulate ~12 such rows per hour. When audit
  retention work lands, exclude these rows from the retention sweep
  by actor IP (UptimeRobot publishes its probe IP ranges at
  https://uptimerobot.com/inc/files/ips/IPv4andIPv6.txt).
