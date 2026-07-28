import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { TariffOptionDto } from '../operatorApiClients';
import type { SessionBillingModeId } from '../operatorTypes';
import {
  billingModeOptions,
  defaultTariffRuleVersionId,
  formatMinorUnits,
  readNumber,
  readString,
  tariffOptionLabel,
  type PlayerClientItem
} from '../operatorHelpers';
import { formatDurationCompact } from '../floorMapState';
import { ClientPicker } from '../booking/ClientPicker';
import { PanelSelect } from '../PanelSelect';
import { projectOperatorError } from '../apiErrors';
import { PlatformApiError } from '../platformApi';

const START_DURATIONS = [30, 60, 120, 180] as const;

export type SessionStartSelection = {
  tariffRuleVersionId: string;
  durationMode: string;
  durationMinutes: number | null;
  billingMode: string;
  tariffVersionId: string | null;
  playerPackageId: string | null;
  isComp: boolean;
  compReason: string | null;
};

export type SessionStartClient = Pick<PlayerClientItem, 'name' | 'phoneNumber' | 'debtMinorUnits'> & {
  playerAccountId: string;
  balanceMinorUnits: number | null;
};

export function createSessionStartSelection(mode: SessionBillingModeId = 'guest'): SessionStartSelection {
  return {
    tariffRuleVersionId: defaultTariffRuleVersionId,
    durationMode: mode === 'guest' || mode === 'postpaid_debt' ? 'open' : 'fixed',
    durationMinutes: mode === 'guest' || mode === 'postpaid_debt' ? null : 60,
    billingMode: mode,
    tariffVersionId: null,
    playerPackageId: null,
    isComp: false,
    compReason: null
  };
}

export interface SessionStartFormProps {
  seatName: string;
  currencyCode: string;
  disabled: boolean;
  value: SessionStartSelection;
  onChange: (value: SessionStartSelection) => void;
  /** undefined = Map may choose a client; null = reservation guest; object = fixed reservation client. */
  fixedClient?: SessionStartClient | null;
  selectedClient?: SessionStartClient | null;
  onSelectedClientChange?: (client: SessionStartClient | null) => void;
  searchClients?: (query: string) => Promise<PlayerClientItem[]>;
  loadTariffs: () => Promise<TariffOptionDto[]>;
  loadPackages: (playerAccountId: string) => Promise<unknown[]>;
  onValidityChange?: (valid: boolean, reason: string | null) => void;
}

export function SessionStartForm({
  seatName,
  currencyCode,
  disabled,
  value,
  onChange,
  fixedClient,
  selectedClient: controlledClient,
  onSelectedClientChange,
  searchClients,
  loadTariffs,
  loadPackages,
  onValidityChange
}: SessionStartFormProps) {
  const { t } = useI18n();
  const [localClient, setLocalClient] = useState<SessionStartClient | null>(null);
  const [query, setQuery] = useState('');
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packages, setPackages] = useState<unknown[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const client = fixedClient === undefined ? (controlledClient ?? localClient) : fixedClient;
  const clientId = client?.playerAccountId ?? '';
  const requestRef = useRef(0);
  const valueRef = useRef(value);
  valueRef.current = value;

  const publishClient = (next: SessionStartClient | null) => {
    setLocalClient(next);
    onSelectedClientChange?.(next);
    if (valueRef.current.playerPackageId !== null) {
      onChange({ ...valueRef.current, playerPackageId: null });
    }
  };

  useEffect(() => {
    let disposed = false;
    setLoadError(null);
    void loadTariffs().then((items) => {
      if (disposed) return;
      setTariffs(items);
      const latest = valueRef.current;
      const current = items.find((item) => readString(item, 'tariffVersionId') === latest.tariffVersionId) ?? items[0];
      if (current && (latest.isComp || latest.billingMode !== 'guest' && latest.billingMode !== 'package')) {
        onChange({
          ...latest,
          tariffVersionId: readString(current, 'tariffVersionId') || null,
          tariffRuleVersionId: readString(current, 'tariffRuleVersionId', defaultTariffRuleVersionId)
        });
      }
    }).catch((error) => { if (!disposed) setLoadError(safeReferenceLoadError(error, t)); });
    return () => { disposed = true; };
    // Loaders identify the authenticated branch; parents memoize them.
  }, [loadTariffs]);

  useEffect(() => {
    const request = ++requestRef.current;
    setPackages([]);
    if (!clientId) return;
    void loadPackages(clientId).then((items) => {
      if (request !== requestRef.current) return;
      setPackages(items);
      const latest = valueRef.current;
      if (latest.billingMode === 'package') {
        const current = items.find((item) => readString(item, 'playerPackageId') === latest.playerPackageId) ?? items[0];
        onChange({ ...latest, playerPackageId: current ? readString(current, 'playerPackageId') || null : null });
      }
    }).catch((error) => { if (request === requestRef.current) setLoadError(safeReferenceLoadError(error, t)); });
  }, [clientId, loadPackages, value.billingMode]);

  const mode = value.billingMode as SessionBillingModeId;
  const isGuest = mode === 'guest';
  const openAllowed = mode === 'guest' || mode === 'postpaid_debt';
  const selectedTariff = tariffs.find((item) => readString(item, 'tariffVersionId') === value.tariffVersionId) ?? tariffs[0] ?? null;
  const pricePerMinute = selectedTariff ? readNumber(selectedTariff, 'pricePerMinuteMinorUnits', 0) : 0;
  const durationMinutes = value.durationMode === 'open' ? null : (value.durationMinutes ?? 60);
  const missing = value.isComp && (value.compReason?.trim().length ?? 0) < 8
    ? t('op.map.panel.compReasonRequired')
    : value.isComp && !value.tariffVersionId
      ? t('op.map.panel.billingMissingTariff')
    : !isGuest && !clientId
      ? t('op.map.panel.billingMissingPlayer')
      : (mode === 'prepaid_wallet' || mode === 'postpaid_debt') && !value.tariffVersionId
        ? t('op.map.panel.billingMissingTariff')
        : mode === 'package' && !value.playerPackageId
          ? t('op.map.panel.billingMissingPackage')
          : null;
  const valid = missing === null;
  const estimate = durationMinutes != null && mode !== 'guest' && mode !== 'package' && pricePerMinute > 0
    ? durationMinutes * pricePerMinute
    : null;
  const coverage = mode === 'prepaid_wallet' && client?.balanceMinorUnits != null && pricePerMinute > 0
    ? Math.floor(client.balanceMinorUnits / pricePerMinute)
    : null;

  useEffect(() => { onValidityChange?.(valid, missing); }, [valid, missing, onValidityChange]);

  const chooseMode = (nextMode: SessionBillingModeId) => {
    const nextTariff = tariffs[0] ?? null;
    const open = nextMode === 'guest' || nextMode === 'postpaid_debt';
    onChange({
      ...value,
      billingMode: nextMode,
      durationMode: open ? value.durationMode : 'fixed',
      durationMinutes: open && value.durationMode === 'open' ? null : (value.durationMinutes ?? 60),
      tariffVersionId: nextMode === 'guest' || nextMode === 'package' ? null : (nextTariff ? readString(nextTariff, 'tariffVersionId') || null : null),
      tariffRuleVersionId: nextTariff ? readString(nextTariff, 'tariffRuleVersionId', defaultTariffRuleVersionId) : defaultTariffRuleVersionId,
      playerPackageId: nextMode === 'package' ? (readString(packages[0], 'playerPackageId') || null) : null
    });
  };

  const searchForPicker = useCallback(async (searchQuery: string) => {
    try {
      const result = await (searchClients ?? (async () => []))(searchQuery);
      setLoadError(null);
      return result;
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
      throw error;
    }
  }, [searchClients, t]);

  const plan = useMemo(() => {
    const who = isGuest ? t('op.helper.billing.guest') : (client?.name ?? '');
    const rate = mode === 'package'
      ? readString(packages.find((item) => readString(item, 'playerPackageId') === value.playerPackageId) ?? packages[0], 'name')
      : selectedTariff ? readString(selectedTariff, 'name') : '';
    const duration = mode === 'package' ? '' : value.durationMode === 'open'
      ? t('op.map.panel.openTab')
      : formatDurationCompact((durationMinutes ?? 60) * 60, t);
    const price = estimate == null ? '' : formatMinorUnits(estimate, currencyCode);
    return [who, rate, duration, price].filter(Boolean).join(' · ');
  }, [client, currencyCode, durationMinutes, estimate, isGuest, mode, packages, selectedTariff, t, value.durationMode, value.playerPackageId]);

  return <div className="billing-selection-panel start-dialog-body" data-seat={seatName}>
    <div className="start-section-head">{t('op.map.panel.startWhoHead')}</div>
    <div className="start-segment" role="tablist" aria-label={t('op.map.panel.startWhoHead')}>
      <button type="button" role="tab" aria-selected={isGuest} className={isGuest ? 'active' : undefined}
        disabled={disabled || fixedClient !== undefined && fixedClient !== null}
        onClick={() => { chooseMode('guest'); if (fixedClient === undefined) { setQuery(''); publishClient(null); } }}>{t('op.helper.billing.guest')}</button>
      <button type="button" role="tab" aria-selected={!isGuest} className={!isGuest ? 'active' : undefined}
        disabled={disabled || fixedClient === null}
        onClick={() => { if (isGuest) chooseMode('prepaid_wallet'); }}>{t('op.map.panel.startMember')}</button>
    </div>

    {!isGuest && <>
      <div className="start-section-head">{t('op.map.panel.startPlayerHead')}</div>
      {fixedClient === undefined ? <ClientPicker
        value={query || client?.name || ''} linked={Boolean(clientId)} disabled={disabled}
        ariaLabel={t('op.map.panel.playerInput')} search={searchForPicker}
        onQueryChange={(next) => { setQuery(next); publishClient(null); }}
        onPick={(next) => { setQuery(next.name); publishClient(next); }}
        onClear={() => { setQuery(''); publishClient(null); }}
      /> : <div className="start-fixed-client"><strong>{client?.name}</strong><span>{t('op.booking.client.linked')}</span></div>}
      <div className="start-section-head">{t('op.map.panel.startChargeHead')}</div>
      <div className="start-segment three" role="tablist" aria-label={t('op.map.panel.startChargeHead')}>
        {billingModeOptions(t).filter((item) => item.id !== 'guest').map((item) => <button
          key={item.id} type="button" role="tab" aria-selected={mode === item.id}
          className={mode === item.id ? 'active' : undefined} disabled={disabled || value.isComp}
          onClick={() => chooseMode(item.id)}>{item.label}</button>)}
      </div>
      {coverage != null && client?.balanceMinorUnits != null && <p className="start-coverage">{t('op.map.panel.startBalanceCoverage', {
        balance: formatMinorUnits(client.balanceMinorUnits, currencyCode), duration: formatDurationCompact(coverage * 60, t)
      })}</p>}
    </>}

    {(value.isComp || (!isGuest && mode !== 'package')) && <>
      <div className="start-section-head">{t('op.map.panel.tariffLabel')}</div>
      <PanelSelect className="start-select" ariaLabel={t('op.map.panel.tariffSession')}
        value={value.tariffVersionId ?? ''} disabled={disabled || tariffs.length === 0}
        placeholder={t('op.map.panel.noTariffs')}
        options={tariffs.map((item) => ({ value: readString(item, 'tariffVersionId'), label: tariffOptionLabel(item, currencyCode, t) }))}
        onChange={(tariffVersionId) => {
          const item = tariffs.find((candidate) => readString(candidate, 'tariffVersionId') === tariffVersionId);
          onChange({ ...value, tariffVersionId, tariffRuleVersionId: item ? readString(item, 'tariffRuleVersionId', defaultTariffRuleVersionId) : defaultTariffRuleVersionId });
        }} />
    </>}
    {mode === 'package' && <>
      <div className="start-section-head">{t('op.map.panel.packageLabel')}</div>
      <PanelSelect className="start-select" ariaLabel={t('op.map.panel.packageSession')}
        value={value.playerPackageId ?? ''} disabled={disabled || packages.length === 0}
        placeholder={t('op.map.panel.noPackages')}
        options={packages.map((item) => ({
          value: readString(item, 'playerPackageId'),
          label: t('op.helper.player.packageLabel', {
            name: readString(item, 'name', t('op.helper.player.packageFallback')),
            minutes: Math.floor((readNumber(item, 'remainingIncludedSeconds') + readNumber(item, 'remainingBonusSeconds')) / 60)
          })
        }))}
        onChange={(playerPackageId) => onChange({ ...value, playerPackageId })} />
    </>}

    {mode !== 'package' && <>
      <div className="start-section-head">{t('op.map.panel.startTimeHead')}</div>
      <div className="start-duration-chips" role="group" aria-label={t('op.map.panel.sessionDuration')}>
        {START_DURATIONS.map((minutes) => <button key={minutes} type="button"
          className={value.durationMode === 'fixed' && value.durationMinutes === minutes ? 'active' : undefined}
          disabled={disabled} onClick={() => onChange({ ...value, durationMode: 'fixed', durationMinutes: minutes })}>
          {formatDurationCompact(minutes * 60, t)}
        </button>)}
        <button type="button" className={value.durationMode === 'open' ? 'active' : undefined}
          disabled={disabled || value.isComp || !openAllowed} title={openAllowed && !value.isComp ? t('op.map.panel.openTabAllowedTitle') : t('op.map.panel.openTabDisabledTitle')}
          onClick={() => onChange({ ...value, durationMode: 'open', durationMinutes: null })}>{t('op.map.panel.openTab')}</button>
      </div>
      {estimate != null && <p className="start-price">{t('op.map.panel.startPriceEstimate', {
        duration: formatDurationCompact((durationMinutes ?? 60) * 60, t), amount: formatMinorUnits(estimate, currencyCode)
      })}</p>}
    </>}

    <label className="start-comp-toggle"><input type="checkbox" disabled={disabled} checked={value.isComp}
      onChange={(event) => {
        if (!event.currentTarget.checked) {
          onChange({ ...value, isComp: false, compReason: null });
          return;
        }
        const tariff = selectedTariff ?? tariffs[0] ?? null;
        onChange({
          ...value,
          isComp: true,
          billingMode: 'guest',
          durationMode: 'fixed',
          durationMinutes: value.durationMinutes ?? 60,
          tariffVersionId: tariff ? readString(tariff, 'tariffVersionId') || null : null,
          tariffRuleVersionId: tariff ? readString(tariff, 'tariffRuleVersionId', defaultTariffRuleVersionId) : defaultTariffRuleVersionId,
          playerPackageId: null
        });
      }} />
      <span>{t('op.map.panel.compSession')}</span></label>
    {value.isComp && <label className="booking-field"><span>{t('op.map.panel.compReason')}</span><input
      aria-label={t('op.map.panel.compReason')} required minLength={8} disabled={disabled} value={value.compReason ?? ''}
      onChange={(event) => onChange({ ...value, compReason: event.currentTarget.value })} /></label>}
    {loadError && <p className="checkout-error" role="alert">{loadError}</p>}
    <div className={`start-plan${missing ? ' is-missing' : ''}`}><span>{missing ? t('op.map.panel.startNeed') : t('op.map.panel.startPlanLabel')}</span><strong>{missing ?? plan}</strong></div>
  </div>;
}

function safeReferenceLoadError(error: unknown, t: Parameters<typeof projectOperatorError>[1]): string {
  const projected = projectOperatorError(error, t);
  return error instanceof PlatformApiError && projected.detail !== error.message
    ? projected.detail
    : t('op.error.actionFailed.noDetail');
}
