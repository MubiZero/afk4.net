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

`platform.afk4.staging.mubi.dev/admin` is **Platform Control** — the
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
holding only `organization.pos.catalog.manage` sees `Товары` and nothing else here — the
navigation is derived from what the signed-in person can actually do. A missing
section therefore reads as "you are signed in as the wrong person", not as a
broken build. Sign in as the organization owner to see all of them.

`Платежи и лояльность` needs `organization.payments.gateways.manage` or
`organization.loyalty.settings.manage`, and the tabs inside it are gated separately: the
referral programme and cashback are behind the loyalty permission, so a role with
only the gateway permission sees the section but not the referral settings.

## Two ways to open it against a deployed environment

### Native Windows application

The released path. Install the MSI for the environment, for staging:

```
https://updates.afk4.staging.mubi.dev/afk4-updates-staging/organization-admin/internal/latest/afk4-organization-admin-internal.msi
```

### Browser, from a checkout

Useful when there is no Windows machine at hand — for verification passes, for
example. The React UI that the WPF shell hosts also builds as an ordinary
browser application:

```bash
cd src/AFK4.OrganizationAdmin.Web
bun install
VITE_PLATFORM_BASE_URL=https://afk4.staging.mubi.dev bun run build
bun run preview
```

It serves on `http://127.0.0.1:4174`. **Do not change the port.** The API's CORS
list is built by `ResolveCorsOrigins` in `src/AFK4.Platform.Api/Program.cs`,
which *concatenates* configured origins onto its built-in defaults rather than
replacing them, and `http://127.0.0.1:4174`, `http://localhost:4174`,
`http://127.0.0.1:5174` and `http://localhost:5174` are among those defaults. So
this works against any environment without changing its configuration — and any
other port is refused by the browser before the request reaches the API.

`bun run dev` is a different thing: it serves on port 5174 against
`devMockBackend.ts`, not against a real API. Use it for UI work, never to verify
behaviour.

Sign-in is the ordinary staff sign-in — phone or email/login plus password.

### The same defaults in production

Those localhost origins cannot be configured away: they are appended
unconditionally. Every deployed environment therefore accepts credentialed
cross-origin calls from a page on the operator's own `localhost:4174`. That is
what makes the browser path above work everywhere, and it is worth an explicit
decision before production rather than a discovery afterwards.
