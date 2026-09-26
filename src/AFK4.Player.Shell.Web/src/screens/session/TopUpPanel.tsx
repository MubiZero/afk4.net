import { useCallback, useEffect, useId, useRef, useState } from 'react';
import type { PlayerDashboardDto, PlayerTopUpIntentDto, PlayerTopUpMethodsDto } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney, majorToMinor } from '@afk4/money';
import { CheckCircle2 } from 'lucide-react';
import { getJson, postJson } from '../../api/playerApi';
import { INTL_LOCALES } from '../../model/offers';
import { BANK_POLL_MS, TOP_UP_PRESETS_MAJOR, outcomeOf, parseMajorAmount, type BankPayment } from '../../model/topUp';
import { topUpQrPayload } from '../../topUpQrPayload';
import { AssistButton } from '../../ui/AssistButton';
import { QrCode } from '../../ui/QrCode';

type Stage =
  | { kind: 'choose' }
  | { kind: 'creating' }
  | { kind: 'waiting'; intent: PlayerTopUpIntentDto; qr: string; since: number }
  | { kind: 'paid'; minorUnits: number };

/**
 * Пополнение с телефона по QR на экране ПК (онлайн-оплата банка, как в приложении). Оплачено —
 * только по слову сервера после зачисления: «код отсканировали» ничего не значит. Не дождались
 * банка за пять минут — экран говорит, что деньги придут сами, а не пугает ошибкой.
 */
export function TopUpPanel({
  baseUrl,
  onPaid,
  pollMs = BANK_POLL_MS
}: {
  baseUrl: string;
  onPaid?: () => void;
  /** Как часто спрашивать банк; тесты не ждут по три секунды на вопрос. */
  pollMs?: number;
}) {
  const { t, locale } = useI18n();
  const customId = useId();
  const [methods, setMethods] = useState<PlayerTopUpMethodsDto | null>(null);
  const [balance, setBalance] = useState<PlayerDashboardDto['walletBalance'] | null>(null);
  const [amountMinor, setAmountMinor] = useState<number | null>(null);
  const [custom, setCustom] = useState('');
  const [stage, setStage] = useState<Stage>({ kind: 'choose' });
  const [problem, setProblem] = useState<MessageKey | null>(null);
  const idempotencyKey = useRef(crypto.randomUUID());
  // Родитель передаёт новый колбэк на каждой перерисовке — таймер опроса банка из-за этого
  // перезапускаться не должен.
  const onPaidRef = useRef(onPaid);
  onPaidRef.current = onPaid;

  const currency = balance?.currencyCode ?? 'TJS';
  const money = (minorUnits: number) => formatMoney(minorUnits, currency, INTL_LOCALES[locale]);

  const loadBalance = useCallback(async () => {
    try {
      setBalance((await getJson<PlayerDashboardDto>(baseUrl, '/api/me/dashboard')).walletBalance);
    } catch {
      // Баланс — подсказка, а не условие: без него пополнить всё равно можно.
    }
  }, [baseUrl]);

  useEffect(() => {
    void loadBalance();
    getJson<PlayerTopUpMethodsDto>(baseUrl, '/api/me/wallet/top-up-methods')
      .then(setMethods)
      // Не узнали — предлагаем стойку: она работает всегда, и это честнее, чем показать онлайн-оплату,
      // о которой ничего не известно.
      .catch(() => setMethods({ counter: true, online: false }));
  }, [baseUrl, loadBalance]);

  const waiting = stage.kind === 'waiting' ? stage : null;
  useEffect(() => {
    if (!waiting) return;
    const timer = window.setInterval(async () => {
      let payment: BankPayment;
      try {
        payment = (await postJson<{ payment: BankPayment }>(
          baseUrl, `/api/me/wallet/top-up-intents/${waiting.intent.paymentIntentId}/eskhata-status`, {})).payment;
      } catch {
        // Обрыв связи — «пока не знаем»: следующий вопрос через несколько секунд.
        return;
      }
      const outcome = outcomeOf(payment, Date.now() - waiting.since, waiting.intent.amountMinorUnits);
      if (!outcome) return;
      if (outcome.kind === 'paid') {
        setStage({ kind: 'paid', minorUnits: outcome.minorUnits });
        idempotencyKey.current = crypto.randomUUID();
        void loadBalance();
        onPaidRef.current?.();
      } else {
        setProblem(outcome.key);
        setStage({ kind: 'choose' });
        idempotencyKey.current = crypto.randomUUID();
      }
    }, pollMs);
    return () => window.clearInterval(timer);
  }, [waiting, baseUrl, loadBalance, pollMs]);

  const start = async () => {
    if (!amountMinor || stage.kind !== 'choose') return;
    setProblem(null);
    setStage({ kind: 'creating' });
    try {
      const intent = await postJson<PlayerTopUpIntentDto>(baseUrl, '/api/me/wallet/top-up-intent', {
        amountMinorUnits: amountMinor,
        currencyCode: null,
        method: 'eskhata',
        idempotencyKey: idempotencyKey.current
      });
      const qr = topUpQrPayload(intent);
      if (!qr) throw new Error('The bank gave nothing to scan.');
      setStage({ kind: 'waiting', intent, qr, since: Date.now() });
    } catch {
      setProblem('playerShell.topUp.busy');
      setStage({ kind: 'choose' });
    }
  };

  if (methods && !methods.online) {
    return (
      <div className="top-up">
        <p className="top-up__note">{t('playerShell.topUp.counterOnly')}</p>
        <AssistButton />
      </div>
    );
  }

  if (stage.kind === 'paid') {
    return (
      <div className="top-up top-up--done" role="status">
        <CheckCircle2 className="top-up__done-icon" aria-hidden="true" />
        <p className="top-up__done">{t('playerShell.topUp.paid', { amount: money(stage.minorUnits) })}</p>
        {balance ? <p className="top-up__note">{t('playerShell.chooseTime.balance', { amount: money(balance.minorUnits) })}</p> : null}
        <button type="button" className="btn btn--ghost" onClick={() => setStage({ kind: 'choose' })}>{t('playerShell.topUp.again')}</button>
      </div>
    );
  }

  if (stage.kind === 'waiting') {
    return (
      <div className="top-up top-up--qr">
        <div className="top-up__qr">
          <QrCode value={stage.qr} label={t('playerShell.topUp.qrLabel', { amount: money(stage.intent.amountMinorUnits) })} />
        </div>
        <div className="top-up__qr-text">
          <p className="top-up__amount mono">{money(stage.intent.amountMinorUnits)}</p>
          <p>{t('playerShell.topUp.scan')}</p>
          <p className="top-up__waiting" role="status">{t('playerShell.topUp.waiting')}</p>
          <button type="button" className="btn btn--ghost" onClick={() => setStage({ kind: 'choose' })}>{t('playerShell.topUp.back')}</button>
        </div>
      </div>
    );
  }

  const choosePreset = (major: number) => {
    setCustom('');
    setAmountMinor(majorToMinor(major));
  };
  const customInvalid = custom.trim() !== '' && parseMajorAmount(custom) === null;

  return (
    <div className="top-up">
      {balance ? <p className="top-up__note">{t('playerShell.chooseTime.balance', { amount: money(balance.minorUnits) })}</p> : null}
      <div className="offer-group__options" role="group" aria-label={t('playerShell.topUp.pick')}>
        {TOP_UP_PRESETS_MAJOR.map((major) => (
          <button
            key={major}
            type="button"
            className="offer-tile top-up__preset"
            aria-pressed={custom === '' && amountMinor === majorToMinor(major)}
            disabled={stage.kind !== 'choose'}
            onClick={() => choosePreset(major)}
          >
            <span className="offer-tile__duration mono">{money(majorToMinor(major))}</span>
          </button>
        ))}
      </div>
      <label className="field top-up__custom" htmlFor={customId}>
        <span className="field__label">{t('playerShell.topUp.custom')}</span>
        <input
          id={customId}
          className="field__input"
          inputMode="decimal"
          value={custom}
          aria-invalid={customInvalid}
          onChange={(event) => {
            setCustom(event.currentTarget.value);
            setAmountMinor(parseMajorAmount(event.currentTarget.value));
          }}
        />
      </label>
      {customInvalid ? <p className="top-up__problem" role="alert">{t('playerShell.topUp.invalidAmount')}</p> : null}
      {problem ? <p className="top-up__problem" role="alert">{t(problem)}</p> : null}
      <div className="top-up__action">
        <button type="button" className="btn btn--primary" disabled={!amountMinor || stage.kind !== 'choose'} onClick={() => void start()}>
          {stage.kind === 'creating'
            ? t('playerShell.topUp.creating')
            : amountMinor
              ? t('playerShell.topUp.pay', { amount: money(amountMinor) })
              : t('playerShell.topUp.pick')}
        </button>
      </div>
    </div>
  );
}
