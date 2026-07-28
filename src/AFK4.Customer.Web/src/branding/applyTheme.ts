import type { OrganizationBrandingDto } from '../api/types';

// Simple hex contrast pick for the accent foreground (dark text on light accent, etc.).
function readableForeground(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return '#101314';
  const n = parseInt(m[1], 16);
  const r = (n >> 16) & 0xff, g = (n >> 8) & 0xff, b = n & 0xff;
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.6 ? '#101314' : '#ffffff';
}

export function applyTheme(branding: OrganizationBrandingDto | null): void {
  const root = document.documentElement;
  if (branding?.accentColor) {
    root.style.setProperty('--accent', branding.accentColor);
    root.style.setProperty('--accent-fg', readableForeground(branding.accentColor));
  }
  if (branding?.name) document.title = branding.name;
}
