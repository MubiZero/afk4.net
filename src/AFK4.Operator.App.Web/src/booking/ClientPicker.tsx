import { useEffect, useId, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';
import { Search, UserCheck, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { PlayerClientItem } from '../operatorHelpers';
import { useDeferredFlag } from '../useDeferredFlag';

export interface ClientPick {
  playerAccountId: string;
  name: string;
  phoneNumber: string;
  balanceMinorUnits: number;
  debtMinorUnits: number;
}

/**
 * Поиск клиента клуба с привязкой аккаунта (playerAccountId) и режимом «гость».
 * Свободный ввод = гость; выбор из найденных = привязанный клиент (подтягивает телефон и аккаунт).
 * Результаты рендерятся блоком в потоке (как в POS-корзине), чтобы не обрезаться в скролле drawer.
 */
export function ClientPicker({
  value,
  linked,
  disabled = false,
  search,
  onQueryChange,
  onPick,
  onClear
}: {
  value: string;
  linked: boolean;
  disabled?: boolean;
  search: (query: string) => Promise<PlayerClientItem[]>;
  onQueryChange: (name: string) => void;
  onPick: (pick: ClientPick) => void;
  onClear: () => void;
}) {
  const { t } = useI18n();
  const [results, setResults] = useState<PlayerClientItem[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [settled, setSettled] = useState(false); // поиск для текущего запроса завершился
  const [activeIndex, setActiveIndex] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const baseId = useId();
  const query = value.trim();
  // «Поиск…» показываем с задержкой — быстрый ответ не успевает мигнуть индикатором.
  const searching = useDeferredFlag(loading, 160);

  // Дебаунс-поиск (только при открытом списке). Прежние результаты НЕ сбрасываем между нажатиями —
  // список обновляется на месте, без мигания «Поиск ↔ не найдено».
  useEffect(() => {
    if (!open || query.length < 2) {
      setResults([]);
      setSettled(false);
      setLoading(false);
      return undefined;
    }
    let cancelled = false;
    const handle = setTimeout(() => {
      setLoading(true);
      search(query)
        .then((hits) => {
          if (cancelled) return;
          setResults(hits.filter((hit) => hit.playerAccountId));
          setActiveIndex(0);
          setSettled(true);
        })
        .catch(() => {
          if (cancelled) return;
          setResults([]);
          setSettled(true);
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 280);
    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
  }, [open, query, search]);

  // Клик вне поля закрывает список.
  useEffect(() => {
    if (!open) return undefined;
    function onPointerDown(event: PointerEvent) {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  function pick(client: PlayerClientItem) {
    onPick({
      playerAccountId: client.playerAccountId ?? '',
      name: client.name,
      phoneNumber: client.phoneNumber,
      balanceMinorUnits: client.balanceMinorUnits,
      debtMinorUnits: client.debtMinorUnits
    });
    setOpen(false);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') {
      setOpen(false);
      return;
    }
    if (!open || results.length === 0) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((index) => Math.min(index + 1, results.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const client = results[activeIndex];
      if (client) pick(client);
    }
  }

  const listboxId = `${baseId}-results`;
  // Список рендерим только когда есть что показать: результаты, идёт поиск или поиск завершён пусто.
  const showResults = open && query.length >= 2 && (results.length > 0 || searching || settled);

  return (
    <div className="booking-client-picker" ref={rootRef}>
      <div className={`booking-client-field${linked ? ' is-linked' : ''}`}>
        {linked ? <UserCheck size={14} className="booking-client-lead" aria-hidden="true" /> : <Search size={14} className="booking-client-lead" aria-hidden="true" />}
        <input
          role="combobox"
          aria-expanded={showResults}
          aria-controls={showResults ? listboxId : undefined}
          aria-autocomplete="list"
          aria-label={t('clients.search.label')}
          value={value}
          disabled={disabled}
          placeholder={t('clients.search.placeholder')}
          onChange={(event) => { onQueryChange(event.currentTarget.value); setOpen(true); }}
          onFocus={() => { if (query.length >= 2) setOpen(true); }}
          onKeyDown={handleKeyDown}
        />
        {linked && (
          <button type="button" className="booking-client-clear" aria-label={t('op.booking.client.clear')} disabled={disabled} onClick={onClear}>
            <X size={13} aria-hidden="true" />
          </button>
        )}
      </div>
      {linked && <span className="booking-client-badge">{t('op.booking.client.linked')}</span>}
      {showResults && (
        <ul id={listboxId} className="booking-client-results" role="listbox" aria-label={t('clients.search.label')}>
          {results.length > 0 ? (
            results.map((client, index) => (
              <li
                key={client.playerAccountId ?? client.name}
                role="option"
                aria-selected={index === activeIndex}
                className={`booking-client-option${index === activeIndex ? ' is-active' : ''}`}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => pick(client)}
              >
                <strong>{client.name}</strong>
                <span>{client.phoneNumber || '—'}</span>
              </li>
            ))
          ) : (
            <li className="booking-client-msg">{searching ? t('op.pos.cart.clientSearching') : t('op.pos.cart.clientNotFound')}</li>
          )}
        </ul>
      )}
    </div>
  );
}
