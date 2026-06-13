import { useOperatorTheme } from './operatorTheme';

// The horizontal AFK4.NET wordmark ships in two variants — the default is drawn for a dark
// titlebar (light text), the light variant for the light theme (dark text). Swapping the asset by
// theme keeps each one crisp instead of one washing out on the wrong background.
export function BrandLogo({ className }: { className?: string }) {
  const { theme } = useOperatorTheme();
  const src = theme === 'light' ? '/afk4-logo-horizontal-light.svg' : '/afk4-logo-horizontal.svg';
  return <img className={className} src={src} alt="AFK4.NET" />;
}
