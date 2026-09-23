import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { LoadFailureState } from '../operatorPrimitives';
import { ViewOnlyNotice } from './ViewOnlyNotice';

export type SaveState = 'clean' | 'dirty' | 'saving' | 'saved';

export interface ManagementScreenProps {
  title: string;
  subtitle: string;
  children: ReactNode; // destination body (panels/forms)
  // Content column width: 'form' (narrow, single-column config forms) keeps fields a
  // comfortable measure instead of stretching edge-to-edge across the canvas; 'wide' is
  // for list/table-heavy screens (catalog, payment cards); 'full' drops the max-width entirely
  // for screens whose content IS a two-pane grid (halls/devices master-detail) that should use
  // the whole workspace width. Defaults to 'form'.
  contentWidth?: 'form' | 'wide' | 'full';
  // Loading/error swap the body for a skeleton/error affordance instead of children — save bar
  // is suppressed in both. Defaults to 'ready' (renders children as before).
  state?: 'loading' | 'error' | 'ready';
  // What failed and whether «Повторить» can help (projectOperatorError of the load) — the detail
  // is shown as-is, never replaced by generic copy; the retry button appears only when a retry can
  // change the answer (not under a permission refusal, where the access hint takes its place).
  failure?: OperatorErrorProjection;
  onRetry?: () => void;
  // Смотреть экран можно, менять — нет: одна строка «только просмотр» и кто может менять, вместо
  // погашенных без объяснения полей и кнопок.
  viewOnly?: string | null;
  save?: {
    // omit for read-only destinations
    state: SaveState;
    onSave: () => void;
    onDiscard?: () => void; // revert unsaved edits to the last-loaded/saved state; shows «Отменить» when dirty
    disabled?: boolean; // e.g. no backend / no permission
  };
}

export function ManagementScreen({
  title,
  subtitle,
  children,
  contentWidth = 'form',
  state = 'ready',
  failure,
  onRetry,
  viewOnly,
  save
}: ManagementScreenProps): JSX.Element {
  const { t } = useI18n();

  return (
    <section className="workspace-screen management-screen">
      <div className="management-screen-head">
        <span>{subtitle}</span>
        <h1>{title}</h1>
      </div>

      <div className="management-screen-body">
        <div className={`management-content management-content--${contentWidth}`}>
          {state === 'loading' ? (
            <div className="management-skeleton" data-testid="management-skeleton" aria-hidden="true">
              <div className="management-skeleton-line" />
              <div className="management-skeleton-line" />
              <div className="management-skeleton-line" />
              <div className="management-skeleton-line" />
            </div>
          ) : state === 'error' ? (
            <div className="management-error-state">
              <LoadFailureState title={t('op.management.state.errorTitle')} failure={failure ?? projectOperatorError(undefined, t)} onRetry={onRetry} />
            </div>
          ) : (
            <>
              <ViewOnlyNotice reason={viewOnly} />
              {children}

              {save && (
                <div className="management-save-bar">
                  <span>{save.state === 'saved' ? t('op.management.save.saved') : save.state === 'clean' ? t('op.management.save.clean') : ''}</span>
                  {save.onDiscard && (
                    <button
                      type="button"
                      className="ui-btn"
                      disabled={save.state !== 'dirty' || save.disabled}
                      onClick={save.onDiscard}
                    >
                      {t('op.management.save.discard')}
                    </button>
                  )}
                  <button
                    type="button"
                    className="ui-btn ui-btn--primary"
                    disabled={save.state === 'clean' || save.state === 'saving' || save.disabled}
                    onClick={save.onSave}
                  >
                    {t('common.save')}
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </section>
  );
}
