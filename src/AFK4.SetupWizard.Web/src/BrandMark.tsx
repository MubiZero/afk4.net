interface BrandMarkProps {
  className?: string;
}

/**
 * Знак AFK4: три активные клетки по диагонали, шесть тихих.
 *
 * Цвета берутся из токенов, а не зашиты хексами. Зашиты они были от светлой темы (#0B9E74 и
 * #D9E6E1), поэтому в тёмной знак оставался светлым — единственный элемент мастера, не следивший
 * за темой. Канон обеих раскладок лежит в brand/afk4-mark.svg и brand/afk4-mark-light.svg.
 */
export function BrandMark({ className }: BrandMarkProps) {
  return (
    <svg
      className={className}
      viewBox="0 0 53 53"
      role="img"
      aria-label="AFK4.NET"
    >
      <g fill="var(--brand-mark-quiet)">
        <rect x="20" y="3" width="13" height="13" rx="3.5" />
        <rect x="37" y="3" width="13" height="13" rx="3.5" />
        <rect x="3" y="20" width="13" height="13" rx="3.5" />
        <rect x="37" y="20" width="13" height="13" rx="3.5" />
        <rect x="3" y="37" width="13" height="13" rx="3.5" />
        <rect x="20" y="37" width="13" height="13" rx="3.5" />
      </g>
      <g fill="var(--brand-mark-active)">
        <rect x="3" y="3" width="13" height="13" rx="3.5" />
        <rect x="20" y="20" width="13" height="13" rx="3.5" />
        <rect x="37" y="37" width="13" height="13" rx="3.5" />
      </g>
    </svg>
  );
}
