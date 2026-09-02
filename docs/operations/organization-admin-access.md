# Reaching Organization Admin

Organization Admin is **one application**, not two. Club operations and the
owner-facing settings live in the same product; what a person sees is decided by
their permissions, not by which app they open. There is no separate "owner
cabinet" to install or navigate to — that phrasing has misled readers before, and
the settings it refers to are the `Управление` workspace inside Organization
Admin.

Organization Admin was the Operator App until the 2026-07-28 product-boundary
migration. The old name survives inside the code (`operatorHelpers.ts`, `op.*`
message keys, `OperatorBackendContext`) and in archived plans; it is not a second
product.

`platform.api.afk4.net/admin` is **Platform Control** — the
platform-owner and support console. Tariffs, loyalty and club layout are not
there.

## Where the owner-facing settings are

Inside Organization Admin, workspace `Управление`:

| Destination | Holds |
| --- | --- |
| `Клуб` | branch profile |
| `Залы и ПК` | zones, seats, and which seats have an attached approved gaming PC |
| `Тарифы и пакеты` | tariffs (including the hours a tariff applies in), hour packages |
| `Товары` | POS catalogue, prices, barcodes |
| `Сотрудники и роли` | branch staff and their roles |
| `Новости` | posts shown to players in the customer app |
| `Платежи и лояльность` | payment gateways, cashback, the referral programme |

Each destination is gated on its own permissions (`managementNav.ts`), so a role
holding only `organization.pos.catalog.manage` sees `Товары` and nothing else
here — the navigation is derived from what the signed-in person can actually do.
Sign in as the organization owner to see all of them.

`Платежи и лояльность` is the exception worth knowing before you go looking for
the referral settings. The destination appears with either
`organization.payments.gateways.manage` or `organization.loyalty.settings.manage`
(`managementNav.ts`), but cashback and the referral programme need **both** that
loyalty permission **and** the organization's `loyalty` feature flag
(`PaymentsLoyaltyDestination.tsx`). With the flag off they are simply absent, and
a role holding only the loyalty permission lands on an empty screen. So a missing
section usually means the wrong login — but a missing *referral block* means the
feature is not enabled for the organization.

## Two ways to open it against a deployed environment

### Native Windows application

The released path. Install the MSI for the environment, for staging:

```
https://updates.afk4.net/afk4-updates-staging/organization-admin/internal/latest/afk4-organization-admin-internal.msi
```

### Browser, from a checkout

Useful when there is no Windows machine at hand — for verification passes, for
example. The React UI that the WPF shell hosts also builds as an ordinary
browser application:

```bash
cd src/AFK4.OrganizationAdmin.Web
bun install
VITE_PLATFORM_BASE_URL=https://api.afk4.net bun run build
bun run preview
```

It serves on `http://127.0.0.1:4174`. **Do not change the port.** The API's CORS
list is built by `CorsOrigins.Resolve`, which *concatenates* configured origins
onto its built-in developer defaults rather than replacing them, and
`http://127.0.0.1:4174`, `http://localhost:4174`, `http://127.0.0.1:5174` and
`http://localhost:5174` are among those defaults. So this works against any
non-production environment — staging included — without changing its
configuration. From any other port the browser withholds the response — the
preflight `OPTIONS` still reaches the API, since every request carries an
`Authorization` header and is therefore non-simple; it is the browser that
refuses to hand the answer back.

`bun run dev` is a different thing: it serves on port 5174 against
`devMockBackend.ts`, not against a real API. Use it for UI work, never to verify
behaviour.

Sign-in is the ordinary staff sign-in — phone or email/login plus password.

### Production lists its own origins

The developer defaults — the loopback ports and the `*.afk4.local` hosts-file
names — are appended only when `ASPNETCORE_ENVIRONMENT` is anything other than
`Production`. Staging sets it to `Staging` (`deploy/coolify/staging.env.template`),
so the browser route above keeps working there. A production deployment gets no
defaults at all and must list its origins explicitly:

```
Cors__OperatorWebOrigins__0=https://admin.afk4.example
Cors__PlatformWebOrigins__0=https://platform.afk4.example
```

Miss that and browser clients are refused outright; the API logs a warning at
startup saying so. That is the deliberate trade: a visible misconfiguration
instead of a quietly open `localhost:4174` on every deployment.

The exposure that was closed was narrow to begin with. The API has no cookie
authentication at all — a grep for cookie usage in `src/AFK4.Platform.Api`
returns nothing — and staff auth is a bearer token held in `sessionStorage`
(`platformApi.ts`, `authClient`). `AllowCredentials` therefore granted no ambient
authority: a page on `localhost:4174` carries no token unless it already has one,
and cross-origin JavaScript cannot read another origin's `sessionStorage`. CORS
is not a server-side authorization boundary either — any non-browser client
reaches the API regardless. What remained was a malicious page served from the
operator's own machine on that exact port reading API responses **if** it first
obtained a token, for instance by phishing a sign-in on that origin. Never a CSRF
hole; still not something to ship on by default.
