import { useTheme } from '@/theme/ThemeProvider';

// Горизонтальный знак AFK4.NET существует в двух вариантах: тёмная подложка (светлый текст) и
// светлая. Подменяем ассет по теме — так каждый остаётся чётким, вместо одного выцветающего.
export function BrandLogo({ className }: { className?: string }) {
  const { theme } = useTheme();
  const src = theme === 'light' ? '/afk4-logo-horizontal-light.svg' : '/afk4-logo-horizontal.svg';
  return <img className={className} src={src} alt="AFK4.NET" />;
}
