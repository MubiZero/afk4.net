import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Button, Inspector } from '@afk4/ui/react';
import { ScreenAction } from '../../ManagementScreen';
import { Archive, Pencil, Tag } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { SkeletonTable } from '../../../LoadingSkeleton';
import type { RowAction } from '../../kit/types';
import { PanelModal } from '../../../PanelModal';
import { CriticalActionConfirmation, Money } from '../../../operatorPrimitives';
import { projectOperatorError } from '../../../apiErrors';
import { retryKeys } from '../../../unsettledKeys';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  formatMoneyInputMinorUnits,
  isGuid,
  parseMoneyInputMinorUnits,
  readBoolean,
  readNumber,
  readOptionalNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { TariffOptionDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

// Настоящий тип, а не `Record<string, unknown>`: поле, которого в ответе сервера нет, теперь заметит компилятор.
type Tariff = TariffOptionDto;

const TARIFFS_GRID = '1.3fr 0.9fr 0.8fr 0.8fr 1.1fr 0.7fr';

// Контракт делает оба поля обязательными (сервер всегда шлёт реальное значение — TariffOptionDto.cs),
// но readNumber — защитный доступ на случай устаревшего кэша или неполных данных превью. Резерв на
// этот случай один и тот же в списке и в форме: иначе список показывал 0 (значение, которое сервер
// никогда не примет — оба поля обязаны быть больше нуля), а открытая форма той же строки — 15/5, и
// «Сохранить» без единой правки тихо подменяло одно на другое.
const FALLBACK_MINIMUM_MINUTES = 15;
const FALLBACK_ROUNDING_MINUTES = 5;

export function TariffsTabSkeleton({ canManageTariffs }: { canManageTariffs: boolean }) {
  return (
    <div className="mgmt-master-detail">
      <SkeletonTable gridTemplate={TARIFFS_GRID} rowActions={canManageTariffs} />
    </div>
  );
}

interface RetireTariffAction {
  tariffId: string;
  tariffVersionId: string;
  name: string;
}

// datetime-local <-> ISO helpers (same shape as NewsWorkspace's toLocalInput/toIsoOrNull) —
// "Действует с" is edited as a local wall-clock value but stored/sent as UTC ISO.
function isoToLocalInput(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
function localInputToIso(localValue: string): string {
  const parsed = new Date(localValue);
  return Number.isNaN(parsed.getTime()) ? new Date().toISOString() : parsed.toISOString();
}

import { TariffScheduleFields } from './TariffScheduleFields';
import {
  ALL_HOURS,
  DAY_LABEL_KEYS,
  describeSchedule,
  scheduleFromOption,
  toSchedulePayload,
  type TariffScheduleForm
} from './tariffSchedule';

// Вкладка «Тарифы» раздела «Тарифы и пакеты»: список версий тарифов + drawer-редактор выбранной
// версии (портировано из прежней формы настроек, удалённой вместе с разделом Settings:
// createTariff/updateTariff/
// updateTariffVersion 1:1 — те же клиентские вызовы, идемпотентные ключи и двойной permission-гейт,
// canManageTariffs проп + серверный hasPermission на каждый вызов). Создание — в PanelModal
// (createTariff → createTariffVersion одним потоком); редактирование — прямо в открытом drawer
// (поля версии + «Сохранить» в footer), «Снять с продажи» — через CriticalActionConfirmation,
// которого в оригинальной форме не было (сознательное усиление, см. task-C1-tariffs-brief.md).
export function TariffsTab({
  tariffs,
  currencyCode,
  backend,
  canManageTariffs,
  onReload,
  onFeedback
}: {
  tariffs: TariffOptionDto[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  canManageTariffs: boolean;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [retireAction, setRetireAction] = useState<RetireTariffAction | null>(null);
  const [name, setName] = useState('');
  const [pricePerHour, setPricePerHour] = useState('90.00');
  const [minimumMinutes, setMinimumMinutes] = useState('15');
  const [roundingMinutes, setRoundingMinutes] = useState('5');
  const [effectiveFromUtc, setEffectiveFromUtc] = useState(() => new Date().toISOString());
  const [schedule, setSchedule] = useState<TariffScheduleForm>(ALL_HOURS);
  const [featuredOnPcs, setFeaturedOnPcs] = useState(false);
  const [busy, setBusy] = useState(false);

  // Если выбранная версия пропала из выборки (снята с продажи/reload) — закрыть drawer, а не
  // показывать устаревшую запись.
  useEffect(() => {
    setSelectedTariffVersionId((current) => (current && tariffs.some((tariff) => readString(tariff, 'tariffVersionId') === current) ? current : null));
  }, [tariffs]);

  const scheduleLabels = {
    dayNames: DAY_LABEL_KEYS.map((key) => t(key)),
    always: t('op.management.tariffs.schedule.always')
  };

  const selectedTariff = tariffs.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId) ?? null;

  // Засев формы drawer'а из выбранной версии — цена показывается за ЧАС (×60), хранится за
  // минуту.
  useEffect(() => {
    if (!selectedTariff) return;
    setName(readString(selectedTariff, 'name'));
    setPricePerHour(formatMoneyInputMinorUnits(readNumber(selectedTariff, 'pricePerMinuteMinorUnits', 0) * 60));
    setMinimumMinutes(String(readNumber(selectedTariff, 'minimumBillableMinutes', FALLBACK_MINIMUM_MINUTES)));
    setRoundingMinutes(String(readNumber(selectedTariff, 'roundingIncrementMinutes', FALLBACK_ROUNDING_MINUTES)));
    setEffectiveFromUtc(readString(selectedTariff, 'effectiveFromUtc', new Date().toISOString()));
    setSchedule(scheduleFromOption(
      readNumber(selectedTariff, 'appliesOnDaysMask', 0),
      readOptionalNumber(selectedTariff, 'appliesFromMinuteOfDay'),
      readOptionalNumber(selectedTariff, 'appliesToMinuteOfDay')));
    setFeaturedOnPcs(readBoolean(selectedTariff, 'featuredOnPcs', false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedTariffVersionId]);

  const openCreate = () => {
    setName(t('op.settings.prefill.tariffName'));
    setPricePerHour('90.00');
    setMinimumMinutes('15');
    setRoundingMinutes('5');
    setSchedule(ALL_HOURS);
    setCreateOpen(true);
  };

  const submitCreate = async () => {
    const label = t('op.settings.action.createTariff');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
        throw new Error(t('op.settings.tariffs.error.noPerm'));
      }

      const trimmedName = name.trim();
      const pricePerHourMinorUnits = parseMoneyInputMinorUnits(pricePerHour);
      const minimumBillableMinutes = Number(minimumMinutes);
      const roundingIncrementMinutes = Number(roundingMinutes);
      if (!trimmedName || pricePerHourMinorUnits === null
        || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
        || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
        throw new Error(t('op.settings.tariffs.error.fillCreate'));
      }

      const schedulePayload = toSchedulePayload(schedule);
      if (!schedulePayload) {
        throw new Error(t('op.settings.tariffs.error.schedule'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const newTariff = { organizationId: nextBackend.session.organizationId, name: trimmedName, schedule: schedulePayload };
      // Повтор после обрыва вернёт уже заведённый тариф, а не второй с тем же именем.
      const tariff = await retryKeys.send('tariff-create', [nextBackend.branchId, newTariff], (idempotencyKey) =>
        apiClients.settings.createTariff(nextBackend.branchId, { ...newTariff, idempotencyKey }));
      const tariffId = readString(tariff, 'tariffId');
      if (tariffId) {
        const price = {
          organizationId: nextBackend.session.organizationId,
          tariffId,
          currencyCode,
          pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
          minimumBillableMinutes,
          roundingIncrementMinutes
        };
        // Цена действует с первого нажатия: повтор после обрыва пришлёт ту же дату и тот же ключ,
        // а не вторую версию цены на минуту позже.
        await retryKeys.send('tariff-version-create', [nextBackend.branchId, price], (idempotencyKey, firstAttemptAt) =>
          apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
            ...price,
            effectiveFromUtc: firstAttemptAt.toISOString(),
            idempotencyKey
          }));
      }
      setCreateOpen(false);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitEdit = async () => {
    if (!selectedTariff) return;
    const label = t('op.settings.action.updateTariff');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
        throw new Error(t('op.settings.tariffs.error.noPerm'));
      }

      const tariffId = readString(selectedTariff, 'tariffId');
      const tariffVersionId = readString(selectedTariff, 'tariffVersionId');
      const trimmedName = name.trim();
      const pricePerHourMinorUnits = parseMoneyInputMinorUnits(pricePerHour);
      const minimumBillableMinutes = Number(minimumMinutes);
      const roundingIncrementMinutes = Number(roundingMinutes);
      if (!isGuid(tariffId) || !isGuid(tariffVersionId) || !trimmedName || pricePerHourMinorUnits === null
        || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
        || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
        throw new Error(t('op.settings.tariffs.error.fillUpdate'));
      }

      const schedulePayload = toSchedulePayload(schedule);
      if (!schedulePayload) {
        throw new Error(t('op.settings.tariffs.error.schedule'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updateTariff(nextBackend.branchId, tariffId, {
        organizationId: nextBackend.session.organizationId,
        name: trimmedName,
        isActive: true,
        schedule: schedulePayload,
        featuredOnPcs
      });
      await apiClients.settings.updateTariffVersion(nextBackend.branchId, tariffId, tariffVersionId, {
        organizationId: nextBackend.session.organizationId,
        currencyCode,
        pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
        minimumBillableMinutes,
        roundingIncrementMinutes,
        effectiveFromUtc,
        isActive: true
      });
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmRetire = async () => {
    if (!retireAction) return;
    const label = t('op.settings.action.retireTariff');
    const { tariffId, tariffVersionId } = retireAction;
    setRetireAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
        throw new Error(t('op.settings.tariffs.error.noPerm'));
      }

      const tariffOption = tariffs.find((tariff) => readString(tariff, 'tariffVersionId') === tariffVersionId);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updateTariff(nextBackend.branchId, tariffId, {
        organizationId: nextBackend.session.organizationId,
        name: readString(tariffOption, 'name', name),
        isActive: false
        // Расписание не передаётся вовсе: снятие с продажи — про доступность, а не про часы, и
        // сервер оставляет их как есть.
      });
      await apiClients.settings.updateTariffVersion(nextBackend.branchId, tariffId, tariffVersionId, {
        organizationId: nextBackend.session.organizationId,
        currencyCode: readString(tariffOption, 'currencyCode', currencyCode),
        pricePerMinuteMinorUnits: readNumber(tariffOption, 'pricePerMinuteMinorUnits', 0),
        minimumBillableMinutes: readNumber(tariffOption, 'minimumBillableMinutes', 1),
        roundingIncrementMinutes: readNumber(tariffOption, 'roundingIncrementMinutes', 1),
        effectiveFromUtc: readString(tariffOption, 'effectiveFromUtc', new Date().toISOString()),
        isActive: false
      });
      setSelectedTariffVersionId((current) => (current === tariffVersionId ? null : current));
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const rowActions = canManageTariffs
    ? (tariff: Tariff): RowAction[] => [
      {
        id: 'edit',
        label: t('op.settings.action.updateTariff'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => setSelectedTariffVersionId(readString(tariff, 'tariffVersionId'))
      },
      {
        id: 'retire',
        label: t('op.settings.action.retireTariff'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        disabled: !readBoolean(tariff, 'isActive', true),
        onSelect: () => setRetireAction({
          tariffId: readString(tariff, 'tariffId'),
          tariffVersionId: readString(tariff, 'tariffVersionId'),
          name: readString(tariff, 'name', t('op.settings.tariffs.tariffFallback'))
        })
      }
    ]
    : undefined;

  const drawerActions: RowAction[] = selectedTariff && canManageTariffs && readBoolean(selectedTariff, 'isActive', true)
    ? [
      {
        id: 'retire',
        label: t('op.settings.action.retireTariff'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setRetireAction({
          tariffId: readString(selectedTariff, 'tariffId'),
          tariffVersionId: readString(selectedTariff, 'tariffVersionId'),
          name: readString(selectedTariff, 'name', t('op.settings.tariffs.tariffFallback'))
        })
      }
    ]
    : [];

  return (
    <>
      <div className="mgmt-master-detail">
        <MgmtTable<Tariff>
          columns={[
            {
              key: 'name',
              header: t('op.management.tariffs.col.name'),
              render: (tariff) => (
                <span className="mgmt-inline-tags">
                  {readString(tariff, 'name', t('op.settings.tariffs.tariffFallback'))}
                  {readBoolean(tariff, 'featuredOnPcs', false)
                    ? <span className="ui-chip ui-chip--xs is-neutral">{t('op.news.onPcsTag')}</span>
                    : null}
                </span>
              )
            },
            {
              key: 'price',
              header: t('op.management.tariffs.col.pricePerHour'),
              align: 'end',
              render: (tariff) => <Money minorUnits={readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60} currencyCode={readString(tariff, 'currencyCode', currencyCode)} />
            },
            // «Мин. минуты 15» и «Округление 5» — голые числа без единиц: «минимум 15 мин» и
            // «по 5 мин» читаются без расшифровки.
            { key: 'min', header: t('op.management.tariffs.col.minMinutes'), align: 'end', render: (tariff) => t('op.management.tariffs.minutesValue', { minutes: readNumber(tariff, 'minimumBillableMinutes', FALLBACK_MINIMUM_MINUTES) }) },
            { key: 'rounding', header: t('op.management.tariffs.col.rounding'), align: 'end', render: (tariff) => t('op.management.tariffs.roundingValue', { minutes: readNumber(tariff, 'roundingIncrementMinutes', FALLBACK_ROUNDING_MINUTES) }) },
            {
              key: 'schedule',
              header: t('op.management.tariffs.col.schedule'),
              // Часы стоят в таблице, а не только в карточке: владелец, у которого утренний и
              // вечерний тарифы называются похоже, различает их по времени, а не по имени.
              render: (tariff) => describeSchedule(
                readNumber(tariff, 'appliesOnDaysMask', 0),
                readOptionalNumber(tariff, 'appliesFromMinuteOfDay'),
                readOptionalNumber(tariff, 'appliesToMinuteOfDay'),
                scheduleLabels)
            },
            {
              key: 'status',
              header: t('op.management.tariffs.col.status'),
              render: (tariff) => readBoolean(tariff, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive')
            }
          ]}
          rows={tariffs}
          rowKey={(tariff) => readString(tariff, 'tariffVersionId')}
          gridTemplate={TARIFFS_GRID}
          selectedKey={selectedTariffVersionId}
          onSelectRow={(tariff) => setSelectedTariffVersionId(readString(tariff, 'tariffVersionId'))}
          rowActions={rowActions}
          empty={{
            icon: <Tag size={22} aria-hidden="true" />,
            title: t('op.management.tariffs.tariffsEmpty.title'),
            description: t('op.management.tariffs.tariffsEmpty.description'),
            next: canManageTariffs ? { kind: 'formAbove' } : { kind: 'denied', hint: t('op.empty.denied.managerOrOwner') }
          }}
        />
        {canManageTariffs && (
          <ScreenAction>
            <Button variant="primary" onClick={openCreate}>{t('op.management.tariffs.addTariffCta')}</Button>
          </ScreenAction>
        )}

        {selectedTariff && (
          <Inspector
            title={readString(selectedTariff, 'name', t('op.settings.tariffs.tariffFallback'))}
            subtitle={readBoolean(selectedTariff, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive')}
            menu={drawerActions.length > 0 ? { label: t('op.management.crud.rowMenu'), actions: drawerActions } : undefined}
            close={{ label: t('common.close'), onClose: () => setSelectedTariffVersionId(null) }}
            footer={
              canManageTariffs ? (
                <div className="mgmt-form-actions">
                  <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void submitEdit()}>
                    {t('common.save')}
                  </button>
                </div>
              ) : undefined
            }
          >
            <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitEdit(); }}>
              <div className="mgmt-form-grid">
                <label>{t('op.settings.tariffs.name')}
                  <input value={name} disabled={!canManageTariffs || busy} onChange={(event) => setName(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.tariffs.pricePerHour')}
                  <input inputMode="decimal" value={pricePerHour} disabled={!canManageTariffs || busy} onChange={(event) => setPricePerHour(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.tariffs.minMinutes')}
                  <input inputMode="numeric" value={minimumMinutes} disabled={!canManageTariffs || busy} onChange={(event) => setMinimumMinutes(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.tariffs.rounding')}
                  <input inputMode="numeric" value={roundingMinutes} disabled={!canManageTariffs || busy} onChange={(event) => setRoundingMinutes(event.currentTarget.value)} />
                </label>
                <label className="mgmt-form-wide">{t('op.management.tariffs.effectiveFrom')}
                  <input
                    type="datetime-local"
                    value={isoToLocalInput(effectiveFromUtc)}
                    disabled={!canManageTariffs || busy}
                    onChange={(event) => setEffectiveFromUtc(localInputToIso(event.currentTarget.value))}
                  />
                </label>
                <TariffScheduleFields
                  value={schedule}
                  disabled={!canManageTariffs || busy}
                  onChange={setSchedule}
                />
                <label className="mgmt-check mgmt-form-wide">
                  <input
                    type="checkbox"
                    checked={featuredOnPcs}
                    disabled={!canManageTariffs || busy}
                    onChange={(event) => setFeaturedOnPcs(event.currentTarget.checked)}
                  />
                  {t('op.management.tariffs.featuredOnPcs')}
                </label>
                <p className="mgmt-drawer-hint mgmt-form-wide">{t('op.management.tariffs.featuredOnPcsHint')}</p>
              </div>
            </form>
          </Inspector>
        )}
      </div>

      {createOpen && (
        <PanelModal title={t('op.management.tariffs.tariffModal.createTitle')} onClose={() => setCreateOpen(false)} closeDisabled={busy}>
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitCreate(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.tariffs.name')}
                <input value={name} onChange={(event) => setName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.tariffs.pricePerHour')}
                <input inputMode="decimal" value={pricePerHour} onChange={(event) => setPricePerHour(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.tariffs.minMinutes')}
                <input inputMode="numeric" value={minimumMinutes} onChange={(event) => setMinimumMinutes(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.tariffs.rounding')}
                <input inputMode="numeric" value={roundingMinutes} onChange={(event) => setRoundingMinutes(event.currentTarget.value)} />
              </label>
              <TariffScheduleFields value={schedule} disabled={busy} onChange={setSchedule} />
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setCreateOpen(false)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>{t('op.settings.action.createTariff')}</button>
            </div>
          </form>
        </PanelModal>
      )}

      {retireAction && (
        <CriticalActionConfirmation
          title={t('op.management.tariffs.confirmRetireTariff.title')}
          detail={retireAction.name}
          impact={t('op.management.tariffs.confirmRetireTariff.impact')}
          confirmLabel={t('op.settings.action.retireTariff')}
          onCancel={() => setRetireAction(null)}
          onConfirm={() => void confirmRetire()}
        />
      )}
    </>
  );
}
