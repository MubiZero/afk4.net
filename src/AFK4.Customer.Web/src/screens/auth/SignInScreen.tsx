import { useEffect, useState, type FormEvent } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type {
  PlatformPersonSessionResponse, RegistrationConfirmRequest,
  RegistrationStartRequest, RegistrationStartedResponse
} from '@/api/types';

interface SignInScreenProps {
  brandName: string;
  startSignIn: (request: RegistrationStartRequest) => Promise<RegistrationStartedResponse>;
  confirmSignIn: (request: RegistrationConfirmRequest) => Promise<PlatformPersonSessionResponse>;
  onSignedIn: (response: PlatformPersonSessionResponse) => void;
}

// Коды сервера переводятся здесь: человеку нужно знать, что делать дальше, а не как эндпоинт
// назвал отказ. Всё, чего в списке нет, — это «связи нет», а не «вы что-то сделали не так».
const CONFIRM_ERRORS: Record<string, MessageKey> = {
  invalid_code: 'customer.signin.codeError',
  code_expired: 'customer.signin.codeExpired',
  no_active_code: 'customer.signin.codeNone',
  too_many_attempts: 'customer.signin.codeTooMany',
  account_disabled: 'customer.signin.accountDisabled'
};

/** Отказ по существу называется своим именем; «слишком часто» отличается от «нет связи», иначе
 *  человек чинил бы интернет там, где надо просто подождать. */
function describeFailure(caught: unknown, known: Record<string, MessageKey>): MessageKey {
  const failure = caught as { message?: string; status?: number };
  const named = known[failure.message ?? ''];
  if (named) return named;
  return failure.status === 429 ? 'customer.signin.codeTooMany' : 'customer.signin.networkError';
}

export function SignInScreen({ brandName, startSignIn, confirmSignIn, onSignedIn }: SignInScreenProps) {
  const { t } = useI18n();
  const [phoneNumber, setPhoneNumber] = useState('');
  const [code, setCode] = useState('');
  const [sent, setSent] = useState<RegistrationStartedResponse | null>(null);
  const [resendIn, setResendIn] = useState(0);
  const [error, setError] = useState<MessageKey | null>(null);
  const [pending, setPending] = useState(false);

  // Обратный отсчёт до повторной отправки: кнопка «ещё раз», которая молча не работает, выглядит
  // как сломанная — лучше честно показать, сколько ждать.
  useEffect(() => {
    if (resendIn <= 0) return;
    const id = setInterval(() => setResendIn((left) => Math.max(0, left - 1)), 1000);
    return () => clearInterval(id);
  }, [resendIn]);

  // Тот же обработчик обслуживает и отправку формы, и кнопку «прислать ещё раз», поэтому от
  // события ему нужно ровно одно — не дать браузеру перезагрузить страницу.
  async function handleStart(event: { preventDefault: () => void }) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      const started = await startSignIn({ phoneNumber });
      setSent(started);
      setCode('');
      setResendIn(started.resendAfterSeconds);
    } catch (caught: unknown) {
      setError(describeFailure(caught, { invalid_phone: 'customer.signin.phoneError' }));
    } finally {
      setPending(false);
    }
  }

  async function handleConfirm(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      onSignedIn(await confirmSignIn({ phoneNumber, code }));
    } catch (caught: unknown) {
      setError(describeFailure(caught, CONFIRM_ERRORS));
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col justify-center gap-8 px-6 py-12">
      <header className="space-y-1">
        <p className="text-sm text-[var(--text-2)]">{t('customer.signin.title')}</p>
        <h1 className="text-3xl font-extrabold tracking-tight">{brandName}</h1>
      </header>

      {sent === null ? (
        <form className="space-y-4" onSubmit={handleStart}>
          <div className="space-y-1.5">
            <label htmlFor="phone" className="text-sm text-[var(--text-2)]">{t('customer.signin.phone')}</label>
            <Input id="phone" type="tel" inputMode="tel" autoComplete="tel"
              value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="+992 90 000 00 01" />
            <p className="text-sm text-[var(--text-3)]">{t('customer.signin.subtitle')}</p>
          </div>

          {error && <p role="alert" className="text-sm text-red-400">{t(error)}</p>}

          <Button type="submit" className="w-full" disabled={pending}>
            {pending ? t('customer.signin.sending') : t('customer.signin.sendCode')}
          </Button>

          <p className="text-sm text-[var(--text-3)]">{t('customer.signin.newHere')}</p>
        </form>
      ) : (
        <form className="space-y-4" onSubmit={handleConfirm}>
          <div className="space-y-1.5">
            <label htmlFor="code" className="text-sm text-[var(--text-2)]">{t('customer.signin.code')}</label>
            <Input id="code" type="text" inputMode="numeric" autoComplete="one-time-code"
              value={code} onChange={(e) => setCode(e.target.value)} />
            <p className="text-sm text-[var(--text-3)]">
              {t('customer.signin.codeSentTo', { phone: phoneNumber })}
              {' '}
              {t('customer.signin.codeLifetime', { minutes: Math.max(1, Math.round(sent.expiresInSeconds / 60)) })}
            </p>
          </div>

          {error && <p role="alert" className="text-sm text-red-400">{t(error)}</p>}

          <Button type="submit" className="w-full" disabled={pending}>
            {pending ? t('customer.signin.submitting') : t('customer.signin.submit')}
          </Button>

          <div className="flex items-center justify-between gap-3">
            <button type="button" onClick={() => { setSent(null); setError(null); }}
              className="min-h-[44px] text-sm text-[var(--text-2)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
              {t('customer.signin.changePhone')}
            </button>
            {resendIn > 0 ? (
              <span className="text-sm text-[var(--text-3)]">{t('customer.signin.resendIn', { seconds: resendIn })}</span>
            ) : (
              <button type="button" onClick={handleStart} disabled={pending}
                className="min-h-[44px] text-sm text-[var(--accent)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
                {t('customer.signin.resend')}
              </button>
            )}
          </div>
        </form>
      )}
    </main>
  );
}
