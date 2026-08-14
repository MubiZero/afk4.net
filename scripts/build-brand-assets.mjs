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

// Иконки клиентского приложения на Flutter. Кладутся прямо в проект, а не в dist: иначе они
// живут копией, которую забывают обновить, и приложение уезжает в магазин со стандартным
// синим значком Flutter — именно так и было до 2026-08-13.
const FLUTTER_APP = join(ROOT, "src", "afk4_customer_app");

// Плотности Android: mdpi 48 → xxxhdpi 192.
const ANDROID_LAUNCHER = {
  "mipmap-mdpi": 48,
  "mipmap-hdpi": 72,
  "mipmap-xhdpi": 96,
  "mipmap-xxhdpi": 144,
  "mipmap-xxxhdpi": 192,
};
for (const [dir, size] of Object.entries(ANDROID_LAUNCHER)) {
  const target = join(FLUTTER_APP, "android", "app", "src", "main", "res", dir);
  mkdirSync(target, { recursive: true });
  writeFileSync(join(target, "ic_launcher.png"), renderPng("afk4-icon.svg", size));
}

// Веб-сборка того же приложения: вкладка браузера и установка на домашний экран.
const WEB = join(FLUTTER_APP, "web");
mkdirSync(join(WEB, "icons"), { recursive: true });
writeFileSync(join(WEB, "favicon.png"), renderPng("afk4-icon.svg", 32));
writeFileSync(join(WEB, "icons", "Icon-192.png"), renderPng("afk4-icon.svg", 192));
writeFileSync(join(WEB, "icons", "Icon-512.png"), renderPng("afk4-icon.svg", 512));
writeFileSync(join(WEB, "icons", "Icon-maskable-192.png"), renderPng("afk4-icon-maskable.svg", 192));
writeFileSync(join(WEB, "icons", "Icon-maskable-512.png"), renderPng("afk4-icon-maskable.svg", 512));

console.log("brand assets written to brand/dist/ and src/afk4_customer_app/");
