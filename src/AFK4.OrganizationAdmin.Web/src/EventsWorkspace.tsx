import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Trophy } from 'lucide-react';
import { MgmtTable } from './management/kit/MgmtTable';
import { MgmtDrawer } from './management/kit/MgmtDrawer';
import { CriticalActionConfirmation } from './operatorPrimitives';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { projectOperatorError } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type {
  CreateTournamentRequest,
  TournamentDto,
  TournamentParticipantDto,
  UpdateTournamentRequest
} from './operatorApiClients';

interface TournamentClient {
  list(branchId: string): Promise<TournamentDto[]>;
  create(request: CreateTournamentRequest): Promise<TournamentDto>;
  update(tournamentId: string, request: UpdateTournamentRequest): Promise<TournamentDto>;
  publish(tournamentId: string): Promise<TournamentDto>;
  cancel(tournamentId: string, reason: string): Promise<TournamentDto>;
  participants(tournamentId: string): Promise<TournamentParticipantDto[]>;
}

const EMPTY = {
  id: null as string | null,
  title: '',
  description: '',
  discipline: '',
  startsAt: '',
  entryFee: '0',
  capacity: '0'
};

function toLocalInput(iso: string): string {
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/// Взнос вводят в сомони, а хранится он в дирамах: клуб не должен считать нули за нас.
function toMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (normalized === '') return 0;
  const parsed = Number(normalized);
  if (!Number.isFinite(parsed) || parsed < 0) return null;
  return Math.round(parsed * 100);
}

function fromMinorUnits(minorUnits: number): string {
  return (minorUnits / 100).toFixed(2);
}

/// События клуба: турнир по пятницам, ночь игры, чемпионат зала.
///
/// Событие сначала черновик, потом публикуется. Разделение не формальность: пока клуб дописывает
/// условия, игроки его не видят, а после публикации на него уже записываются — и правки задевают
/// людей, которые заплатили взнос.
export function EventsWorkspace({
  backend,
  canManage = true,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  canManage?: boolean;
  client?: TournamentClient;
}) {
  const { t, formatDate } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).tournaments : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;
  const branchId = backend?.branchId ?? '';

  const [items, setItems] = useState<TournamentDto[]>([]);
  const [participants, setParticipants] = useState<TournamentParticipantDto[]>([]);
  const [form, setForm] = useState({ ...EMPTY });
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null); // id или '__new__' для создания
  const [cancelTarget, setCancelTarget] = useState<TournamentDto | null>(null);
  const isDrawerOpen = selectedId !== null;
  const isCreate = selectedId === '__new__';
  const selected = isCreate ? null : items.find((item) => item.tournamentId === selectedId) ?? null;

  useEffect(() => {
    if (client === null || branchId === '') return undefined;
    let active = true;
    client.list(branchId).then((list) => {
      if (!active) return;
      setItems(list);
      setReady(true);
    });
    return () => { active = false; };
  }, [client, branchId]);

  const reload = async () => {
    if (client === null || branchId === '') return;
    setItems(await client.list(branchId));
  };

  // Список записавшихся спрашивается только при открытии события: по нему встречают на входе,
  // и держать его для всей таблицы значит платить запросом за строку.
  useEffect(() => {
    if (client === null || selected === null) {
      setParticipants([]);
      return undefined;
    }
    let active = true;
    client.participants(selected.tournamentId).then((list) => {
      if (active) setParticipants(list);
    }).catch(() => {
      if (active) setParticipants([]);
    });
    return () => { active = false; };
  }, [client, selected?.tournamentId]);

  const edit = (item: TournamentDto) => {
    setError(null);
    setForm({
      id: item.tournamentId,
      title: item.title,
      description: item.description,
      discipline: item.discipline,
      startsAt: toLocalInput(item.startsAtUtc),
      entryFee: fromMinorUnits(item.entryFee.minorUnits),
      capacity: String(item.capacity)
    });
    setSelectedId(item.tournamentId);
  };

  const openCreate = () => {
    setForm({ ...EMPTY });
    setError(null);
    setSelectedId('__new__');
  };

  const save = async () => {
    if (client === null) return;
    if (!form.title.trim()) {
      setError(t('op.events.errorTitleRequired'));
      return;
    }
    if (!form.startsAt) {
      setError(t('op.events.errorStartRequired'));
      return;
    }
    const entryFeeMinorUnits = toMinorUnits(form.entryFee);
    if (entryFeeMinorUnits === null) {
      setError(t('op.events.errorFee'));
      return;
    }
    const capacity = Number(form.capacity.trim() === '' ? '0' : form.capacity);
    if (!Number.isInteger(capacity) || capacity < 0) {
      setError(t('op.events.errorCapacity'));
      return;
    }

    setError(null);
    const startsAtUtc = new Date(form.startsAt).toISOString();
    try {
      if (form.id === null) {
        await client.create({
          branchId,
          title: form.title.trim(),
          description: form.description.trim(),
          discipline: form.discipline.trim(),
          startsAtUtc,
          entryFeeMinorUnits,
          capacity
        });
      } else {
        await client.update(form.id, {
          title: form.title.trim(),
          description: form.description.trim(),
          discipline: form.discipline.trim(),
          startsAtUtc,
          entryFeeMinorUnits,
          capacity
        });
      }
      setForm({ ...EMPTY });
      await reload();
      setSelectedId(null);
    } catch (failure) {
      // Отказ несёт причину — «потолок ниже, чем уже записалось» звучит иначе, чем «не вышло».
      setError(projectOperatorError(failure, t).detail ?? t('op.events.errorGeneric'));
    }
  };

  const publish = async (tournamentId: string) => {
    if (client === null) return;
    try {
      await client.publish(tournamentId);
      await reload();
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail ?? t('op.events.errorGeneric'));
    }
  };

  const cancel = async (tournamentId: string, reason: string) => {
    if (client === null) return;
    try {
      await client.cancel(tournamentId, reason);
      await reload();
      setSelectedId(null);
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail ?? t('op.events.errorGeneric'));
    }
  };

  if (!ready) {
    return <p className="workspace-loading">{t('state.loading')}</p>;
  }

  const stateChip = (item: TournamentDto) => {
    const label = item.state === 'published'
      ? t('op.events.statePublished')
      : item.state === 'cancelled'
        ? t('op.events.stateCancelled')
        : t('op.events.stateDraft');
    const tone = item.state === 'published' ? 'is-live' : item.state === 'cancelled' ? 'is-danger' : 'is-neutral';
    return <span className={`ui-chip ui-chip--status ui-chip--xs ${tone}`}>{label}</span>;
  };

  // «3 из 10» отвечает на вопрос стойки — заполняется ли вечер. У события без потолка потолка
  // нет и в подписи: выдуманное число хуже, чем его отсутствие.
  const seatsLabel = (item: TournamentDto) =>
    item.capacity === 0
      ? String(item.registeredCount)
      : t('op.events.registeredOf', { registered: item.registeredCount, capacity: item.capacity });

  return (
    <div className="mgmt-master-detail">
      <MgmtTable<TournamentDto>
        columns={[
          { key: 'title', header: t('op.events.fieldTitle'), render: (item) => item.title },
          { key: 'startsAt', header: t('op.events.col.starts'), render: (item) => formatDate(item.startsAtUtc) },
          { key: 'state', header: t('op.events.col.state'), render: (item) => stateChip(item) },
          { key: 'registered', header: t('op.events.col.registered'), render: (item) => seatsLabel(item) }
        ]}
        rows={items}
        rowKey={(item) => item.tournamentId}
        gridTemplate="1.6fr 1.2fr 0.8fr 0.8fr"
        selectedKey={isCreate ? null : selectedId}
        onSelectRow={(item) => edit(item)}
        toolbar={{
          title: t('op.management.dest.events'),
          primary: canManage ? { label: t('op.events.addCta'), onClick: openCreate } : undefined
        }}
        empty={{
          icon: <Trophy size={22} aria-hidden="true" />,
          title: t('op.events.empty'),
          description: t('op.events.emptyDescription'),
          action: canManage ? { label: t('op.events.addCta'), onClick: openCreate } : undefined
        }}
      />

      {isDrawerOpen && (
        <MgmtDrawer
          title={isCreate ? t('op.events.createTitle') : (form.title || t('op.events.createTitle'))}
          subtitle={selected === null ? undefined : (
            selected.state === 'published'
              ? t('op.events.statePublished')
              : selected.state === 'cancelled'
                ? t('op.events.stateCancelled')
                : t('op.events.stateDraft')
          )}
          onClose={() => { setSelectedId(null); setForm({ ...EMPTY }); setError(null); }}
          footer={
            canManage && selected?.state !== 'cancelled' ? (
              <div className="mgmt-form-actions">
                {selected !== null && selected.state !== 'draft' && (
                  <button
                    type="button"
                    className="ui-btn ui-btn--danger"
                    onClick={() => setCancelTarget(selected)}
                  >
                    {t('op.events.cancelCta')}
                  </button>
                )}
                {selected !== null && selected.state === 'draft' && (
                  <button
                    type="button"
                    className="ui-btn"
                    onClick={() => void publish(selected.tournamentId)}
                  >
                    {t('op.events.publishCta')}
                  </button>
                )}
                <button type="button" className="ui-btn ui-btn--primary" onClick={() => void save()}>
                  {t('op.events.save')}
                </button>
              </div>
            ) : undefined
          }
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); if (canManage) void save(); }}>
            <label>
              {t('op.events.fieldTitle')}
              <input
                value={form.title}
                disabled={!canManage}
                onChange={(event) => setForm({ ...form, title: event.target.value })}
              />
            </label>
            <label>
              {t('op.events.fieldDiscipline')}
              <input
                value={form.discipline}
                disabled={!canManage}
                placeholder={t('op.events.fieldDisciplineHint')}
                onChange={(event) => setForm({ ...form, discipline: event.target.value })}
              />
            </label>
            <label>
              {t('op.events.fieldStarts')}
              <input
                type="datetime-local"
                value={form.startsAt}
                disabled={!canManage}
                onChange={(event) => setForm({ ...form, startsAt: event.target.value })}
              />
            </label>
            <label>
              {t('op.events.fieldDescription')}
              <textarea
                value={form.description}
                disabled={!canManage}
                rows={4}
                onChange={(event) => setForm({ ...form, description: event.target.value })}
              />
            </label>
            <label>
              {t('op.events.fieldFee')}
              <input
                value={form.entryFee}
                disabled={!canManage}
                inputMode="decimal"
                onChange={(event) => setForm({ ...form, entryFee: event.target.value })}
              />
            </label>
            <label>
              {t('op.events.fieldCapacity')}
              <input
                value={form.capacity}
                disabled={!canManage}
                inputMode="numeric"
                placeholder={t('op.events.fieldCapacityHint')}
                onChange={(event) => setForm({ ...form, capacity: event.target.value })}
              />
            </label>
            {error && <p className="ui-inline-error" role="alert">{error}</p>}
          </form>

          {/* Список записавшихся: по нему встречают на входе, поэтому рядом с именем стоит
              номер — тёзок в клубной базе больше, чем кажется. */}
          {selected !== null && (
            <section className="mgmt-drawer-section">
              <h3 className="mgmt-section-title">{t('op.events.participants')}</h3>
              {participants.length === 0 ? (
                <p className="mgmt-drawer-hint">{t('op.events.participantsEmpty')}</p>
              ) : (
                participants.map((participant) => (
                  <p key={participant.tournamentRegistrationId}>
                    {participant.displayName}
                    {participant.phoneNumber && (
                      <span className="mgmt-drawer-hint"> · {participant.phoneNumber}</span>
                    )}
                  </p>
                ))
              )}
            </section>
          )}
        </MgmtDrawer>
      )}

      {cancelTarget && (
        <CancelEventConfirmation
          event={cancelTarget}
          onCancel={() => setCancelTarget(null)}
          onConfirm={(reason) => {
            const id = cancelTarget.tournamentId;
            setCancelTarget(null);
            void cancel(id, reason);
          }}
        />
      )}
    </div>
  );
}

/// Отмена события возвращает взносы всем записавшимся, поэтому спрашивается причина: игрок
/// прочитает её в приложении вместо голого «отменено».
function CancelEventConfirmation({
  event,
  onCancel,
  onConfirm
}: {
  event: TournamentDto;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
}) {
  const { t } = useI18n();
  const [reason, setReason] = useState('');

  return (
    <CriticalActionConfirmation
      title={t('op.events.cancelTitle')}
      detail={event.title}
      impact={event.registeredCount > 0 && event.entryFee.minorUnits > 0
        ? t('op.events.cancelImpactRefund', { count: event.registeredCount })
        : t('op.events.cancelImpact', { count: event.registeredCount })}
      confirmLabel={t('op.events.cancelCta')}
      onCancel={onCancel}
      onConfirm={() => onConfirm(reason.trim())}
    >
      <label className="mgmt-form">
        {t('op.events.cancelReason')}
        <input value={reason} onChange={(input) => setReason(input.target.value)} />
      </label>
    </CriticalActionConfirmation>
  );
}
