import { useState } from 'react';
import { ShellBridgeRequestTypeNames } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { Bell } from 'lucide-react';
import { requestHost } from '../host/shellHost';

type AssistState = 'idle' | 'sending' | 'sent' | 'failed';

/**
 * «Позвать администратора». «Идёт» — только когда стойка узнала: агент отвечает, дошёл ли вызов.
 * Раньше кнопка отвечала «позвали» и не звала никого.
 */
export function AssistButton() {
  const { t } = useI18n();
  const [state, setState] = useState<AssistState>('idle');

  const call = async () => {
    setState('sending');
    try {
      await requestHost(ShellBridgeRequestTypeNames.AssistCall);
      setState('sent');
    } catch {
      setState('failed');
    }
  };

  if (state === 'sent') {
    return <p className="assist assist--sent" role="status">{t('playerShell.assist.sent')}</p>;
  }

  return (
    <div className="assist">
      <button type="button" className="btn btn--ghost" onClick={call} disabled={state === 'sending'}>
        <Bell aria-hidden="true" />
        {t('playerShell.assist.call')}
      </button>
      {state === 'failed' ? <p className="assist__error" role="alert">{t('playerShell.assist.failed')}</p> : null}
    </div>
  );
}
