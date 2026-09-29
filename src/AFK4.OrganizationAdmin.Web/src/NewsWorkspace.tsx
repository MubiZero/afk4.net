import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Newspaper } from 'lucide-react';
import { MediaPurposeNames } from '@afk4/contracts';
import { MgmtTable } from './management/kit/MgmtTable';
import { MgmtDrawer } from './management/kit/MgmtDrawer';
import { ScreenAction } from './management/ManagementScreen';
import { Button } from '@afk4/ui/react';
import { CriticalActionConfirmation, EmptyState, PartialLoadFailure } from './operatorPrimitives';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { NewsItemDto, NewsItemInput, NewsScopeDto, OwnerBranchSummaryDto } from './operatorApiClients';
import { DeferredSkeleton, SkeletonTable } from './LoadingSkeleton';
import { MediaUpload } from './components/MediaUpload';

// Колонки списка — одни на таблицу и её заглушку.
const NEWS_GRID = '1.6fr 1fr 0.8fr 1.2fr';

interface NewsClient {
  list(): Promise<NewsItemDto[]>;
  /** Где сотрудник может публиковать: свои филиалы и «на всю сеть», если право во всех. */
  scope(): Promise<NewsScopeDto>;
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
  expiresAt: '',
  showOnPcs: false
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
  // Управляющий одного филиала на всю сеть не пишет: сервер откажет, и выбора «Все филиалы» у него нет.
  const [canPublishToAll, setCanPublishToAll] = useState(false);
  const [form, setForm] = useState({ ...EMPTY });
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [branchesError, setBranchesError] = useState<OperatorErrorProjection | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null); // id или '__new__' для создания
  const [deleteTarget, setDeleteTarget] = useState<NewsItemDto | null>(null);
  // Сохранение или удаление в пути: второй клик не создаст вторую новость. Ref — для кликов в одном
  // кадре, пока кнопка ещё не перерисовалась неактивной.
  const [saving, setSaving] = useState(false);
  const inFlight = useRef(false);
  const begin = () => {
    if (inFlight.current) return false;
    inFlight.current = true;
    setSaving(true);
    return true;
  };
  const finish = () => {
    inFlight.current = false;
    setSaving(false);
  };
  const isDrawerOpen = selectedId !== null;
  const isCreate = selectedId === '__new__';

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    // Филиалы нужны новостям только ради подписи «где показывается» и выбора в форме: их отказ
    // не должен прятать сами новости. Раньше отказ любого из двух запросов никто не ловил, и
    // экран так и оставался в загрузке.
    Promise.allSettled([client.list(), client.scope()]).then(([list, scope]) => {
      if (!active) return;
      if (list.status === 'fulfilled') setItems(list.value);
      else setListError(projectOperatorError(list.reason, t).detail);
      if (scope.status === 'fulfilled') applyScope(scope.value);
      else setBranchesError(projectOperatorError(scope.reason, t));
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

  const applyScope = (scope: NewsScopeDto) => {
    setBranches(scope.branches);
    setCanPublishToAll(scope.canPublishToAllBranches);
  };

  const retryBranches = () => {
    if (client === null) return;
    setBranchesError(null);
    client.scope()
      .then(applyScope)
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
      expiresAt: toLocalInput(item.expiresAtUtc),
      showOnPcs: item.showOnPcs ?? false
    });
    setSelectedId(item.id);
  };

  const openCreate = () => {
    // Кому «вся сеть» закрыта, у того новость сразу в его филиале, а не в запрещённом «Все».
    setForm({ ...EMPTY, branchId: canPublishToAll ? '' : branches[0]?.branchId ?? '' });
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
      expiresAtUtc,
      showOnPcs: form.showOnPcs
    };
    if (!begin()) return;
    try {
      if (form.id === null) {
        await client.create(request);
      } else {
        await client.update(form.id, request);
      }
    } catch (reason) {
      // Отказ сервера — в форме, где человек его увидит; введённое остаётся на месте.
      setError(projectOperatorError(reason, t).detail);
      finish();
      return;
    }
    setForm({ ...EMPTY });
    setSelectedId(null);
    finish();
    await reload().catch((reason) => setListError(projectOperatorError(reason, t).detail));
  };

  const remove = async (id: string) => {
    if (client === null || !begin()) return;
    try {
      await client.remove(id);
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
      finish();
      return;
    }
    setSelectedId(null);
    setForm({ ...EMPTY });
    finish();
    await reload().catch((reason) => setListError(projectOperatorError(reason, t).detail));
  };

  if (!ready) {
    return (
      <DeferredSkeleton>
        <div className="mgmt-master-detail">
          <SkeletonTable gridTemplate={NEWS_GRID} />
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
              <span className="mgmt-inline-tags">
                <span className={`ui-chip ui-chip--status ui-chip--xs ${n.isPublished ? 'is-live' : 'is-neutral'}`}>
                  {n.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag')}
                </span>
                {n.showOnPcs ? <span className="ui-chip ui-chip--xs is-neutral">{t('op.news.onPcsTag')}</span> : null}
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
        empty={{
          icon: <Newspaper size={22} aria-hidden="true" />,
          title: t('op.news.empty'),
          description: t('op.news.emptyDescription'),
          next: canManage ? { kind: 'formAbove' } : { kind: 'denied', hint: t('op.empty.denied.managerOrOwner') }
        }}
      />
      {/* Главная кнопка раздела — в шапке экрана; у пустого списка второй такой же нет. */}
      {canManage && (
        <ScreenAction>
          <Button variant="primary" onClick={openCreate}>{t('op.news.addCta')}</Button>
        </ScreenAction>
      )}

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
                    disabled={saving}
                    onClick={() => setDeleteTarget(items.find((n) => n.id === form.id) ?? null)}
                  >
                    {t('op.news.delete')}
                  </button>
                )}
                <button type="button" className="ui-btn ui-btn--primary" disabled={saving} onClick={() => void save()}>
                  {t('op.news.save')}
                </button>
              </div>
            ) : undefined
          }
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); if (canManage && !saving) void save(); }}>
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
                {canPublishToAll || form.branchId === '' ? <option value="">{t('op.news.allBranches')}</option> : null}
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
              {/* Загрузка кладёт картинку в медиа-хранилище и ставит сюда её адрес; без бэкенда (в
                  тестах экрана) остаётся поле адреса. */}
              {backend ? (
                <MediaUpload
                  value={form.imageUrl === '' ? null : form.imageUrl}
                  purpose={MediaPurposeNames.NewsImage}
                  branchId={form.branchId === '' ? backend.branchId : form.branchId}
                  backend={backend}
                  disabled={!canManage}
                  onChange={(media) => setForm((current) => ({ ...current, imageUrl: media?.url ?? '' }))}
                />
              ) : (
                <input value={form.imageUrl} disabled={!canManage} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} />
              )}
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
            <label className="mgmt-check">
              <input
                type="checkbox"
                checked={form.showOnPcs}
                disabled={!canManage}
                onChange={(event) => setForm({ ...form, showOnPcs: event.target.checked })}
              />
              {t('op.news.showOnPcs')}
            </label>
            <p className="mgmt-drawer-hint">{t('op.news.showOnPcsHint')}</p>
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
