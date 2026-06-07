import type { LucideIcon } from 'lucide-react';
import {
  CalendarClock,
  CircleDollarSign,
  ClipboardCheck,
  CreditCard,
  LayoutDashboard,
  Monitor,
  ReceiptText,
  ScrollText,
  Settings,
  Users
} from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';

export type SeatTone = 'ready' | 'active' | 'pending' | 'warning' | 'blocking' | 'offline' | 'service';

export interface SeatSummary {
  id: string;
  zone: string;
  name: string;
  tone: SeatTone;
  stateLabel: string;
  player: string;
  remaining: string;
  billing: string;
  device: string;
  command: string;
  app: string;
  deviceId?: string | null;
  deviceName?: string | null;
  isDeviceOnline?: boolean | null;
  isDeviceLocked?: boolean | null;
  hasActiveSession?: boolean;
  activeSessionId?: string | null;
  rawState?: string;
  remainingSeconds?: number | null;
  remainingDeadlineMs?: number | null;
  accruedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  sortOrder?: number;
}

export interface NavItem {
  labelKey: MessageKey;
  icon: LucideIcon;
  active?: boolean;
}

export const navItems: NavItem[] = [
  { labelKey: 'op.shell.nav.map', icon: Monitor, active: true },
  { labelKey: 'op.shell.nav.dashboard', icon: LayoutDashboard },
  { labelKey: 'op.shell.nav.booking', icon: CalendarClock },
  { labelKey: 'op.shell.nav.pos', icon: ReceiptText },
  { labelKey: 'op.shell.nav.players', icon: Users },
  { labelKey: 'op.shell.nav.payments', icon: CircleDollarSign },
  { labelKey: 'op.shell.nav.payment_cards', icon: CreditCard },
  { labelKey: 'op.shell.nav.logs', icon: ScrollText },
  { labelKey: 'op.shell.nav.settings', icon: Settings },
  { labelKey: 'op.shell.nav.review', icon: ClipboardCheck }
];
export const seats: SeatSummary[] = [
  {
    id: 'pc-01',
    zone: 'Зал A',
    name: 'PC-01',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Amir K.',
    remaining: '43 мин',
    billing: 'Wallet',
    device: 'Online · unlocked · Agent 0.4',
    command: 'Сессия подтверждена',
    app: 'Rust'
  },
  {
    id: 'pc-02',
    zone: 'Зал A',
    name: 'PC-02',
    tone: 'ready',
    stateLabel: 'Готов',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: 'Online · locked · Agent 0.4',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-03',
    zone: 'Зал A',
    name: 'PC-03',
    tone: 'pending',
    stateLabel: 'Команда',
    player: 'Daler M.',
    remaining: 'Ожидает',
    billing: 'Package',
    device: 'Online · locked · Shell 0.4',
    command: 'Unlock pending',
    app: 'CS2'
  },
  {
    id: 'pc-04',
    zone: 'Зал A',
    name: 'PC-04',
    tone: 'warning',
    stateLabel: 'Долг',
    player: 'Said R.',
    remaining: '12 мин',
    billing: 'Постоплата',
    device: 'Online · unlocked',
    command: 'Payment check',
    app: 'Valorant'
  },
  {
    id: 'pc-05',
    zone: 'Зал A',
    name: 'PC-05',
    tone: 'offline',
    stateLabel: 'Офлайн',
    player: 'Нет игрока',
    remaining: 'Нет heartbeat',
    billing: 'N/A',
    device: 'Offline · locked state unknown',
    command: 'Нет связи с ПК',
    app: 'Shell ?'
  },
  {
    id: 'pc-06',
    zone: 'Зал B',
    name: 'PC-06',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Madina S.',
    remaining: '1ч 12м',
    billing: 'Package',
    device: 'Online · unlocked · Agent 0.4',
    command: 'Сессия подтверждена',
    app: 'Dota 2'
  },
  {
    id: 'pc-07',
    zone: 'Зал B',
    name: 'PC-07',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Готов',
    billing: 'Fast guest',
    device: 'Online · unlocked',
    command: 'Idle',
    app: 'Launcher'
  },
  {
    id: 'pc-08',
    zone: 'Зал B',
    name: 'PC-08',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Nikita V.',
    remaining: '18 мин',
    billing: 'Wallet',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'CS2'
  },
  {
    id: 'pc-09',
    zone: 'Зал B',
    name: 'PC-09',
    tone: 'service',
    stateLabel: 'Сервис',
    player: 'Нет игрока',
    remaining: 'Закрыт',
    billing: 'N/A',
    device: 'Устройство не назначено',
    command: 'Technician',
    app: 'Maintenance'
  },
  {
    id: 'pc-10',
    zone: 'VIP',
    name: 'PC-10',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Yusuf A.',
    remaining: '54 мин',
    billing: 'Wallet',
    device: 'Online · unlocked · Shell 0.4',
    command: 'Сессия подтверждена',
    app: 'Fortnite'
  },
  {
    id: 'pc-11',
    zone: 'VIP',
    name: 'PC-11',
    tone: 'blocking',
    stateLabel: 'Ошибка',
    player: 'Гость',
    remaining: 'Нужно действие',
    billing: 'Cash',
    device: 'Online · lock failed',
    command: 'Command failed',
    app: 'Shell'
  },
  {
    id: 'pc-12',
    zone: 'VIP',
    name: 'PC-12',
    tone: 'ready',
    stateLabel: 'Готов',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: 'Online · locked',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-13',
    zone: 'Зал C',
    name: 'PC-13',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Aziz P.',
    remaining: '2ч 05м',
    billing: 'Package',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'Apex'
  },
  {
    id: 'pc-14',
    zone: 'Зал C',
    name: 'PC-14',
    tone: 'ready',
    stateLabel: 'Готов',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: 'Online · locked',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-15',
    zone: 'Зал C',
    name: 'PC-15',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Murod T.',
    remaining: '31 мин',
    billing: 'Wallet',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'PUBG'
  },
  {
    id: 'pc-16',
    zone: 'Зал C',
    name: 'PC-16',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Готов',
    billing: 'Fast guest',
    device: 'Online · locked',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-17',
    zone: 'Зал C',
    name: 'PC-17',
    tone: 'pending',
    stateLabel: 'Команда',
    player: 'Bek S.',
    remaining: 'Ожидает',
    billing: 'Cash',
    device: 'Online · locked',
    command: 'Start pending',
    app: 'Launcher'
  },
  {
    id: 'pc-18',
    zone: 'Зал C',
    name: 'PC-18',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Farid N.',
    remaining: '8 мин',
    billing: 'Постоплата',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'Roblox'
  },
  {
    id: 'pc-19',
    zone: 'Bootcamp',
    name: 'PC-19',
    tone: 'ready',
    stateLabel: 'Готов',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: 'Online · locked',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-20',
    zone: 'Bootcamp',
    name: 'PC-20',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Timur Z.',
    remaining: '1ч 41м',
    billing: 'Package',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'CS2'
  },
  {
    id: 'pc-21',
    zone: 'Bootcamp',
    name: 'PC-21',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Готов',
    billing: 'Fast guest',
    device: 'Online · locked',
    command: 'Idle',
    app: 'Shell'
  },
  {
    id: 'pc-22',
    zone: 'Bootcamp',
    name: 'PC-22',
    tone: 'warning',
    stateLabel: 'Внимание',
    player: 'Olim K.',
    remaining: '5 мин',
    billing: 'Wallet',
    device: 'Online · unlocked',
    command: 'Low balance',
    app: 'Dota 2'
  },
  {
    id: 'pc-23',
    zone: 'Bootcamp',
    name: 'PC-23',
    tone: 'offline',
    stateLabel: 'Офлайн',
    player: 'Нет игрока',
    remaining: 'Нет heartbeat',
    billing: 'N/A',
    device: 'Offline',
    command: 'Нет связи с ПК',
    app: 'Shell ?'
  },
  {
    id: 'pc-24',
    zone: 'Bootcamp',
    name: 'PC-24',
    tone: 'active',
    stateLabel: 'В сессии',
    player: 'Ali J.',
    remaining: '26 мин',
    billing: 'Wallet',
    device: 'Online · unlocked',
    command: 'Сессия подтверждена',
    app: 'Valorant'
  }
];
