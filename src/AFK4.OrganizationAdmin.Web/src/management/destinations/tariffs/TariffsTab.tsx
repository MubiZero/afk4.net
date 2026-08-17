import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Archive, Pencil, Tag } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import type { RowAction } from '../../kit/types';
import { PanelModal } from '../../../PanelModal';
import { CriticalActionConfirmation, Money } from '../../../operatorPrimitives';
import { projectOperatorError } from '../../../apiErrors';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
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

type Tariff = Record<string, unknown>;

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
    setMinimumMinutes(String(readNumber(selectedTariff, 'minimumBillableMinutes', 15)));
    setRoundingMinutes(String(readNumber(selectedTariff, 'roundingIncrementMinutes', 5)));
    setEffectiveFromUtc(readString(selectedTariff, 'effectiveFromUtc', new Date().toISOString()));
    setSchedule(scheduleFromOption(
      readNumber(selectedTariff, 'appliesOnDaysMask', 0),
      readOptionalNumber(selectedTariff, 'appliesFromMinuteOfDay'),
      readOptionalNumber(selectedTariff, 'appliesToMinuteOfDay')));
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
      const tariff = await apiClients.settings.createTariff(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: trimmedName,
        idempotencyKey: createIdempotencyKey('tariff-create'),
        ...schedulePayload
      });
      const tariffId = readString(tariff, 'tariffId');
      if (tariffId) {
        await apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
          organizationId: nextBackend.session.organizationId,
          tariffId,
          currencyCode,
          pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
          minimumBillableMinutes,
          roundingIncrementMinutes,
          effectiveFromUtc: new Date().toISOString(),
          idempotencyKey: createIdempotencyKey('tariff-version-create')
        });
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
        ...schedulePayload
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
        isActive: false,
        // Часы переносятся как есть: снятие с продажи — это про доступность, а не про
        // расписание, и молча стереть его здесь значит вернуть тариф из архива уже круглосуточным.
        appliesOnDaysMask: readNumber(tariffOption, 'appliesOnDaysMask', 0),
        appliesFromMinuteOfDay: readOptionalNumber(tariffOption, 'appliesFromMinuteOfDay'),
        appliesToMinuteOfDay: readOptionalNumber(tariffOption, 'appliesToMinuteOfDay')
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
            { key: 'name', header: t('op.management.tariffs.col.name'), render: (tariff) => readString(tariff, 'name', t('op.settings.tariffs.tariffFallback')) },
            {
              key: 'price',
              header: t('op.management.tariffs.col.pricePerHour'),
              align: 'end',
              render: (tariff) => <Money minorUnits={readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60} currencyCode={readString(tariff, 'currencyCode', currencyCode)} />
            },
            { key: 'min', header: t('op.management.tariffs.col.minMinutes'), align: 'end', render: (tariff) => String(readNumber(tariff, 'minimumBillableMinutes', 0)) },
            { key: 'rounding', header: t('op.management.tariffs.col.rounding'), align: 'end', render: (tariff) => String(readNumber(tariff, 'roundingIncrementMinutes', 0)) },
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
          gridTemplate="1.3fr 0.9fr 0.8fr 0.8fr 1.1fr 0.7fr"
          selectedKey={selectedTariffVersionId}
          onSelectRow={(tariff) => setSelectedTariffVersionId(readString(tariff, 'tariffVersionId'))}
          rowActions={rowActions}
          toolbar={{
            title: t('op.settings.tariffs.title'),
            primary: canManageTariffs ? { label: t('op.management.tariffs.addTariffCta'), onClick: openCreate } : undefined
          }}
          empty={{
            icon: <Tag size={22} aria-hidden="true" />,
            title: t('op.management.tariffs.tariffsEmpty.title'),
            description: t('op.management.tariffs.tariffsEmpty.description'),
            action: canManageTariffs ? { label: t('op.management.tariffs.addTariffCta'), onClick: openCreate } : undefined
          }}
        />

        {selectedTariff && (
          <MgmtDrawer
            title={readString(selectedTariff, 'name', t('op.settings.tariffs.tariffFallback'))}
            subtitle={readBoolean(selectedTariff, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive')}
            actions={drawerActions}
            onClose={() => setSelectedTariffVersionId(null)}
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
              </div>
            </form>
          </MgmtDrawer>
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
