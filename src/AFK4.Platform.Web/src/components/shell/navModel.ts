import type { MessageKey } from '@/i18n/messages';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  ownerOnly: boolean;
  soon: boolean;
}

export interface NavGroup {
  key: string;
  labelKey: MessageKey;
  items: NavItem[];
}
