# Coolify Ingress & Rate-Limiting Recipes

Coolify ships with Traefik as the cluster ingress. The recipes below cover the
two ingress concerns that the SaaS Control Plane added in Slice 5–6:

1. **Hosting the Slice 5 SaaS Control Plane SPA** (`src/AFK4.Platform.Web`) on
   a dedicated host such as `platform.afk4.staging.mubi.dev`.
2. **Ingress-level rate-limiting** on the two public endpoints that accept
   bearer-style invite / connection credentials with no auth in front of them:
   - `POST /api/operator-connections/resolve`
   - `POST /api/platform/owner-invites/accept`
   - `POST /api/install/discover`
   - `POST /api/install/enroll`

Every recipe below is a Traefik label set you paste into the Coolify
application's **Network ▸ Labels** field (or, for compose-based services, into
the `labels:` map). The labels assume the Coolify default entrypoint name
(`https`). Rename if your install differs.

## 1. Platform.Web SPA (`platform.afk4.staging.mubi.dev`)

Build the SPA image from the repo root using
[`deploy/coolify/platform-web.Dockerfile`](./platform-web.Dockerfile). The
runtime container listens on port `8080`, serves `dist/` with SPA fallback,
and exposes `/healthz` for the container probe.

Coolify settings:

| Setting | Value |
| --- | --- |
| Build context | repository root |
| Dockerfile path | `deploy/coolify/platform-web.Dockerfile` |
| Build argument | `VITE_PLATFORM_API_BASE_URL=https://api.afk4.staging.mubi.dev` (or whatever the Platform API public URL is on this Coolify) |
| Exposed port | `8080` |
| Health path | `/healthz` |

Traefik labels (set on the SPA application — these wire up the host route
without disturbing the API container):

```yaml
- traefik.enable=true
- traefik.http.routers.afk4-platform-web.rule=Host(`platform.afk4.staging.mubi.dev`)
- traefik.http.routers.afk4-platform-web.entrypoints=https
- traefik.http.routers.afk4-platform-web.tls=true
- traefik.http.routers.afk4-platform-web.tls.certresolver=letsencrypt
- traefik.http.services.afk4-platform-web.loadbalancer.server.port=8080
```

Add a DNS A / AAAA record for `platform.afk4.staging.mubi.dev` pointing at the
Coolify host before deploying.

## 2. Rate-limit the public Platform API endpoints

Both endpoints below are public on purpose — the slug pair / setup code / invite
code is the credential. 128 bits of entropy on the invite code makes a brute
force impractical, but ingress-level rate-limiting is still the right defense
against a misbehaving operator app loop or a noisy scraper, and it caps the
audit-log write rate on the `tenancy.operator_connection.resolve` action.

Add the labels below to the **Platform API** Coolify application. They define
one shared Traefik middleware and attach it to dedicated
router whose `rule` is restricted to the target path. The default Platform API
router (which serves everything else) keeps its existing labels untouched.

```yaml
# Rate-limit middleware: at most 30 requests per minute per source IP, with a
# burst of 10 to absorb the operator app retry-on-typo flow. Tune the average /
# burst pair if real-world traffic needs more headroom.
- traefik.http.middlewares.afk4-public-ratelimit.ratelimit.average=30
- traefik.http.middlewares.afk4-public-ratelimit.ratelimit.period=1m
- traefik.http.middlewares.afk4-public-ratelimit.ratelimit.burst=10
- traefik.http.middlewares.afk4-public-ratelimit.ratelimit.sourcecriterion.ipstrategy.depth=1

# Router 1: /api/operator-connections/resolve
- traefik.http.routers.afk4-api-resolve.rule=Host(`api.afk4.staging.mubi.dev`) && Path(`/api/operator-connections/resolve`) && Method(`POST`)
- traefik.http.routers.afk4-api-resolve.entrypoints=https
- traefik.http.routers.afk4-api-resolve.tls=true
- traefik.http.routers.afk4-api-resolve.tls.certresolver=letsencrypt
- traefik.http.routers.afk4-api-resolve.middlewares=afk4-public-ratelimit
- traefik.http.routers.afk4-api-resolve.service=afk4-platform-api
- traefik.http.routers.afk4-api-resolve.priority=200

# Router 2: /api/platform/owner-invites/accept
- traefik.http.routers.afk4-api-invite-accept.rule=Host(`api.afk4.staging.mubi.dev`) && Path(`/api/platform/owner-invites/accept`) && Method(`POST`)
- traefik.http.routers.afk4-api-invite-accept.entrypoints=https
- traefik.http.routers.afk4-api-invite-accept.tls=true
- traefik.http.routers.afk4-api-invite-accept.tls.certresolver=letsencrypt
- traefik.http.routers.afk4-api-invite-accept.middlewares=afk4-public-ratelimit
- traefik.http.routers.afk4-api-invite-accept.service=afk4-platform-api
- traefik.http.routers.afk4-api-invite-accept.priority=200

# Router 3: /api/install/discover
- traefik.http.routers.afk4-api-install-discover.rule=Host(`api.afk4.staging.mubi.dev`) && Path(`/api/install/discover`) && Method(`POST`)
- traefik.http.routers.afk4-api-install-discover.entrypoints=https
- traefik.http.routers.afk4-api-install-discover.tls=true
- traefik.http.routers.afk4-api-install-discover.tls.certresolver=letsencrypt
- traefik.http.routers.afk4-api-install-discover.middlewares=afk4-public-ratelimit
- traefik.http.routers.afk4-api-install-discover.service=afk4-platform-api
- traefik.http.routers.afk4-api-install-discover.priority=200

# Router 4: /api/install/enroll
- traefik.http.routers.afk4-api-install-enroll.rule=Host(`api.afk4.staging.mubi.dev`) && Path(`/api/install/enroll`) && Method(`POST`)
- traefik.http.routers.afk4-api-install-enroll.entrypoints=https
- traefik.http.routers.afk4-api-install-enroll.tls=true
- traefik.http.routers.afk4-api-install-enroll.tls.certresolver=letsencrypt
- traefik.http.routers.afk4-api-install-enroll.middlewares=afk4-public-ratelimit
- traefik.http.routers.afk4-api-install-enroll.service=afk4-platform-api
- traefik.http.routers.afk4-api-install-enroll.priority=200
```

Notes:

- `priority=200` is higher than the default router Coolify generates (which is
  typically `1`), so these two specific routers win the match for those exact
  paths. The rest of the API traffic continues to flow through the catch-all
  router unchanged.
- `Method(`POST`)` keeps preflight `OPTIONS` requests on the default router
  so they don't count against the rate limit.
- `sourcecriterion.ipstrategy.depth=1` tells Traefik to trust the immediate
  upstream's `X-Forwarded-For` (Coolify's edge). Bump the depth if you front
  Coolify with a CDN or a second proxy.
- Replace `letsencrypt` with whatever cert resolver name your Coolify install
  registered if you customised it.
- If you need to verify the limit is firing without producing real audit
  spam, hit the `resolve` endpoint in a loop with `curl -i -d '{}'` — you
  should see HTTP 429 once the burst is exhausted.

## Verification

After applying the labels:

1. `curl -I https://platform.afk4.staging.mubi.dev/healthz` returns `200`.
2. `curl -I https://platform.afk4.staging.mubi.dev/` returns `200` and the
   SPA shell.
3. From a single source IP, run `for i in $(seq 1 50); do curl -s -o /dev/null
   -w '%{http_code}\n' -X POST https://api.afk4.staging.mubi.dev/api/operator-connections/resolve
   -H 'content-type: application/json' -d '{}'; done`. The first ~10 requests
   return 400 (validation), then the rest return 429 until the period rolls.
4. The same pattern applied to `/api/platform/owner-invites/accept` should
   surface 429 after exhausting the burst.
5. Repeat the same burst check for `/api/install/discover` and
   `/api/install/enroll`; they should also return 429 after the shared burst is
   exhausted. The Platform API also applies an in-process per-source-IP backoff
   to `/api/install/*` so the public install flow still slows noisy callers if
   ingress labels are missing during a staging rehearsal.
