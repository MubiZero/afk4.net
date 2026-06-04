# AFK4.NET brand assets

Canonical logo assets for AFK4.NET. Master files are hand-authored SVGs; raster
outputs are generated. See the brand foundation spec:
`docs/superpowers/specs/2026-06-04-afk4-brand-positioning-design.md`.

## Files

| File | Use |
|------|-----|
| `afk4-mark.svg` / `-light.svg` | command-grid mark only (dark / light surfaces) |
| `afk4-icon.svg` | mark on rounded dark tile — favicon / app / tray |
| `afk4-icon-maskable.svg` | PWA maskable (safe-zone padded) |
| `afk4-logo-horizontal.svg` / `-light.svg` | primary lockup (mark + wordmark) |
| `afk4-logo-vertical.svg` / `-light.svg` | stacked lockup |
| `dist/*` | generated png sizes + `afk4.ico` |

## Tokens

- Accent: `#2DD4A7` (dark surfaces) / `#0B9E74` (light surfaces)
- Inactive cell: `#173028` (dark) / `#D9E6E1` (light)
- Icon background: `#0E2019`
- Wordmark text: `#E2F1EC` (dark) / `#0B1F18` (light)
- Active cells: diagonal — top-left, center, bottom-right
- Wordmark is **always uppercase** `AFK4.NET`; `.NET` in the accent color.

## Regenerate raster assets

```bash
bun run build:brand
```

Outputs go to `dist/`. The web frontends use `afk4-icon.svg` directly as their
`favicon.svg` (the Customer PWA manifest points at the same SVG), so re-copy that
file into each `src/*.Web/public/` dir if the mark changes. The `dist/` raster set
(png sizes + `afk4.ico`) is the library for tray/MSI and other raster consumers.

## Verify

```bash
bun test brand/afk4-brand.test.ts
```
