import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Newspaper } from 'lucide-react';
import { MgmtTable } from './management/kit/MgmtTable';
import { MgmtDrawer } from './management/kit/MgmtDrawer';
import { CriticalActionConfirmation } from './operatorPrimitives';
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
  const { t, formatDate } = useI18n();
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
  const [selectedId, setSelectedId] = useState<string | null>(null); // id или '__new__' для создания
  const [deleteTarget, setDeleteTarget] = useState<NewsItemDto | null>(null);
  const isDrawerOpen = selectedId !== null;
  const isCreate = selectedId === '__new__';

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
    setSelectedId(item.id);
  };

  const openCreate = () => {
    setForm({ ...EMPTY });
    setError(null);
    setSelectedId('__new__');
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
    setSelectedId(null);
  };

  const remove = async (id: string) => {
    if (client === null) return;
    await client.remove(id);
    await reload();
    setSelectedId(null);
  };

  if (!ready) {
    return <p className="workspace-loading">{t('state.loading')}</p>;
  }

  const branchName = (branchId: string | null) =>
    branchId === null ? t('op.news.allBranches') : (branches.find((b) => b.branchId === branchId)?.name ?? '—');

  const windowLabel = (item: NewsItemDto) => {
    const from = item.publishAtUtc ? formatDate(item.publishAtUtc) : '';
    const to = item.expiresAtUtc ? formatDate(item.expiresAtUtc) : '';
    if (!from && !to) return '—';
    return `${from || '…'} — ${to || '…'}`;
  };

  return (
    <div className="mgmt-master-detail">
      <MgmtTable<NewsItemDto>
        columns={[
          { key: 'title', header: t('op.news.fieldTitle'), render: (n) => n.title },
          { key: 'branch', header: t('op.news.col.branch'), render: (n) => branchName(n.branchId) },
          {
            key: 'status',
            header: t('op.news.col.status'),
            render: (n) => (
              <span className={`ui-chip ui-chip--status ui-chip--xs ${n.isPublished ? 'is-live' : 'is-neutral'}`}>
                {n.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag')}
              </span>
            )
          },
          { key: 'window', header: t('op.news.col.window'), render: (n) => windowLabel(n) }
        ]}
        rows={items}
        rowKey={(n) => n.id}
        gridTemplate="1.6fr 1fr 0.8fr 1.2fr"
        selectedKey={isCreate ? null : selectedId}
        onSelectRow={(n) => edit(n)}
        toolbar={{
          title: t('op.management.dest.news'),
          primary: { label: t('op.news.addCta'), onClick: openCreate }
        }}
        empty={{
          icon: <Newspaper size={22} aria-hidden="true" />,
          title: t('op.news.empty'),
          description: t('op.news.emptyDescription'),
          action: { label: t('op.news.addCta'), onClick: openCreate }
        }}
      />

      {isDrawerOpen && (
        <MgmtDrawer
          title={isCreate ? t('op.news.createTitle') : (form.title || t('op.news.createTitle'))}
          subtitle={isCreate ? undefined : (form.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag'))}
          onClose={() => { setSelectedId(null); setForm({ ...EMPTY }); setError(null); }}
          footer={
            <div className="mgmt-form-actions">
              {!isCreate && (
                <button
                  type="button"
                  className="ui-btn ui-btn--danger"
                  onClick={() => setDeleteTarget(items.find((n) => n.id === form.id) ?? null)}
                >
                  {t('op.news.delete')}
                </button>
              )}
              <button type="button" className="ui-btn ui-btn--primary" onClick={() => void save()}>
                {t('op.news.save')}
              </button>
            </div>
          }
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void save(); }}>
            <label>
              {t('op.news.fieldTitle')}
              <input value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} />
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
              {t('op.news.fieldBody')}
              <textarea value={form.body} onChange={(event) => setForm({ ...form, body: event.target.value })} rows={5} />
            </label>
            <label>
              {t('op.news.fieldImage')}
              <input value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} />
            </label>
            <label className="mgmt-check">
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
            {error && <p className="ui-inline-error" role="alert">{error}</p>}
          </form>
        </MgmtDrawer>
      )}

      {deleteTarget && (
        <CriticalActionConfirmation
          title={t('op.news.deleteTitle')}
          detail={deleteTarget.title}
          impact={t('op.news.deleteImpact')}
          confirmLabel={t('op.news.delete')}
          onCancel={() => setDeleteTarget(null)}
          onConfirm={() => { const id = deleteTarget.id; setDeleteTarget(null); void remove(id); }}
        />
      )}
    </div>
  );
}
