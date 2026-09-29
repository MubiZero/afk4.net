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
/** `wide` — во всю ширину колонки, как соседние кнопки в колонке сессии. */
export function AssistButton({ wide = false }: { wide?: boolean }) {
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
    return <p className="banner banner--success" role="status">{t('playerShell.assist.sent')}</p>;
  }

  return (
    <div className={wide ? 'assist assist--wide' : 'assist'}>
      <button type="button" className={wide ? 'btn btn--ghost btn--wide' : 'btn btn--ghost'} onClick={call} disabled={state === 'sending'}>
        <Bell aria-hidden="true" />
        {t('playerShell.assist.call')}
      </button>
      {state === 'failed' ? <p className="banner banner--danger" role="alert">{t('playerShell.assist.failed')}</p> : null}
    </div>
  );
}
