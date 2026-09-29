import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { RowActionsMenu } from './RowActionsMenu';
import type { RowAction } from './types';
import { CloseButton } from '@afk4/ui/react';

// Правая панель деталей/редактирования выбранной записи. Управляемая: родитель рендерит её по
// условию (запись выбрана) вторым столбцом грида .mgmt-master-detail; при закрытой панели таблица
// занимает всю ширину. Не модалка — без бэкдропа/фокус-трапа (для create/edit используем PanelModal).
// Head: заголовок/подзаголовок + опц. ⋯-меню действий записи + крестик закрытия. Body скроллится.
// `onClose` опционален: мастер-детейл разделы (напр. «Залы и места») держат панель ПОСТОЯННОЙ —
// без крестика, т.к. деталь всегда показывает выбранную (автовыбранную) запись, закрывать нечего.
export function MgmtDrawer({
  title,
  subtitle,
  actions,
  onClose,
  children,
  footer
}: {
  title: string;
  subtitle?: string;
  actions?: RowAction[];
  onClose?: () => void;
  children: ReactNode;
  footer?: ReactNode;
}) {
  const { t } = useI18n();

  return (
    <aside className="mgmt-drawer">
      <div className="mgmt-drawer-head">
        <div className="mgmt-drawer-id">
          <div className="mgmt-drawer-title">{title}</div>
          {subtitle && <div className="mgmt-drawer-subtitle">{subtitle}</div>}
        </div>
        {actions && actions.length > 0 && <RowActionsMenu actions={actions} />}
        {onClose && (
          <CloseButton label={t('common.close')} onClick={onClose} />
        )}
      </div>

      <div className="mgmt-drawer-body">{children}</div>

      {footer && <div className="mgmt-drawer-footer">{footer}</div>}
    </aside>
  );
}
