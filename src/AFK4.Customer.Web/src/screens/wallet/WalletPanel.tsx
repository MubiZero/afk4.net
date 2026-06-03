import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerTopUpIntentDto } from '@/api/types';
import { majorToMinor } from '@afk4/money';
import { formatMoney } from '@/lib/money';
import { useToast } from '@/components/ui/toast';

const DEFAULT_CURRENCY = 'TJS';
// Upper bound for a single request: rejects absurd / scientific-notation input (e.g. 1e308 → Infinity → JSON null).
const MAX_TOPUP_MAJOR = 1_000_000;

function intentStateKey(intent: PlayerTopUpIntentDto): MessageKey {
  if (intent.state === 'fulfilled') return 'customer.wallet.stateFulfilled';
  if (intent.isExpired) return 'customer.wallet.stateExpired';
  return 'customer.wallet.statePending';
}

export function WalletPanel({ api, phoneVerified }: { api: PlayerApiClient; phoneVerified: boolean }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [intents, setIntents] = useState<PlayerTopUpIntentDto[]>([]);
  const [amount, setAmount] = useState('');
  const [pending, setPending] = useState(false);

  // Guard against setState after unmount (navigation away mid-fetch).
  const mountedRef = useRef(true);
  useEffect(() => () => { mountedRef.current = false; }, []);

  const refreshIntents = useCallback(() => {
    api.getTopUpIntents().then((list) => { if (mountedRef.current) setIntents(list); }).catch(() => { /* list is best-effort */ });
  }, [api]);

  useEffect(() => { refreshIntents(); }, [refreshIntents]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const major = Number(amount.trim().replace(',', '.'));
    if (!Number.isFinite(major) || major <= 0 || major > MAX_TOPUP_MAJOR) {
      toast({ title: t('customer.wallet.amountError'), variant: 'error' });
      return;
    }
    setPending(true);
    try {
      await api.createTopUpIntent({ amountMinorUnits: majorToMinor(major), currencyCode: DEFAULT_CURRENCY });
      setAmount('');
      toast({ title: t('customer.wallet.sent'), variant: 'success' });
      refreshIntents();
    } catch {
      toast({ title: t('customer.wallet.sendError'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="rounded-2xl bg-[var(--color-surface)] p-4">
      <h2 className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.wallet.title')}</h2>

      {phoneVerified ? (
        <form className="mt-3 flex gap-2" onSubmit={handleSubmit}>
          <label htmlFor="topup-amount" className="sr-only">{t('customer.wallet.amount')}</label>
          <input
            id="topup-amount"
            type="text"
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0,00"
            className="h-11 flex-1 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          />
          <button
            type="submit"
            disabled={pending}
            className="h-11 rounded-xl bg-[var(--accent)] px-4 text-sm font-bold text-[var(--accent-fg)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          >
            {pending ? t('customer.wallet.requesting') : t('customer.wallet.request')}
          </button>
        </form>
      ) : (
        <p className="mt-3 rounded-xl border border-dashed border-[var(--color-border)] p-3 text-sm text-[var(--text-2)]">
          {t('customer.wallet.gate')}
        </p>
      )}

      {intents.length > 0 && (
        <ul className="mt-4 space-y-2">
          {intents.map((intent) => (
            <li key={intent.paymentIntentId} className="flex items-center justify-between text-sm">
              <span>{formatMoney(intent.amountMinorUnits, intent.currencyCode)}</span>
              <span className={intent.state === 'fulfilled' ? 'text-[var(--accent)]' : 'text-[var(--text-2)]'}>
                {t(intentStateKey(intent))}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
