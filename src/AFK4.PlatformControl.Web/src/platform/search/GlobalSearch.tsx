import { useEffect, useId, useRef, useState, type MouseEvent } from 'react';
import { Search } from 'lucide-react';
import type { SearchApi, PlatformSearchResult } from '@/api/platformClients/search';
import { Button } from '@/components/ui/button';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import { nextSearchIndex, SEARCH_KIND_LABEL } from './searchModel';

export function GlobalSearch({ client, onNavigate }: {
  client: Pick<SearchApi, 'search'>;
  onNavigate: (href: string) => void;
}) {
  const { t, formatNumber } = useI18n();
  const listboxId = useId();
  const requestId = useRef(0);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<PlatformSearchResult[]>([]);
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle');
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [activeIndex, setActiveIndex] = useState(-1);
  const open = query.trim().length >= 2 && status !== 'idle';

  useEffect(() => {
    const normalized = query.trim();
    if (normalized.length < 2) { setResults([]); setStatus('idle'); setActiveIndex(-1); return; }
    const controller = new AbortController();
    const currentRequest = ++requestId.current;
    const timer = window.setTimeout(() => {
      setStatus('loading');
      client.search(normalized, controller.signal).then(value => {
        if (currentRequest !== requestId.current) return;
        setResults(value); setStatus('ready'); setActiveIndex(-1);
      }).catch((cause: unknown) => {
        if (controller.signal.aborted || currentRequest !== requestId.current) return;
        setResults([]); setError(describeApiError(cause, t)); setStatus('error'); setActiveIndex(-1);
      });
    }, 180);
    return () => { window.clearTimeout(timer); controller.abort(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [client, query, attempt]);

  function choose(result: PlatformSearchResult) {
    setQuery(''); setStatus('idle'); onNavigate(result.href);
  }

  function clickResult(event: MouseEvent<HTMLAnchorElement>, result: PlatformSearchResult) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault(); choose(result);
  }

  return (
    <div className="pc-search">
      <div className="ui-search-field">
        <Search size={15} aria-hidden="true" />
        <input
          type="search"
          role="combobox"
          aria-label={t('platform.search.label')}
          aria-expanded={open && status !== 'error'}
          aria-controls={listboxId}
          aria-activedescendant={activeIndex >= 0 ? `${listboxId}-${activeIndex}` : undefined}
          placeholder={t('platform.search.placeholder')}
          value={query}
          onChange={event => setQuery(event.target.value)}
          onKeyDown={event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
              event.preventDefault();
              setActiveIndex(index => nextSearchIndex(index, event.key === 'ArrowDown' ? 1 : -1, results.length));
            } else if (event.key === 'Enter' && activeIndex >= 0) {
              event.preventDefault();
              choose(results[activeIndex]);
            } else if (event.key === 'Escape') {
              setQuery(''); setResults([]); setStatus('idle');
            }
          }}
        />
      </div>

      <span className="sr-only" aria-live="polite">
        {status === 'ready' ? t('platform.search.resultCount', { count: formatNumber(results.length) }) : ''}
      </span>

      {/* Поиск, который сорвался, раньше показывал одну фразу без выхода: повторить тот же запрос
          было нечем — приходилось стирать строку и набирать её заново. Отдельная ветка, а не пункт
          в списке результатов: кнопке не место среди option, читалка её там не назовёт. */}
      {open && status === 'error' ? (
        <div role="status" className="pc-search-results">
          <p className="pc-search-empty">{error ?? t('platform.search.error')}</p>
          <Button variant="outline" size="sm" onClick={() => setAttempt(value => value + 1)}>{t('state.retry')}</Button>
        </div>
      ) : open ? (
        <div id={listboxId} role="listbox" className="pc-search-results">
          {status === 'loading' ? <p className="pc-search-empty">{t('platform.search.loading')}</p>
            : results.length === 0 ? <p className="pc-search-empty">{t('platform.search.empty')}</p>
            : results.map((result, index) => (
              <a
                id={`${listboxId}-${index}`}
                key={`${result.kind}-${result.id}`}
                role="option"
                aria-selected={activeIndex === index}
                data-href={result.href}
                href={result.href}
                className="pc-search-option"
                onMouseDown={() => setActiveIndex(index)}
                onClick={event => clickResult(event, result)}
              >
                <strong>{result.title}</strong>
                <span>{t(SEARCH_KIND_LABEL[result.kind])} · {result.context}</span>
              </a>
            ))}
        </div>
      ) : null}
    </div>
  );
}
