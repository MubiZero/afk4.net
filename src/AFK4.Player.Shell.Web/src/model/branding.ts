/**
 * Цвет клуба — поверх нашей палитры, но только если его можно читать.
 *
 * Клуб выбирает акцент в Панели, и он приходит в состоянии. Кнопка с тёмной надписью на бледно-
 * жёлтом или светлый текст на тёмном фиолетовом — нечитаемы; тогда остаётся фирменный зелёный.
 * Порог тот же, что в приложении игрока (#419): 4,5:1 и для акцента на фоне, и для надписи на
 * акценте.
 */
const CANVAS = '#080c0b';
const TEXT_ON_ACCENT = '#04120d';
const AA_TEXT = 4.5;

export function clubAccent(accentColor: string | null | undefined): string | null {
  const color = normalizeHex(accentColor);
  if (!color) return null;
  return contrast(color, CANVAS) >= AA_TEXT && contrast(TEXT_ON_ACCENT, color) >= AA_TEXT ? color : null;
}

export function contrast(foreground: string, background: string): number {
  const l1 = luminance(foreground);
  const l2 = luminance(background);
  const [hi, lo] = l1 > l2 ? [l1, l2] : [l2, l1];
  return (hi + 0.05) / (lo + 0.05);
}

function normalizeHex(value: string | null | undefined): string | null {
  if (!value) return null;
  const hex = value.trim().replace(/^#/, '');
  if (/^[0-9a-f]{3}$/i.test(hex)) {
    return `#${hex.split('').map((c) => c + c).join('').toLowerCase()}`;
  }
  return /^[0-9a-f]{6}$/i.test(hex) ? `#${hex.toLowerCase()}` : null;
}

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const [r, g, b] = channels.map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
