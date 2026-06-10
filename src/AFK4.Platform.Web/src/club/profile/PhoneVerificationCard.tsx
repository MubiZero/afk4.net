import { useEffect, useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import type { ProfileApi } from '@/api/clients/profile';

type Client = Pick<ProfileApi, 'getStaffPhone' | 'startPhoneVerification' | 'confirmPhoneVerification'>;
type Phase = 'loading' | 'idle' | 'code' | 'verified' | 'error';

// Backend error code → i18n key. t() has no interpolation, so the numeric
// "remaining attempts" detail is appended by concatenation in describe()
// (mirroring the desktop card) rather than interpolated.
const ERROR_KEYS: Record<string, MessageKey> = {
  invalid_phone: 'account.phone.err.invalid_phone',
  cooldown_active: 'account.phone.err.cooldown',
  rate_limited: 'account.phone.err.rate_limited',
  sms_unavailable: 'account.phone.err.sms_unavailable',
  invalid_code: 'account.phone.err.invalid_code',
  code_expired: 'account.phone.err.expired',
  no_active_code: 'account.phone.err.expired',
  too_many_attempts: 'account.phone.err.too_many',
  phone_already_in_use: 'account.phone.err.in_use'
};

export function PhoneVerificationCard({ client }: { client: Client }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [phase, setPhase] = useState<Phase>('loading');
  const [currentPhone, setCurrentPhone] = useState<string | null>(null);
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let disposed = false;
    setPhase('loading');
    setError(null);
    void (async () => {
      try {
        const status = await client.getStaffPhone();
        if (disposed) return;
        if (status && status.phoneVerifiedAtUtc !== null) {
          setCurrentPhone(status.phone);
          setPhase('verified');
        } else {
          setPhase('idle');
        }
      } catch {
        if (!disposed) setPhase('error');
      }
    })();
    return () => { disposed = true; };
  }, [client, reloadKey]);

  function describe(err: unknown): string {
    if (err instanceof PlatformApiError) {
      if (err.errorCode === 'invalid_code' && typeof err.remainingAttempts === 'number') {
        return `${t('account.phone.invalidCodeAttempts')} ${err.remainingAttempts}`;
      }
      if (err.errorCode !== null && err.errorCode in ERROR_KEYS) {
        return t(ERROR_KEYS[err.errorCode]);
      }
    }
    return t('account.phone.err.generic');
  }

  async function sendCode() {
    setBusy(true);
    setError(null);
    try {
      await client.startPhoneVerification(phone.trim());
      setCode('');
      setPhase('code');
    } catch (err) {
      setError(describe(err));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    setBusy(true);
    setError(null);
    try {
      const result = await client.confirmPhoneVerification(code.trim());
      setCurrentPhone(result.phone);
      setPhase('verified');
      toast({ title: t('account.phone.verifiedToast'), variant: 'success' });
    } catch (err) {
      setError(describe(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('account.phone.title')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4">
        {error !== null && <p className="text-sm text-destructive" role="alert">{error}</p>}

        {phase === 'loading' && <p className="text-sm text-muted-foreground">{t('account.phone.loading')}</p>}

        {phase === 'error' && (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setReloadKey(k => k + 1)} />
        )}

        {phase === 'idle' && (
          <div className="flex max-w-md flex-col gap-3">
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('account.phone.field')}</span>
              <Input
                aria-label={t('account.phone.field')}
                inputMode="tel"
                placeholder={t('account.phone.placeholder')}
                value={phone}
                onChange={e => setPhone(e.target.value)}
                disabled={busy}
              />
            </label>
            <div>
              <Button disabled={busy || phone.trim().length === 0} onClick={() => void sendCode()}>
                {t('account.phone.sendCode')}
              </Button>
            </div>
          </div>
        )}

        {phase === 'code' && (
          <div className="flex max-w-md flex-col gap-3">
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('account.phone.codeField')}</span>
              <Input
                aria-label={t('account.phone.codeField')}
                inputMode="numeric"
                value={code}
                onChange={e => setCode(e.target.value)}
                disabled={busy}
              />
            </label>
            <div className="flex flex-wrap gap-3">
              <Button disabled={busy || code.trim().length === 0} onClick={() => void confirm()}>
                {t('account.phone.confirm')}
              </Button>
              <Button variant="outline" disabled={busy} onClick={() => void sendCode()}>
                {t('account.phone.resend')}
              </Button>
            </div>
          </div>
        )}

        {phase === 'verified' && (
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium">{currentPhone}</span>
            <Badge variant="default">{t('account.phone.verifiedBadge')}</Badge>
            <Button variant="outline" disabled={busy} onClick={() => { setPhone(''); setError(null); setPhase('idle'); }}>
              {t('account.phone.change')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
