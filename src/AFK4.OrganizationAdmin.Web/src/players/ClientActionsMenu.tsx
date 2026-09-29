import type { TFunc } from '../operatorHelpers';
import type { RowAction } from '@afk4/ui/react';
import { CalendarClock, Pencil, Power, PowerOff, SlidersHorizontal, Ticket } from 'lucide-react';

// Пункты меню «⋯» в шапке карточки клиента. Каждый пункт гейтится своим правом, независимо от
// права править профиль: корректировать можно и без права менять имя. Пустой список — меню нет
// вовсе (RowActions кита сам не рисует кнопку, за которой пусто).
//
// «Посадить за ПК» и деньги сюда не входят: это частые действия, и они стоят кнопками на виду.
// Корректировка — наоборот, только здесь: раньше она была и кнопкой, и пунктом меню.
export function clientMenuActions(t: TFunc, {
  isActive,
  canManageClient,
  onEditProfile,
  onToggleActive,
  canCreateReservation = false,
  onCreateReservation,
  canSellPackage = false,
  onSellPackage,
  canCorrect = false,
  onCorrect,
}: {
  isActive: boolean;
  canManageClient: boolean;
  onEditProfile: () => void;
  onToggleActive: () => void;
  canCreateReservation?: boolean;
  onCreateReservation?: () => void;
  canSellPackage?: boolean;
  onSellPackage?: () => void;
  canCorrect?: boolean;
  onCorrect?: () => void;
}): RowAction[] {
  const items: RowAction[] = [];
  if (canCreateReservation && onCreateReservation) {
    items.push({
      id: 'reservation',
      label: t('op.players.actions.bookingBtn'),
      icon: <CalendarClock size={14} aria-hidden="true" />,
      onSelect: onCreateReservation,
    });
  }
  // Пакет продаётся тому, чья карточка открыта: раньше за этим уходили в Кассу и искали
  // того же человека второй раз.
  if (canSellPackage && onSellPackage) {
    items.push({
      id: 'sellPackage',
      label: t('op.players.packages.sellBtn'),
      icon: <Ticket size={14} aria-hidden="true" />,
      onSelect: onSellPackage,
    });
  }
  if (canManageClient) {
    items.push({
      id: 'edit',
      label: t('op.players.actions.editProfileLabel'),
      icon: <Pencil size={14} aria-hidden="true" />,
      onSelect: onEditProfile,
    });
  }
  if (canCorrect && onCorrect) {
    items.push({
      id: 'correction',
      label: t('op.players.correction.openLink'),
      icon: <SlidersHorizontal size={14} aria-hidden="true" />,
      onSelect: onCorrect,
    });
  }
  // Деактивация — опасное действие: RowActions кита отбивает его разделителем, если перед ним есть
  // что отделять.
  if (canManageClient) {
    items.push({
      id: 'toggle',
      label: isActive ? t('op.players.actions.deactivateLabel') : t('op.players.actions.reactivateLabel'),
      icon: isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />,
      onSelect: onToggleActive,
      danger: isActive,
    });
  }
  return items;
}
