interface BrandMarkProps {
  className?: string;
}

export function BrandMark({ className }: BrandMarkProps) {
  return (
    <svg
      className={className}
      viewBox="0 0 53 53"
      role="img"
      aria-label="AFK4.NET"
    >
      <g fill="#D9E6E1">
        <rect x="20" y="3" width="13" height="13" rx="3.5" />
        <rect x="37" y="3" width="13" height="13" rx="3.5" />
        <rect x="3" y="20" width="13" height="13" rx="3.5" />
        <rect x="37" y="20" width="13" height="13" rx="3.5" />
        <rect x="3" y="37" width="13" height="13" rx="3.5" />
        <rect x="20" y="37" width="13" height="13" rx="3.5" />
      </g>
      <g fill="#0B9E74">
        <rect x="3" y="3" width="13" height="13" rx="3.5" />
        <rect x="20" y="20" width="13" height="13" rx="3.5" />
        <rect x="37" y="37" width="13" height="13" rx="3.5" />
      </g>
    </svg>
  );
}
