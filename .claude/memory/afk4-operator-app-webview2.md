---
name: afk4-operator-app-webview2
description: "Operator app is a WebView2 native host + React UI (same pivot as the player shell), NOT pure WPF."
metadata: 
  node_type: memory
  type: project
  originSessionId: 419d4960-82a3-41b8-af9f-4fb3db04624d
---

`AFK4.OrganizationAdmin.App` = native WebView2 host (WPF shell project, dirs Pos/FloorMap/Realtime/Web/WebAssets…); the operator UI lives in `src/AFK4.OrganizationAdmin.Web` as a React app (Vite + bun, builds/tests on Linux). Existing React surfaces: `App.tsx`, `BackendPosWorkspace.tsx` (POS), `BackendPlayersWorkspace`, `DashboardWorkspace`, `MapWorkspace`, `operatorApiClients.ts` (API), `hostBridge.ts`, SignalR realtime via `DeviceHub`. So operator-side UI work = React in `AFK4.OrganizationAdmin.Web` (cheap, Linux-buildable), NOT slow WPF/XAML. Mirrors the player-shell pivot — see [[afk4-customer-shell-pivot]].
