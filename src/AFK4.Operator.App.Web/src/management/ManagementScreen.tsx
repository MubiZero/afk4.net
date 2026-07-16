import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';

export type SaveState = 'clean' | 'dirty' | 'saving' | 'saved';

export interface ManagementScreenProps {
  title: string;
  subtitle: string;
  children: ReactNode; // destination body (panels/forms)
  // Content column width: 'form' (narrow, single-column config forms) keeps fields a
  // comfortable measure instead of stretching edge-to-edge across the canvas; 'wide' is
  // for list/table-heavy screens (halls, catalog, payment cards). Defaults to 'form'.
  contentWidth?: 'form' | 'wide';
  save?: {
    // omit for read-only destinations
    state: SaveState;
    onSave: () => void;
    disabled?: boolean; // e.g. no backend / no permission
  };
}

export function ManagementScreen({ title, subtitle, children, contentWidth = 'form', save }: ManagementScreenProps): JSX.Element {
  const { t } = useI18n();

  return (
    <section className="workspace-screen management-screen">
      <div className="management-screen-head">
        <span>{subtitle}</span>
        <h1>{title}</h1>
      </div>

      <div className="management-screen-body">
        <div className={`management-content management-content--${contentWidth}`}>
          {children}

          {save && (
            <div className="management-save-bar">
              <span>{save.state === 'saved' ? t('op.management.save.saved') : save.state === 'clean' ? t('op.management.save.clean') : ''}</span>
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
        </div>
      </div>
    </section>
  );
}
