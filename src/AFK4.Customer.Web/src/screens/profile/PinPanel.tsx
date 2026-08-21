import { useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import { useToast } from '@/components/ui/toast';

// Та же форма, что проверяет сервер (`PinFormat` в `AFK4.Shared.Contracts`). Дублируется здесь
// намеренно: показать отказ на месте дешевле, чем сходить за ним по сети. Разъехаться этим двум
// правилам не даёт тест, который читает сам контракт.
export const PIN_LENGTH_BOUNDS: readonly [number, number] = [4, 8];
const PIN_PATTERN = new RegExp(`^\\d{${PIN_LENGTH_BOUNDS[0]},${PIN_LENGTH_BOUNDS[1]}}$`);

interface PinPanelProps {
  api: PlayerApiClient;
  pinSet: boolean;
  onPinSet: () => void;
}

/**
 * PIN — это не пароль от приложения, а четыре-восемь цифр, которыми человек садится за ПК сам.
 * Задаётся и меняется он только здесь: клуб сетевой PIN не назначает, иначе администратор одного
 * клуба получил бы вход от чужого имени в соседних.
 */
export function PinPanel({ api, pinSet, onPinSet }: PinPanelProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [editing, setEditing] = useState(false);
  const [pin, setPin] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = pin.trim();
    if (!PIN_PATTERN.test(trimmed)) {
      setError(t('customer.pin.invalid'));
      return;
    }
    setError(null);
    setPending(true);
    try {
      await api.setPin(trimmed);
      setPin('');
      setEditing(false);
      toast({ title: t('customer.pin.saved'), variant: 'success' });
      onPinSet();
    } catch {
      toast({ title: t('customer.pin.saveError'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4">
      <div>
        <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.pin.title')}</p>
        <p className="mt-1 text-sm text-[var(--text-2)]">{t('customer.pin.note')}</p>
        <p className="mt-1 text-sm text-[var(--text-3)]">{t('customer.pin.networkNote')}</p>
      </div>

      <p className="text-sm font-bold">{t(pinSet ? 'customer.pin.isSet' : 'customer.pin.notSet')}</p>

      {editing ? (
        <form className="space-y-3" onSubmit={handleSubmit}>
          <div className="space-y-1.5">
            <label htmlFor="new-pin" className="text-sm text-[var(--text-2)]">{t('customer.pin.field')}</label>
            <input
              id="new-pin"
              type="password"
              inputMode="numeric"
              autoComplete="off"
              value={pin}
              onChange={(event) => setPin(event.target.value)}
              className="h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
            />
            <p className="text-sm text-[var(--text-3)]">{t('customer.pin.hint')}</p>
            <p className="text-sm text-[var(--text-3)]">{t('customer.pin.forgotNote')}</p>
          </div>

          {error && <p role="alert" className="text-sm text-red-400">{error}</p>}

          <div className="flex gap-2">
            <button
              type="submit"
              disabled={pending}
              className="h-11 flex-1 rounded-xl bg-[var(--accent)] text-sm font-bold text-[var(--accent-fg)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
            >
              {pending ? t('customer.pin.saving') : t('customer.pin.save')}
            </button>
            <button
              type="button"
              onClick={() => { setEditing(false); setPin(''); setError(null); }}
              className="h-11 flex-1 rounded-xl border border-[var(--color-border)] text-sm text-[var(--text-2)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
            >
              {t('customer.pin.cancel')}
            </button>
          </div>
        </form>
      ) : (
        <button
          type="button"
          onClick={() => setEditing(true)}
          className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm font-medium focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
        >
          {t(pinSet ? 'customer.pin.change' : 'customer.pin.set')}
        </button>
      )}
    </section>
  );
}
