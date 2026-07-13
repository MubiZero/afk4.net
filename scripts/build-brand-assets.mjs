import { Resvg } from "@resvg/resvg-js";
import pngToIco from "png-to-ico";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const BRAND = join(ROOT, "brand");
const DIST = join(BRAND, "dist");
mkdirSync(DIST, { recursive: true });

function renderPng(svgRelPath, size) {
  const svg = readFileSync(join(BRAND, svgRelPath), "utf8");
  const resvg = new Resvg(svg, { fitTo: { mode: "width", value: size } });
  return resvg.render().asPng();
}

// App/favicon/tray icon sizes (from afk4-icon.svg)
const ICON_SIZES = [16, 32, 48, 64, 128, 256];
for (const size of ICON_SIZES) {
  writeFileSync(join(DIST, `icon-${size}.png`), renderPng("afk4-icon.svg", size));
}

// PWA icons
writeFileSync(join(DIST, "pwa-192.png"), renderPng("afk4-icon.svg", 192));
writeFileSync(join(DIST, "pwa-512.png"), renderPng("afk4-icon.svg", 512));
writeFileSync(join(DIST, "pwa-maskable-512.png"), renderPng("afk4-icon-maskable.svg", 512));

// Multi-resolution favicon .ico (16/32/48) for browsers + WPF/MSI consumption
const ico = await pngToIco([
  join(DIST, "icon-16.png"),
  join(DIST, "icon-32.png"),
  join(DIST, "icon-48.png"),
]);
writeFileSync(join(DIST, "afk4.ico"), ico);

// Светлый вариант (белый фон) — для тёмного таскбара нативных WPF-хостов
// (Operator/SetupWizard); .ico из afk4-icon.svg там сливался в чёрное пятно.
const LIGHT_ICON_SIZES = [16, 32, 48, 64, 128, 256];
for (const size of LIGHT_ICON_SIZES) {
  writeFileSync(join(DIST, `icon-light-${size}.png`), renderPng("afk4-icon-light.svg", size));
}
const icoLight = await pngToIco([
  join(DIST, "icon-light-16.png"),
  join(DIST, "icon-light-32.png"),
  join(DIST, "icon-light-48.png"),
]);
writeFileSync(join(DIST, "afk4-light.ico"), icoLight);

console.log("brand assets written to brand/dist/");
