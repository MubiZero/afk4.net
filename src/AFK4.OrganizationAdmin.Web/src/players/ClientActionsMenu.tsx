import { useI18n } from '@afk4/i18n';
import { RowActions, type RowAction } from '@afk4/ui/react';
import { CalendarClock, MonitorPlay, Pencil, Power, PowerOff, SlidersHorizontal, Ticket } from 'lucide-react';

// Меню «⋯» действий с клиентом в шапке карточки/drawer'а. Пункты собираются динамически по
// правам — «Бронь»/«Корректировка» гейтятся СВОИМИ флагами независимо от canManageClient
// (оператор может иметь право на корректировку без права редактировать профиль). Пустое меню
// (ни одного разрешённого пункта) не рендерится вовсе — вызывающий код должен решить, показывать
// ли триггер «⋯», по тому же условию (canManageClient || canCreateReservation || canCorrect).
// Клавиатура, фокус и закрытие — у RowActions кита.
export function ClientActionsMenu({
  isActive,
  canManageClient,
  onEditProfile,
  onToggleActive,
  canCreateReservation = false,
  onCreateReservation,
  canSellPackage = false,
  onSellPackage,
  canStartSession = false,
  onStartSession,
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
  canStartSession?: boolean;
  onStartSession?: () => void;
  canCorrect?: boolean;
  onCorrect?: () => void;
}) {
  const { t } = useI18n();

  const showReservation = canCreateReservation && Boolean(onCreateReservation);
  const showSellPackage = canSellPackage && Boolean(onSellPackage);
  const showStartSession = canStartSession && Boolean(onStartSession);
  const showCorrection = canCorrect && Boolean(onCorrect);

  const items: RowAction[] = [];
  if (showReservation) {
    items.push({
      id: 'reservation',
      label: t('op.players.actions.bookingBtn'),
      icon: <CalendarClock size={14} aria-hidden="true" />,
      onSelect: onCreateReservation!,
    });
  }
  // Посадить за ПК отсюда же: иначе это третий поиск того же человека за визит — после кассы
  // и после этой самой карточки.
  if (showStartSession) {
    items.push({
      id: 'startSession',
      label: t('op.players.session.start'),
      icon: <MonitorPlay size={14} aria-hidden="true" />,
      onSelect: onStartSession!,
    });
  }
  // Пакет продаётся тому, чья карточка открыта: раньше за этим уходили в Кассу и искали
  // того же человека второй раз.
  if (showSellPackage) {
    items.push({
      id: 'sellPackage',
      label: t('op.players.packages.sellBtn'),
      icon: <Ticket size={14} aria-hidden="true" />,
      onSelect: onSellPackage!,
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
  if (showCorrection) {
    items.push({
      id: 'correction',
      label: t('op.players.correction.openLink'),
      icon: <SlidersHorizontal size={14} aria-hidden="true" />,
      onSelect: onCorrect!,
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

  return <RowActions label={t('op.players.menu.open')} actions={items} />;
}
