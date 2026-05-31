# syntax=docker/dockerfile:1.7

# Multistage Dockerfile for Platform.Web (src/AFK4.Platform.Web). Build
# context = repository root. The same source builds either the internal admin
# host or the customer club host through VITE_AUDIENCE.
#
# Build command:
#   docker build -f deploy/coolify/platform-web.Dockerfile --build-arg VITE_AUDIENCE=admin -t afk4-platform-web-admin .
#   docker build -f deploy/coolify/platform-web.Dockerfile --build-arg VITE_AUDIENCE=club -t afk4-platform-web-club .
#
# Runtime image serves the Vite `dist/` output via nginx with SPA-style
# fallback (every unknown path is rewritten to /index.html). The actual
# `platform.afk4.local` (or staging) host and TLS are terminated by the
# Coolify-managed Traefik in front of this container — see ingress.md.

FROM oven/bun:1 AS build
WORKDIR /src
COPY src/AFK4.Platform.Web/package.json src/AFK4.Platform.Web/bun.lock ./
RUN bun install --frozen-lockfile
COPY src/AFK4.Platform.Web/. ./
ARG VITE_PLATFORM_API_BASE_URL=""
ARG VITE_AUDIENCE="admin"
ENV VITE_PLATFORM_API_BASE_URL=${VITE_PLATFORM_API_BASE_URL}
ENV VITE_AUDIENCE=${VITE_AUDIENCE}
RUN bun run build

FROM nginx:1.27-alpine AS runtime
COPY deploy/coolify/platform-web.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/ /usr/share/nginx/html/
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --quiet --spider http://127.0.0.1:8080/healthz || exit 1
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
