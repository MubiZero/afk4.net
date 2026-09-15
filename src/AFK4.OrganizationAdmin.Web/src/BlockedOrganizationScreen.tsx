import { AlertTriangle } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { OperatorOrganizationStatus, type ResolveOperatorConnectionResponse } from './connectionResolver';
import { WindowControls, WindowResizeHandles, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';

export function BlockedOrganizationScreen({
  resolution,
  onChangeConnection
}: {
  resolution: ResolveOperatorConnectionResponse;
  onChangeConnection: () => void;
}) {
  const { t } = useI18n();
  const isDeletionPending = resolution.organizationStatus === OperatorOrganizationStatus.DeletionPending;
  const headline = isDeletionPending ? t('op.shell.club.deletionPending') : t('op.shell.club.suspended');
  const reason = resolution.organizationStatusReason?.trim();
  // Вход — свободный экран: одна задача во весь монитор, спешить некуда, и рука должна
  // попадать без прицеливания. Плотная консоль начинается уже за ним.
  return (
    <div className="operator-shell auth-shell" data-density="comfortable">
      <WindowResizeHandles />
      <header
        className="top-command auth-top-command"
        onMouseDown={handleWindowDragStart}
        onDoubleClick={handleWindowTitleDoubleClick}
      >
        <div className="brand-block">
          <img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>{resolution.organizationName}</span>
            <h1>{headline}</h1>
            <p>
              {reason !== undefined && reason.length > 0
                ? reason
                : t('op.shell.club.contactOwner')}
            </p>
          </header>

          <div className="ui-alert" role="alert">
            <AlertTriangle size={16} />
            <span>
              {isDeletionPending
                ? t('op.shell.club.deletionMsg')
                : t('op.shell.club.suspendedMsg')}
            </span>
          </div>

          <button type="button" className="primary-wide" onClick={onChangeConnection}>
            {t('op.shell.club.changeConnection')}
          </button>
        </section>
      </main>
    </div>
  );
}
