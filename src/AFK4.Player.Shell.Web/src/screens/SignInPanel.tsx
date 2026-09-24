import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { HostBridgeRequestError, hostBridgeTimeoutCode } from '@afk4/host-bridge';
import {
  isWellFormedPin,
  keepPinDigits,
  PIN_LENGTH,
  ShellBridgeErrorCodeNames,
  ShellBridgeRequestTypeNames,
  type PlayerShellStateDto
} from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatLocal, fullPhoneDigits, localPhoneDigits } from '@afk4/formatting';
import { requestHost } from '../host/shellHost';
import { AssistButton } from '../ui/AssistButton';
import { QrCode } from '../ui/QrCode';
import { SeatBadge } from '../ui/SeatBadge';

interface SignInPanelProps {
  state: PlayerShellStateDto;
  onClose: () => void;
}

/**
 * Окно входа на свободном ПК (спека, §3, кадр 01a): номер и ПИН-код слева, QR с кодом посадки
 * справа. Раскрывается из знака ПК и закрывается по Esc или тишине. Деньги и вход решает сервер:
 * пока ждём ответа — «Входим…», и экран не делает вид, что уже вошли.
 */
export function SignInPanel({ state, onClose }: SignInPanelProps) {
  const { t } = useI18n();
  const [phone, setPhone] = useState('');
  const [pin, setPin] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);
  const phoneField = useRef<HTMLInputElement>(null);
  const phoneId = useId();
  const pinId = useId();

  useEffect(() => {
    phoneField.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  const ready = localPhoneDigits(phone).length === 9 && isWellFormedPin(pin) && !submitting;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!ready) return;
    setSubmitting(true);
    setError(null);
    try {
      await requestHost(ShellBridgeRequestTypeNames.AuthSignIn, { phone: `+${fullPhoneDigits(phone)}`, pin });
      // Вход подтверждён — экран сменит событие auth.changed от хоста.
    } catch (reason) {
      setError(signInErrorKey(reason));
      setPin('');
      setSubmitting(false);
    }
  };

  const code = state.seatingCode;

  return (
    <div className="sign-in" role="dialog" aria-modal="true" aria-labelledby="sign-in-title">
      <div className="sign-in__head">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} free />
        <button type="button" className="sign-in__back" onClick={onClose}>{t('playerShell.signIn.back')}</button>
      </div>

      <form className="sign-in__column" onSubmit={submit} noValidate>
        <h2 id="sign-in-title" className="sign-in__title">{t('playerShell.signIn.title')}</h2>
        <div className="field">
          <label className="field__label" htmlFor={phoneId}>{t('playerShell.signIn.phone')}</label>
          <span className="field__phone">
            <span className="field__prefix mono" aria-hidden="true">+992</span>
            <input
              id={phoneId}
              ref={phoneField}
              className="field__input mono"
              inputMode="tel"
              autoComplete="off"
              value={formatLocal(phone)}
              onChange={(event) => setPhone(localPhoneDigits(event.target.value))}
            />
          </span>
        </div>
        <div className="field">
          <label className="field__label" htmlFor={pinId}>{t('playerShell.signIn.pin')}</label>
          <input
            id={pinId}
            className="field__input field__input--pin mono"
            type="password"
            inputMode="numeric"
            autoComplete="off"
            maxLength={PIN_LENGTH}
            value={pin}
            onChange={(event) => setPin(keepPinDigits(event.target.value))}
          />
        </div>
        {error ? <p className="sign-in__error" role="alert">{t(error)}</p> : null}
        <button type="submit" className="btn btn--primary btn--wide" disabled={!ready}>
          {submitting ? t('playerShell.signIn.submitting') : t('playerShell.signIn.submit')}
        </button>
      </form>

      <div className="sign-in__column sign-in__column--phone">
        <h2 className="sign-in__title">{t('playerShell.signIn.qrTitle')}</h2>
        {code ? (
          <div className="sign-in__qr">
            <QrCode value={`https://afk4.net/s/${code}`} label={t('playerShell.signIn.qrTitle')} />
            <p>{t('playerShell.signIn.qrHint', { code: formatSeatingCode(code) })}</p>
          </div>
        ) : null}
        <AssistButton />
      </div>
    </div>
  );
}

/** «418 207» — две тройки читаются и набираются легче шести цифр подряд. */
export function formatSeatingCode(code: string): string {
  return code.length === 6 ? `${code.slice(0, 3)} ${code.slice(3)}` : code;
}

export function signInErrorKey(reason: unknown): MessageKey {
  const code = reason instanceof HostBridgeRequestError ? reason.code : null;
  switch (code) {
    case ShellBridgeErrorCodeNames.SignInRefused:
      return 'playerShell.signIn.error.refused';
    case ShellBridgeErrorCodeNames.TooManyAttempts:
      return 'playerShell.signIn.error.tooMany';
    case ShellBridgeErrorCodeNames.SessionNotYours:
      return 'playerShell.signIn.error.notYours';
    case ShellBridgeErrorCodeNames.AgentUnavailable:
    case hostBridgeTimeoutCode:
      return 'playerShell.signIn.error.unavailable';
    default:
      return 'playerShell.signIn.error.generic';
  }
}
