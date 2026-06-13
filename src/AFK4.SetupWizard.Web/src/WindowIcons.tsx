// Тонкие «виндовые» глифы контролов окна: sharp corners и единый лёгкий штрих. Раньше maximize
// рисовался lucide-Square (скруглённый и толще остальных) — выбивался по весу. 12×12, currentColor.
const glyph = {
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.1,
} as const;

export function MinimizeIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden>
      <line x1="2" y1="6.5" x2="10" y2="6.5" {...glyph} />
    </svg>
  );
}

export function MaximizeIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden>
      <rect x="2.5" y="2.5" width="7" height="7" {...glyph} />
    </svg>
  );
}

// Восстановить из развёрнутого: передний квадрат + выглядывающий сзади-сверху-справа.
export function RestoreIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden>
      <rect x="2.5" y="3.5" width="6" height="6" {...glyph} />
      <path d="M4.5 3.5 V1.5 H10.5 V7.5 H8.5" {...glyph} />
    </svg>
  );
}

export function CloseIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden>
      <line x1="2.7" y1="2.7" x2="9.3" y2="9.3" {...glyph} />
      <line x1="9.3" y1="2.7" x2="2.7" y2="9.3" {...glyph} />
    </svg>
  );
}
