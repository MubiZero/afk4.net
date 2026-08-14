# syntax=docker/dockerfile:1.7

# Multistage Dockerfile for the club-facing Organization Admin UI
# (src/AFK4.OrganizationAdmin.Web). Build context = repository root.
#
# Build command:
#   docker build -f deploy/coolify/organization-admin.Dockerfile \
#     --build-arg VITE_PLATFORM_BASE_URL=https://<platform-host>/ \
#     -t afk4-organization-admin .
#
# The same UI normally runs inside the Windows shell (AFK4.OrganizationAdmin.App), which injects
# its config through WebView2. A browser build has no shell, so the API origin comes from
# VITE_PLATFORM_BASE_URL at build time — see operatorConfig.ts, which throws rather than quietly
# falling back to localhost. That means the URL is baked into the image: a different platform host
# needs a different build, not a different environment variable at runtime.
#
# Runtime image serves the Vite `dist/` output via nginx with SPA-style fallback. TLS and host
# routing are terminated by the Coolify-managed Traefik in front of this container — see ingress.md.

FROM oven/bun:1 AS build
WORKDIR /src
# The repo is a single Bun workspace with one root lockfile, so a frozen install needs the root
# manifest + lockfile plus every workspace member's package.json (the lockfile resolves the whole
# graph). Copy just the manifests first to keep the install layer cached across source changes.
COPY package.json bun.lock ./
COPY packages/formatting/package.json ./packages/formatting/
COPY packages/i18n/package.json ./packages/i18n/
COPY packages/money/package.json ./packages/money/
COPY packages/tokens/package.json ./packages/tokens/
COPY packages/ui/package.json ./packages/ui/
COPY src/AFK4.PlatformControl.Web/package.json ./src/AFK4.PlatformControl.Web/
COPY src/AFK4.OrganizationAdmin.Web/package.json ./src/AFK4.OrganizationAdmin.Web/
COPY src/AFK4.Customer.Web/package.json ./src/AFK4.Customer.Web/
COPY src/AFK4.SetupWizard.Web/package.json ./src/AFK4.SetupWizard.Web/
COPY src/AFK4.Player.Shell.Web/package.json ./src/AFK4.Player.Shell.Web/
RUN bun install --frozen-lockfile
# Bring the full source (node_modules is .dockerignored, so the fresh install stands) and build the
# Organization Admin app — the @afk4/* packages are consumed as TypeScript source, no build step.
COPY . .
ARG VITE_PLATFORM_BASE_URL=""
ARG VITE_SETUP_INSTALLER_URL=""
ENV VITE_PLATFORM_BASE_URL=${VITE_PLATFORM_BASE_URL}
ENV VITE_SETUP_INSTALLER_URL=${VITE_SETUP_INSTALLER_URL}
WORKDIR /src/src/AFK4.OrganizationAdmin.Web
RUN bun run build

FROM nginx:1.27-alpine AS runtime
COPY deploy/coolify/organization-admin.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/src/AFK4.OrganizationAdmin.Web/dist/ /usr/share/nginx/html/
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --quiet --spider http://127.0.0.1:8080/healthz || exit 1
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
