import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerTopUpIntentDto } from '@/api/types';
import { majorToMinor } from '@afk4/money';
import { formatMoney } from '@/lib/money';
import { useToast } from '@/components/ui/toast';
import { BranchPicker } from '@/branch/BranchPicker';
import type { BranchChoice } from '@/branch/branchChoice';

const DEFAULT_CURRENCY = 'TJS';
// Upper bound for a single request: rejects absurd / scientific-notation input (e.g. 1e308 → Infinity → JSON null).
const MAX_TOPUP_MAJOR = 1_000_000;

function intentStateKey(intent: PlayerTopUpIntentDto): MessageKey {
  if (intent.state === 'fulfilled') return 'customer.wallet.stateFulfilled';
  if (intent.isExpired) return 'customer.wallet.stateExpired';
  return 'customer.wallet.statePending';
}

export function WalletPanel({ api, phoneVerified, features = null, branch, onChooseBranch }: {
  api: PlayerApiClient;
  phoneVerified: boolean;
  features?: string[] | null;
  // Зал, в котором клуб откроет счёт: пополнение — такое же первое действие, как бронь.
  branch: BranchChoice;
  onChooseBranch: (branchId: string) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  // features === null means "not loaded yet / failed to load" — treat online_topup as enabled,
  // same reasoning as BottomNav: this hides UI for convenience, the server still gates the write.
  const topUpEnabled = features === null || features.includes('online_topup');
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
    if (branch.unanswered) {
      toast({ title: t('customer.branch.errRequired'), variant: 'error' });
      return;
    }
    setPending(true);
    try {
      await api.createTopUpIntent({
        amountMinorUnits: majorToMinor(major),
        currencyCode: DEFAULT_CURRENCY,
        branchId: branch.branchId
      });
      setAmount('');
      toast({ title: t('customer.wallet.sent'), variant: 'success' });
      refreshIntents();
    } catch (error: unknown) {
      const message = (error as { message?: string }).message;
      const title = message === 'branch_required' ? t('customer.branch.errRequired')
        : message === 'branch_not_found' ? t('customer.branch.errGone')
        : message === 'club_account_closed' ? t('customer.club.errClosed')
        : t('customer.wallet.sendError');
      toast({ title, variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="rounded-2xl bg-[var(--color-surface)] p-4">
      <h2 className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.wallet.title')}</h2>

      {topUpEnabled && (phoneVerified ? (
        <form className="mt-3 space-y-3" onSubmit={handleSubmit}>
          <BranchPicker choice={branch} onChoose={onChooseBranch} />
          <div className="flex gap-2">
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
          </div>
        </form>
      ) : (
        <p className="mt-3 rounded-xl border border-dashed border-[var(--color-border)] p-3 text-sm text-[var(--text-2)]">
          {t('customer.wallet.gate')}
        </p>
      ))}

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
