import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonTable } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { GamesApi } from '@/api/platformClients/games';
import type { CatalogGameDto } from '@/api/types';
import { GameFormDialog } from './GameFormDialog';
import {
  describeGameError,
  describeLaunchKind,
  emptyGameForm,
  formFromGame,
  formatAgeMark,
  requestFromForm,
  type GameForm
} from './gamesModel';
import { useLoadable } from '../useLoadable';
import { loadImage } from '../mediaErrors';

type Client = Pick<GamesApi, 'listGames' | 'createGame' | 'updateGame' | 'steamCover' | 'uploadCover'>;

interface Draft {
  catalogGameId: string | null;
  form: GameForm;
}

export function GamesScreen({ client }: { client: Client }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const state = useLoadable(async () => {
    const loaded = await client.listGames();
    return Array.isArray(loaded) ? loaded : [];
  });
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  function open(next: Draft) {
    setSaveError(null);
    setDraft(next);
  }

  function close() {
    if (pending) return;
    setDraft(null);
    setSaveError(null);
  }

  async function save() {
    if (draft === null || pending) return;
    setPending(true);
    setSaveError(null);
    try {
      const request = requestFromForm(draft.form);
      if (draft.catalogGameId === null) {
        await client.createGame(request);
      } else {
        await client.updateGame(draft.catalogGameId, request);
      }
      toast({ title: t(draft.catalogGameId === null ? 'platform.games.created' : 'platform.games.saved'), variant: 'success' });
      setDraft(null);
      state.retry();
    } catch (cause) {
      // Форма остаётся открытой с тем, что человек ввёл: причина видна рядом с полями, которые
      // её вызвали, и исправить можно не набирая всё заново.
      setSaveError(describeGameError(cause, t));
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'error') {
    return <ErrorState title={t('platform.games.error.load')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;
  }
  if (state.status === 'loading') {
    return (
      <Loading>
        <SkeletonCard action>
          <p className="mgmt-drawer-hint">{t('platform.games.description')}</p>
          <SkeletonTable columns={6} />
        </SkeletonCard>
      </Loading>
    );
  }

  const games = state.data;
  const createNew = () => open({ catalogGameId: null, form: emptyGameForm() });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.games.title')}</CardTitle>
        <Button onClick={createNew}>{t('platform.games.create')}</Button>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.games.description')}</p>

        {games.length === 0 ? (
          <EmptyState message={t('platform.games.empty')} next={{ label: t('platform.games.createFirst'), onClick: createNew }} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.games.column.name')}</TableHead>
                <TableHead>{t('platform.games.column.genre')}</TableHead>
                <TableHead>{t('platform.games.column.age')}</TableHead>
                <TableHead>{t('platform.games.column.launch')}</TableHead>
                <TableHead>{t('platform.games.column.status')}</TableHead>
                <TableHead>{t('platform.games.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {games.map(game => (
                <TableRow key={game.catalogGameId}>
                  <TableCell>
                    <span className="pc-game-name">
                      <CoverThumb key={game.coverUrl ?? ''} url={game.coverUrl} />
                      {game.name}
                    </span>
                  </TableCell>
                  <TableCell>{game.genre ?? '—'}</TableCell>
                  <TableCell><span className="pc-num">{formatAgeMark(game.minAge) ?? '—'}</span></TableCell>
                  <TableCell><LaunchCell game={game} /></TableCell>
                  <TableCell>
                    <Badge variant={game.isPublished ? 'success' : 'outline'}>
                      {t(game.isPublished ? 'platform.games.status.published' : 'platform.games.status.draft')}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <span className="pc-cell-actions">
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={pending}
                        onClick={() => open({ catalogGameId: game.catalogGameId, form: formFromGame(game) })}
                      >
                        {t('platform.games.edit')}
                      </Button>
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {draft === null ? null : (
          <GameFormDialog
            key={draft.catalogGameId ?? 'new'}
            mode={draft.catalogGameId === null ? 'create' : 'edit'}
            form={draft.form}
            pending={pending}
            error={saveError}
            onChange={form => setDraft({ ...draft, form })}
            onSubmit={() => void save()}
            onClose={close}
            onSteamCover={appId => loadImage(() => client.steamCover(appId), t)}
            onUploadCover={file => loadImage(() => client.uploadCover(file), t)}
          />
        )}
      </CardContent>
    </Card>
  );
}

function LaunchCell({ game }: { game: CatalogGameDto }) {
  const { t } = useI18n();
  const kind = describeLaunchKind(game.launchKind, t);
  if (game.launchTarget === null) return <>{kind}</>;
  return <>{kind} · <span className="pc-mono">{game.launchTarget}</span></>;
}

// Миниатюра стоит и там, где обложки нет или она не открылась: пустая плашка держит названия
// в одну колонку, а не скачет по строкам.
function CoverThumb({ url }: { url: string | null }) {
  const [broken, setBroken] = useState(false);
  if (url === null || broken) return <span className="pc-game-thumb is-empty" aria-hidden="true" />;
  return <img className="pc-game-thumb" src={url} alt="" loading="lazy" onError={() => setBroken(true)} />;
}
