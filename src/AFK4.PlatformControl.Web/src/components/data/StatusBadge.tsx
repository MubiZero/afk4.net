import { Badge } from '@/components/ui/badge';

export function StatusBadge({ label, tone = 'neutral' }: { label: string; tone?: 'neutral' | 'success' | 'warning' | 'danger' }) {
  const variant = tone === 'danger' ? 'destructive' : tone === 'neutral' ? 'secondary' : 'outline';
  return <Badge variant={variant} className={tone === 'success' ? 'border-success/50 text-success' : tone === 'warning' ? 'border-warning/50 text-warning' : undefined}>{label}</Badge>;
}
