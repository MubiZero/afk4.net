import type { LucideIcon } from 'lucide-react';
import type { MessageKey } from '@/i18n/messages';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  icon: LucideIcon;
}
