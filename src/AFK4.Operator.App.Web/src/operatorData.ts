import type { LucideIcon } from 'lucide-react';
import {
  Box,
  CalendarClock,
  LayoutDashboard,
  Monitor,
  ReceiptText,
  Settings,
  Users
} from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { WorkspaceId } from './operatorTypes';

// Консолидированная модель: 4 состояния, цвет = действие оператора.
//  ready    — свободно (зелёный): посадить гостя
//  active   — в сессии (нейтральный): следить
//  pending  — ожидание команды (янтарь): подождать
//  offline  — нет связи / ПК недоступен (серый, требует внимания): сбой команды, мёртвый
//             heartbeat или сессия без связи с ПК — один бакет «иди проверь ПК». Конкретную
//             причину несёт строка-тело плитки; активная сессия (время/сумма) сохраняется.
//  service  — обслуживание (тот же серый, но спокойный): ПК намеренно снят с линии. Это НЕ
//             сбой связи, поэтому в фильтр «Нет связи» не попадает (только в «Все»).
export type SeatTone = 'ready' | 'active' | 'pending' | 'offline' | 'service';

export interface SeatSummary {
  id: string;
  zone: string;
  name: string;
  tone: SeatTone;
  stateLabel: string;
  player: string;
  remaining: string;
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
  // Real session identity (B1 DTO): the account player name, the billed tariff name, and
  // the start instant — null when the backend has none (guest, free seat) or on fixtures.
  playerDisplayName?: string | null;
  tariffName?: string | null;
  sessionStartedAtUtc?: string | null;
  // Floor-plan geometry (B2 DTO). Null/default until the seat is placed in the «План» editor.
  posX?: number | null;
  posY?: number | null;
  rotation?: number;
  seatType?: string;
  // The seat's zone: required by the «План» editor's full-replace PUT (B2-3) to bind each seat
  // to its zone in the saved layout payload.
  zoneId?: string | null;
}

export interface NavItem {
  id: WorkspaceId;
  labelKey: MessageKey;
}

export interface NavSection {
  // Стабильный ключ раздела (для подсветки/aria); для одиночных совпадает с workspace id.
  key: string;
  labelKey: MessageKey;
  icon: LucideIcon;
  items: NavItem[];
}

// Рельс собран в разделы по смыслу и частоте. Часто используемые экраны (зал, брони, клиенты)
// остаются отдельными кнопками — без лишнего клика. Родственные «бэк-офисные» экраны слиты в
// разделы с вкладками: Касса (продажи/деньги), Отчёты, Управление (вся конфигурация клуба).
// Это сжимает рельс с 14 кнопок до 6. Права по-прежнему скрывают/глушат отдельные вкладки.
export const navSections: NavSection[] = [
  { key: 'map', labelKey: 'op.shell.nav.map', icon: Monitor, items: [{ id: 'map', labelKey: 'op.shell.nav.map' }] },
  {
    key: 'cashier',
    labelKey: 'op.shell.navGroup.cashier',
    icon: ReceiptText,
    items: [
      { id: 'cash', labelKey: 'op.shell.navGroup.cashier' }
    ]
  },
  { key: 'booking', labelKey: 'op.shell.nav.booking', icon: CalendarClock, items: [{ id: 'booking', labelKey: 'op.shell.nav.booking' }] },
  { key: 'players', labelKey: 'op.shell.nav.players', icon: Users, items: [{ id: 'players', labelKey: 'op.shell.nav.players' }] },
  {
    key: 'stock',
    labelKey: 'op.shell.navGroup.warehouse',
    icon: Box,
    items: [{ id: 'stock', labelKey: 'op.shell.navGroup.warehouse' }]
  },
  {
    key: 'reports',
    labelKey: 'op.shell.navGroup.reports',
    icon: LayoutDashboard,
    items: [
      { id: 'dashboard', labelKey: 'op.shell.nav.dashboard' }
    ]
  },
  {
    key: 'admin',
    labelKey: 'op.shell.navGroup.management',
    icon: Settings,
    items: [
      { id: 'settings', labelKey: 'op.shell.nav.settings' },
      { id: 'payment_cards', labelKey: 'op.shell.nav.payment_cards' },
      { id: 'loyalty', labelKey: 'op.loyalty.nav' },
      { id: 'news', labelKey: 'op.news.nav' },
      { id: 'logs', labelKey: 'op.shell.nav.logs' }
    ]
  }
];
