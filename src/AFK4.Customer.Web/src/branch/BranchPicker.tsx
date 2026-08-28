import { useI18n } from '@afk4/i18n';
import type { BranchChoice } from './branchChoice';

/**
 * Выбор зала: название и адрес каждого — по ним игрок и узнаёт своё место.
 *
 * Ничего не рисует, когда выбирать не из чего: заголовок «В какой зал вы придёте?» над
 * единственным залом — вопрос без вопроса.
 */
export function BranchPicker({
  choice,
  onChoose
}: {
  choice: BranchChoice;
  onChoose: (branchId: string) => void;
}) {
  const { t } = useI18n();
  if (!choice.asks) return null;

  return (
    <fieldset className="space-y-2 rounded-2xl bg-[var(--color-surface)] p-4">
      <legend className="text-sm font-bold">{t('customer.branch.title')}</legend>
      <p className="text-sm text-[var(--text-2)]">{t('customer.branch.hint')}</p>
      {choice.halls.map((hall) => {
        const selected = hall.branchId === choice.chosenId;
        const address = [hall.city, hall.address ?? ''].filter((part) => part.length > 0).join(', ');
        return (
          <label
            key={hall.branchId}
            className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-xl border px-3 py-2 ${
              selected ? 'border-[var(--accent)]' : 'border-[var(--color-border)]'
            }`}
          >
            <input
              type="radio"
              name="branch"
              value={hall.branchId}
              checked={selected}
              onChange={() => onChoose(hall.branchId)}
              className="size-4 accent-[var(--accent)]"
            />
            <span className="min-w-0">
              <span className="block truncate text-sm font-bold">{hall.name || hall.city}</span>
              {address.length > 0 && (
                <span className="block truncate text-sm text-[var(--text-2)]">{address}</span>
              )}
            </span>
          </label>
        );
      })}
    </fieldset>
  );
}
