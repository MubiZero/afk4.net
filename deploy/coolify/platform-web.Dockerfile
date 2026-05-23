# syntax=docker/dockerfile:1.7

# Multistage Dockerfile for the internal SaaS Control Plane SPA
# (src/AFK4.Platform.Web). Build context = repository root.
#
# Build command:
#   docker build -f deploy/coolify/platform-web.Dockerfile -t afk4-platform-web .
#
# Runtime image serves the Vite `dist/` output via nginx with SPA-style
# fallback (every unknown path is rewritten to /index.html). The actual
# `platform.afk4.local` (or staging) host and TLS are terminated by the
# Coolify-managed Traefik in front of this container — see ingress.md.

FROM node:24-alpine AS build
WORKDIR /src
COPY src/AFK4.Platform.Web/package.json src/AFK4.Platform.Web/package-lock.json ./
RUN npm ci
COPY src/AFK4.Platform.Web/. ./
ARG VITE_PLATFORM_API_BASE_URL=""
ENV VITE_PLATFORM_API_BASE_URL=${VITE_PLATFORM_API_BASE_URL}
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY deploy/coolify/platform-web.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/ /usr/share/nginx/html/
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --quiet --spider http://127.0.0.1:8080/healthz || exit 1
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
