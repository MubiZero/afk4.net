import { useState } from 'react';
import { AlertTriangle, LoaderCircle, LogOut, RefreshCw, Unlock } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { AuthFrame } from './AuthFrame';
import {
  createIdempotencyKey,
  parseNonNegativeMoneyInputMinorUnits
} from './operatorHelpers';
import type { PostAuthShiftGateController } from './usePostAuthShiftGate';

export function PostAuthShiftGate({
  controller,
  organizationId,
  currencyCode,
  onSignOut
}: {
  controller: PostAuthShiftGateController;
  organizationId: string;
  currencyCode: string;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const [startingCash, setStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState(t('op.cash.open.defaultNote'));
  const [localError, setLocalError] = useState<string | null>(null);
  const checking = controller.status === 'checking';
  const opening = controller.status === 'opening';
  const checkFailed = controller.status === 'failed' && controller.failureKind === 'check';
  const showForm = controller.status === 'required'
    || opening
    || (controller.status === 'failed' && controller.failureKind === 'open');

  const submit = () => {
    if (opening) return;
    const minorUnits = parseNonNegativeMoneyInputMinorUnits(startingCash);
    if (minorUnits === null) {
      setLocalError(t('op.shiftGate.cashInvalid'));
      return;
    }

    setLocalError(null);
    void controller.openShift({
      organizationId,
      startingCash: { currencyCode, minorUnits },
      openingNote: openingNote.trim(),
      idempotencyKey: createIdempotencyKey('shift-open')
    });
  };

  return (
    <AuthFrame>
      <section className="auth-panel shift-gate-panel" aria-busy={checking || opening}>
        <header className="auth-panel-head">
          <Unlock className="shift-gate-mark" size={36} aria-hidden="true" />
          <h1>{t('op.shiftGate.title')}</h1>
          <p>{t('op.shiftGate.subtitle')}</p>
        </header>

        {checking && (
          <div className="shift-gate-loading" role="status">
            <LoaderCircle className="auth-spinner" size={20} aria-hidden="true" />
            <span>{t('op.shiftGate.checking')}</span>
          </div>
        )}

        {checkFailed && (
          <>
            <div className="auth-error" role="alert">
              <AlertTriangle size={17} aria-hidden="true" />
              <span>{controller.error}</span>
            </div>
            <button type="button" className="auth-primary" onClick={controller.retry}>
              <RefreshCw size={16} aria-hidden="true" />
              <span>{t('op.shiftGate.retry')}</span>
            </button>
          </>
        )}

        {showForm && (
          <form
            className="auth-form"
            onSubmit={(event) => {
              event.preventDefault();
              submit();
            }}
          >
            <label className="auth-field">
              <span className="auth-field-label">{t('op.cash.open.startingCashLabel')}</span>
              <input
                inputMode="decimal"
                value={startingCash}
                disabled={opening}
                onChange={(event) => setStartingCash(event.currentTarget.value)}
              />
            </label>
            <label className="auth-field">
              <span className="auth-field-label">{t('op.cash.open.noteLabel')}</span>
              <input
                value={openingNote}
                disabled={opening}
                onChange={(event) => setOpeningNote(event.currentTarget.value)}
              />
            </label>
            {(localError ?? controller.error) && (
              <div className="auth-error" role="alert">
                <AlertTriangle size={17} aria-hidden="true" />
                <span>{localError ?? controller.error}</span>
              </div>
            )}
            <button type="submit" className="auth-primary" disabled={opening}>
              {opening
                ? <LoaderCircle className="auth-spinner" size={16} aria-hidden="true" />
                : <Unlock size={16} aria-hidden="true" />}
              <span>{t('op.cash.open.submit')}</span>
            </button>
          </form>
        )}

        <button type="button" className="shift-gate-signout" onClick={onSignOut}>
          <LogOut size={15} aria-hidden="true" />
          <span>{t('op.shiftGate.signOut')}</span>
        </button>
      </section>
    </AuthFrame>
  );
}
