import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext } from './operatorTypes';
import type { NewsItemDto, NewsItemInput, OwnerBranchSummaryDto } from './operatorApiClients';

interface NewsClient {
  list(): Promise<NewsItemDto[]>;
  listBranches(): Promise<OwnerBranchSummaryDto[]>;
  create(request: NewsItemInput): Promise<NewsItemDto>;
  update(id: string, request: NewsItemInput): Promise<NewsItemDto>;
  remove(id: string): Promise<void>;
}

const EMPTY = {
  id: null as string | null,
  branchId: '',
  title: '',
  body: '',
  imageUrl: '',
  isPublished: true,
  publishAt: '',
  expiresAt: ''
};

function toIsoOrNull(localValue: string): string | null {
  if (!localValue) return null;
  return new Date(localValue).toISOString();
}

function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function NewsWorkspace({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: NewsClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).news : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;

  const [items, setItems] = useState<NewsItemDto[]>([]);
  const [branches, setBranches] = useState<OwnerBranchSummaryDto[]>([]);
  const [form, setForm] = useState({ ...EMPTY });
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    Promise.all([client.list(), client.listBranches()]).then(([list, branchList]) => {
      if (!active) return;
      setItems(list);
      setBranches(branchList);
      setReady(true);
    });
    return () => { active = false; };
  }, [client]);

  const reload = async () => {
    if (client === null) return;
    setItems(await client.list());
  };

  const edit = (item: NewsItemDto) => {
    setError(null);
    setForm({
      id: item.id,
      branchId: item.branchId ?? '',
      title: item.title,
      body: item.body,
      imageUrl: item.imageUrl ?? '',
      isPublished: item.isPublished,
      publishAt: toLocalInput(item.publishAtUtc),
      expiresAt: toLocalInput(item.expiresAtUtc)
    });
  };

  const save = async () => {
    if (client === null) return;
    if (!form.title.trim() || !form.body.trim()) {
      setError(t('op.news.errorRequired'));
      return;
    }
    const publishAtUtc = toIsoOrNull(form.publishAt);
    const expiresAtUtc = toIsoOrNull(form.expiresAt);
    if (publishAtUtc !== null && expiresAtUtc !== null && publishAtUtc >= expiresAtUtc) {
      setError(t('op.news.errorWindow'));
      return;
    }
    setError(null);
    const request: NewsItemInput = {
      branchId: form.branchId === '' ? null : form.branchId,
      title: form.title.trim(),
      body: form.body.trim(),
      imageUrl: form.imageUrl.trim() === '' ? null : form.imageUrl.trim(),
      isPublished: form.isPublished,
      publishAtUtc,
      expiresAtUtc
    };
    if (form.id === null) {
      await client.create(request);
    } else {
      await client.update(form.id, request);
    }
    setForm({ ...EMPTY });
    await reload();
  };

  const remove = async (id: string) => {
    if (client === null) return;
    await client.remove(id);
    await reload();
  };

  if (!ready) {
    return (
      <main className="workspace-screen news-screen">
        <section className="screen-head"><h1>{t('op.news.title')}</h1></section>
        <p>…</p>
      </main>
    );
  }

  return (
    <main className="workspace-screen news-screen">
      <section className="screen-head"><h1>{t('op.news.title')}</h1></section>

      <form onSubmit={(event) => { event.preventDefault(); void save(); }}>
        <label>
          {t('op.news.fieldTitle')}
          <input value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldBody')}
          <textarea value={form.body} onChange={(event) => setForm({ ...form, body: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldImage')}
          <input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} />
        </label>
        <label>
          {t('op.news.fieldBranch')}
          <select value={form.branchId} onChange={(event) => setForm({ ...form, branchId: event.target.value })}>
            <option value="">{t('op.news.allBranches')}</option>
            {branches.map((branch) => (
              <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
            ))}
          </select>
        </label>
        <label>
          <input
            type="checkbox"
            checked={form.isPublished}
            onChange={(event) => setForm({ ...form, isPublished: event.target.checked })}
          />
          {t('op.news.published')}
        </label>
        <label>
          {t('op.news.publishAt')}
          <input type="datetime-local" value={form.publishAt} onChange={(event) => setForm({ ...form, publishAt: event.target.value })} />
        </label>
        <label>
          {t('op.news.expiresAt')}
          <input type="datetime-local" value={form.expiresAt} onChange={(event) => setForm({ ...form, expiresAt: event.target.value })} />
        </label>
        {error && <p role="alert">{error}</p>}
        <button type="submit">{t('op.news.save')}</button>
        {form.id !== null && (
          <button type="button" onClick={() => { setForm({ ...EMPTY }); setError(null); }}>{t('op.news.cancel')}</button>
        )}
      </form>

      {items.length === 0 && <p>{t('op.news.empty')}</p>}
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            <strong>{item.title}</strong>
            {!item.isPublished && <em> ({t('op.news.draftTag')})</em>}
            <button type="button" onClick={() => edit(item)}>{t('op.news.edit')}</button>
            <button type="button" onClick={() => void remove(item.id)}>{t('op.news.delete')}</button>
          </li>
        ))}
      </ul>
    </main>
  );
}
