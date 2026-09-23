import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Newspaper } from 'lucide-react';
import { MgmtTable } from './management/kit/MgmtTable';
import { MgmtDrawer } from './management/kit/MgmtDrawer';
import { CriticalActionConfirmation, EmptyState, PartialLoadFailure } from './operatorPrimitives';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { NewsItemDto, NewsItemInput, OwnerBranchSummaryDto } from './operatorApiClients';
import { DeferredSkeleton, SkeletonTable } from './LoadingSkeleton';

// Колонки списка — одни на таблицу и её заглушку.
const NEWS_GRID = '1.6fr 1fr 0.8fr 1.2fr';

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
  canManage = true,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  // Client-side write gate — see NewsDestination.tsx for why this exists (support sessions can see
  // this screen's data without being able to write it). Defaults to true so every other caller
  // (currently just NewsDestination) keeps today's behaviour without having to think about it.
  canManage?: boolean;
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
  const [listError, setListError] = useState<string | null>(null);
  const [branchesError, setBranchesError] = useState<OperatorErrorProjection | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null); // id или '__new__' для создания
  const [deleteTarget, setDeleteTarget] = useState<NewsItemDto | null>(null);
  const isDrawerOpen = selectedId !== null;
  const isCreate = selectedId === '__new__';

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    // Филиалы нужны новостям только ради подписи «где показывается» и выбора в форме: их отказ
    // не должен прятать сами новости. Раньше отказ любого из двух запросов никто не ловил, и
    // экран так и оставался в загрузке.
    Promise.allSettled([client.list(), client.listBranches()]).then(([list, branchList]) => {
      if (!active) return;
      if (list.status === 'fulfilled') setItems(list.value);
      else setListError(projectOperatorError(list.reason, t).detail);
      if (branchList.status === 'fulfilled') setBranches(branchList.value);
      else setBranchesError(projectOperatorError(branchList.reason, t));
      setReady(true);
    });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [client]);

  const retryList = () => {
    if (client === null) return;
    setListError(null);
    client.list()
      .then(setItems)
      .catch((reason) => setListError(projectOperatorError(reason, t).detail));
  };

  const retryBranches = () => {
    if (client === null) return;
    setBranchesError(null);
    client.listBranches()
      .then(setBranches)
      .catch((reason) => setBranchesError(projectOperatorError(reason, t)));
  };

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
    return (
      <DeferredSkeleton>
        <div className="mgmt-master-detail">
          <SkeletonTable gridTemplate={NEWS_GRID} toolbar={{ action: canManage }} />
        </div>
      </DeferredSkeleton>
    );
  }

  if (listError !== null) {
    return (
      <EmptyState
        title={t('op.management.state.errorTitle')}
        description={listError}
        next={{ kind: 'action', label: t('op.management.state.retry'), onClick: retryList }}
      />
    );
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
      {branchesError !== null && (
        <PartialLoadFailure text={t('op.news.branchesFailed', { reason: branchesError.detail })} failure={branchesError} onRetry={retryBranches} />
      )}
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
        gridTemplate={NEWS_GRID}
        selectedKey={isCreate ? null : selectedId}
        onSelectRow={(n) => edit(n)}
        toolbar={{
          title: t('op.management.dest.news'),
          primary: canManage ? { label: t('op.news.addCta'), onClick: openCreate } : undefined
        }}
        empty={{
          icon: <Newspaper size={22} aria-hidden="true" />,
          title: t('op.news.empty'),
          description: t('op.news.emptyDescription'),
          next: canManage
            ? { kind: 'action', label: t('op.news.addCta'), onClick: openCreate }
            : { kind: 'denied', hint: t('op.empty.denied.managerOrOwner') }
        }}
      />

      {isDrawerOpen && (
        <MgmtDrawer
          title={isCreate ? t('op.news.createTitle') : (form.title || t('op.news.createTitle'))}
          subtitle={isCreate ? undefined : (form.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag'))}
          onClose={() => { setSelectedId(null); setForm({ ...EMPTY }); setError(null); }}
          footer={
            canManage ? (
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
            ) : undefined
          }
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); if (canManage) void save(); }}>
            <label>
              {t('op.news.fieldTitle')}
              <input value={form.title} disabled={!canManage} onChange={(event) => setForm({ ...form, title: event.target.value })} />
            </label>
            <label>
              {t('op.news.fieldBranch')}
              {/* Без списка филиалов выбирать не из чего, а показать «Все филиалы» у новости одного
                  филиала — значит соврать и тихо перенаправить её при сохранении. Поле замирает
                  на том, что есть, причина — в строке над таблицей. */}
              <select value={form.branchId} disabled={!canManage || branchesError !== null} onChange={(event) => setForm({ ...form, branchId: event.target.value })}>
                <option value="">{t('op.news.allBranches')}</option>
                {branchesError !== null && form.branchId !== '' && <option value={form.branchId}>—</option>}
                {branches.map((branch) => (
                  <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
                ))}
              </select>
            </label>
            <label>
              {t('op.news.fieldBody')}
              <textarea value={form.body} disabled={!canManage} onChange={(event) => setForm({ ...form, body: event.target.value })} rows={5} />
            </label>
            <label>
              {t('op.news.fieldImage')}
              <input value={form.imageUrl} disabled={!canManage} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} />
            </label>
            <label className="mgmt-check">
              <input
                type="checkbox"
                checked={form.isPublished}
                disabled={!canManage}
                onChange={(event) => setForm({ ...form, isPublished: event.target.checked })}
              />
              {t('op.news.published')}
            </label>
            <label>
              {t('op.news.publishAt')}
              <input type="datetime-local" value={form.publishAt} disabled={!canManage} onChange={(event) => setForm({ ...form, publishAt: event.target.value })} />
            </label>
            <label>
              {t('op.news.expiresAt')}
              <input type="datetime-local" value={form.expiresAt} disabled={!canManage} onChange={(event) => setForm({ ...form, expiresAt: event.target.value })} />
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
