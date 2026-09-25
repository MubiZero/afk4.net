import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Gamepad2, Trash2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../ManagementScreen';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import { CriticalActionConfirmation, EmptyState } from '../../../operatorPrimitives';
import { DeferredSkeleton, SkeletonTable } from '../../../LoadingSkeleton';
import { createAuthenticatedOperatorClients } from '../../../operatorHelpers';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import { projectOperatorError } from '../../../apiErrors';
import type { BranchGameDto, CatalogGameDto } from '../../../api/clients/games';
import type { DestinationProps } from '../types';
import {
  buildGameRequest,
  emptyGameForm,
  formFromCatalog,
  formFromGame,
  launchKindLabel,
  launchKinds,
  launchSummary,
  launchTargetLabel,
  moved,
  validateGame,
  type GameForm,
  type LaunchKind
} from './gamesModel';

const GAMES_GRID = '56px 1.4fr 1.4fr 140px';

type Drawer = { mode: 'closed' } | { mode: 'pick' } | { mode: 'form' };

/**
 * «Игры» (спека оболочки, §6.6): что игрок запустит на ПК филиала. Игру берут из каталога
 * платформы — название, обложка и возраст уже заполнены, — или заводят свою по пути к exe.
 * Список уходит агентам по версии в сердцебиении.
 */
export function GamesDestination({ backend, session, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canManage = hasPermission(session, permissionNames.manageGameLibrary);
  const client = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).games : null),
    [backend?.config, backend?.session]
  );
  const branchId = backend?.branchId ?? null;

  const [games, setGames] = useState<BranchGameDto[]>([]);
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [drawer, setDrawer] = useState<Drawer>({ mode: 'closed' });
  const [form, setForm] = useState<GameForm>(emptyGameForm);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [query, setQuery] = useState('');
  const [catalog, setCatalog] = useState<CatalogGameDto[] | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [removeTarget, setRemoveTarget] = useState<BranchGameDto | null>(null);

  useEffect(() => { onDirtyChange?.(false); }, [onDirtyChange]);

  const load = async () => {
    if (client === null || branchId === null) return;
    setStatus('loading');
    try {
      setGames(await client.list(branchId));
      setStatus('ready');
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
      setStatus('failed');
    }
  };

  useEffect(() => { void load(); }, [client, branchId]);

  // Каталог ищется по мере ввода, но не на каждую букву: четверть секунды тишины.
  useEffect(() => {
    if (drawer.mode !== 'pick' || client === null) return undefined;
    let active = true;
    const timer = setTimeout(() => {
      client.catalog(query)
        .then((list) => { if (active) { setCatalog(list); setCatalogError(null); } })
        .catch((error) => { if (active) setCatalogError(projectOperatorError(error, t).detail); });
    }, 250);
    return () => { active = false; clearTimeout(timer); };
  }, [client, drawer.mode, query]);

  const openGame = (game: BranchGameDto) => {
    setForm(formFromGame(game));
    setFormError(null);
    setDrawer({ mode: 'form' });
  };

  const openCustom = () => {
    setForm(emptyGameForm);
    setFormError(null);
    setDrawer({ mode: 'form' });
  };

  const close = () => {
    setDrawer({ mode: 'closed' });
    setForm(emptyGameForm);
    setFormError(null);
  };

  const save = async () => {
    if (client === null || branchId === null || backend === null) return;
    const problem = validateGame(form);
    if (problem !== null) {
      setFormError(t(problem));
      return;
    }

    setSaving(true);
    try {
      const request = buildGameRequest(backend.session.organizationId, form);
      if (form.id === null) {
        await client.add(branchId, request);
      } else {
        await client.update(branchId, form.id, request);
      }
      await load();
      close();
    } catch (error) {
      setFormError(projectOperatorError(error, t).detail);
    } finally {
      setSaving(false);
    }
  };

  const move = async (game: BranchGameDto, delta: -1 | 1) => {
    if (client === null || branchId === null || backend === null) return;
    const order = moved(games.map((item) => item.branchGameId), game.branchGameId, delta);
    if (order === null) return;
    try {
      setGames(await client.reorder(branchId, { organizationId: backend.session.organizationId, branchGameIds: order }));
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
    }
  };

  const remove = async (game: BranchGameDto) => {
    setRemoveTarget(null);
    if (client === null || branchId === null) return;
    try {
      await client.remove(branchId, game.branchGameId);
      await load();
      close();
    } catch (error) {
      setFormError(projectOperatorError(error, t).detail);
    }
  };

  const kindLabel = (kind: LaunchKind) => t(launchKindLabel[kind]);

  const content = () => {
    if (status === 'loading' && games.length === 0) {
      return (
        <DeferredSkeleton>
          <div className="mgmt-master-detail">
            <SkeletonTable gridTemplate={GAMES_GRID} toolbar={{ action: canManage }} />
          </div>
        </DeferredSkeleton>
      );
    }

    if (status === 'failed') {
      return (
        <EmptyState
          title={t('op.management.state.errorTitle')}
          description={loadError ?? undefined}
          next={{ kind: 'action', label: t('op.management.state.retry'), onClick: () => void load() }}
        />
      );
    }

    return (
      <div className="mgmt-master-detail">
        <MgmtTable<BranchGameDto>
          columns={[
            {
              key: 'cover',
              header: '',
              render: (game) => (game.coverUrl
                ? <img className="games-cover" src={game.coverUrl} alt="" loading="lazy" />
                : <span className="games-cover games-cover--empty" aria-hidden="true">{game.name.slice(0, 1)}</span>)
            },
            {
              key: 'name',
              header: t('op.games.col.name'),
              render: (game) => (
                <span className="games-name">
                  <strong>{game.name}</strong>
                  {game.minAge !== null && <span className="ui-chip ui-chip--xs">{t('op.games.age', { age: game.minAge })}</span>}
                </span>
              )
            },
            { key: 'launch', header: t('op.games.col.launch'), render: (game) => launchSummary(game, kindLabel) },
            {
              key: 'status',
              header: t('op.games.col.status'),
              render: (game) => (
                <span className={`ui-chip ui-chip--status ui-chip--xs ${game.isEnabled ? 'is-live' : 'is-neutral'}`}>
                  {t(!game.isEnabled ? 'op.games.status.off' : game.availableWithoutSession ? 'op.games.status.always' : 'op.games.status.on')}
                </span>
              )
            }
          ]}
          rows={games}
          rowKey={(game) => game.branchGameId}
          gridTemplate={GAMES_GRID}
          selectedKey={form.id}
          onSelectRow={openGame}
          rowActions={canManage ? (game) => [
            { id: 'up', label: t('op.games.moveUp'), icon: <ArrowUp size={14} aria-hidden="true" />, onSelect: () => void move(game, -1), disabled: games[0]?.branchGameId === game.branchGameId },
            { id: 'down', label: t('op.games.moveDown'), icon: <ArrowDown size={14} aria-hidden="true" />, onSelect: () => void move(game, 1), disabled: games.at(-1)?.branchGameId === game.branchGameId },
            { id: 'remove', label: t('op.games.remove'), icon: <Trash2 size={14} aria-hidden="true" />, onSelect: () => setRemoveTarget(game), danger: true }
          ] : undefined}
          toolbar={{
            title: t('op.management.dest.games'),
            secondary: canManage ? { label: t('op.games.addOwn'), onClick: openCustom } : undefined,
            primary: canManage ? { label: t('op.games.addFromCatalog'), onClick: () => { setQuery(''); setDrawer({ mode: 'pick' }); } } : undefined
          }}
          empty={{
            icon: <Gamepad2 size={22} aria-hidden="true" />,
            title: t('op.games.empty'),
            description: t('op.games.emptyDescription'),
            next: canManage
              ? { kind: 'action', label: t('op.games.addFromCatalog'), onClick: () => setDrawer({ mode: 'pick' }) }
              : { kind: 'denied', hint: t('op.empty.denied.managerOrOwner') }
          }}
        />

        {drawer.mode === 'pick' && (
          <MgmtDrawer title={t('op.games.addFromCatalog')} subtitle={t('op.games.catalogHint')} onClose={close}>
            <div className="mgmt-form">
              <label>
                {t('op.games.search')}
                <input value={query} autoFocus onChange={(event) => setQuery(event.target.value)} placeholder={t('op.games.searchPlaceholder')} />
              </label>
              {catalogError !== null && <p className="ui-inline-error" role="alert">{catalogError}</p>}
              {catalog !== null && catalog.length === 0 && (
                <EmptyState
                  inline
                  className="mgmt-drawer-hint"
                  title={t('op.games.catalogEmpty')}
                  next={{ kind: 'action', label: t('op.games.addOwn'), onClick: openCustom }}
                />
              )}
              {catalog !== null && catalog.length > 0 && (
                <ul className="games-catalog-list">
                  {catalog.map((game) => (
                    <li key={game.catalogGameId}>
                      <button
                        type="button"
                        className="games-catalog-item"
                        onClick={() => { setForm(formFromCatalog(game)); setFormError(null); setDrawer({ mode: 'form' }); }}
                      >
                        {game.coverUrl
                          ? <img className="games-cover" src={game.coverUrl} alt="" loading="lazy" />
                          : <span className="games-cover games-cover--empty" aria-hidden="true">{game.name.slice(0, 1)}</span>}
                        <span className="games-catalog-text">
                          <strong>{game.name}</strong>
                          <span>{[game.genre, game.minAge === null ? null : t('op.games.age', { age: game.minAge })].filter(Boolean).join(' · ')}</span>
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </MgmtDrawer>
        )}

        {drawer.mode === 'form' && (
          <MgmtDrawer
            title={form.id === null ? (form.catalogGameId ? form.name : t('op.games.addOwn')) : form.name}
            subtitle={form.catalogGameId ? t('op.games.fromCatalog') : t('op.games.ownGame')}
            onClose={close}
            footer={canManage ? (
              <div className="mgmt-form-actions">
                <button type="button" className="ui-btn ui-btn--primary" disabled={saving} onClick={() => void save()}>
                  {t('op.games.save')}
                </button>
              </div>
            ) : undefined}
          >
            <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); if (canManage) void save(); }}>
              {form.catalogCoverUrl && <img className="games-cover games-cover--large" src={form.catalogCoverUrl} alt="" />}
              <label>
                {t('op.games.field.name')}
                <input value={form.name} disabled={!canManage} onChange={(event) => setForm({ ...form, name: event.target.value })} />
              </label>
              <label>
                {t('op.games.field.kind')}
                <select
                  value={form.launchKind}
                  disabled={!canManage}
                  onChange={(event) => setForm({ ...form, launchKind: event.target.value as LaunchKind })}
                >
                  {launchKinds.map((kind) => <option key={kind} value={kind}>{kindLabel(kind)}</option>)}
                </select>
              </label>
              {form.launchKind !== 'exe' && (
                <label>
                  {t(launchTargetLabel[form.launchKind])}
                  <input value={form.launchTarget} disabled={!canManage} onChange={(event) => setForm({ ...form, launchTarget: event.target.value })} />
                </label>
              )}
              <label className="mgmt-form-wide">
                {t(form.launchKind === 'exe' ? 'op.games.field.path' : 'op.games.field.pathOverride')}
                <input
                  value={form.executablePath}
                  disabled={!canManage}
                  spellCheck={false}
                  placeholder="C:\Games\Game\game.exe"
                  onChange={(event) => setForm({ ...form, executablePath: event.target.value })}
                />
                <span className="payset-field-hint">{t(form.launchKind === 'exe' ? 'op.games.field.pathHint' : 'op.games.field.pathOverrideHint')}</span>
              </label>
              <label className="mgmt-form-wide">
                {t('op.games.field.arguments')}
                <input value={form.arguments} disabled={!canManage} spellCheck={false} onChange={(event) => setForm({ ...form, arguments: event.target.value })} />
              </label>
              {!form.catalogGameId && (
                <>
                  <label>
                    {t('op.games.field.genre')}
                    <input value={form.genre} disabled={!canManage} onChange={(event) => setForm({ ...form, genre: event.target.value })} />
                  </label>
                  <label>
                    {t('op.games.field.age')}
                    <select value={form.minAge} disabled={!canManage} onChange={(event) => setForm({ ...form, minAge: event.target.value })}>
                      <option value="">{t('op.games.age.none')}</option>
                      {[0, 6, 12, 16, 18].map((age) => <option key={age} value={String(age)}>{t('op.games.age', { age })}</option>)}
                    </select>
                  </label>
                </>
              )}
              <label className="mgmt-check mgmt-form-wide">
                <input type="checkbox" checked={form.isEnabled} disabled={!canManage} onChange={(event) => setForm({ ...form, isEnabled: event.target.checked })} />
                {t('op.games.field.enabled')}
              </label>
              <label className="mgmt-check mgmt-form-wide">
                <input
                  type="checkbox"
                  checked={form.availableWithoutSession}
                  disabled={!canManage}
                  onChange={(event) => setForm({ ...form, availableWithoutSession: event.target.checked })}
                />
                {t('op.games.field.withoutSession')}
              </label>
              <p className="payset-field-hint mgmt-form-wide">{t('op.games.field.withoutSessionHint')}</p>
              {formError && <p className="ui-inline-error mgmt-form-wide" role="alert">{formError}</p>}
            </form>
          </MgmtDrawer>
        )}

        {removeTarget && (
          <CriticalActionConfirmation
            title={t('op.games.removeTitle')}
            detail={removeTarget.name}
            impact={t('op.games.removeImpact')}
            confirmLabel={t('op.games.remove')}
            onCancel={() => setRemoveTarget(null)}
            onConfirm={() => void remove(removeTarget)}
          />
        )}
      </div>
    );
  };

  return (
    <ManagementScreen title={t('op.management.dest.games')} subtitle={t('op.management.dest.games.subtitle')} contentWidth="full">
      {content()}
    </ManagementScreen>
  );
}
