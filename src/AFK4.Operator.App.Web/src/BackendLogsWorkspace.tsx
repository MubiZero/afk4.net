import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, MonitorCheck, ReceiptText, Search, ShieldAlert, UserRoundPlus } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { AuditSearchResultDto, BranchDiagnosticsDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import {
  auditActionLabel,
  auditActorLabel,
  commandStatusLabel,
  commandStatusMessageLabel,
  commandTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  emptyFeedback,
  formatTime,
  isGuid,
  isRecord,
  operatorDisplayNameLabel,
  readArray,
  readNumber,
  readString,
  requireBackend,
  updateComponentLabel,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { FeedbackNotice, StateFlag } from './operatorPrimitives';

type LogEventKind = 'audit' | 'commandFailure' | 'updateFailure' | 'staleDevice' | 'placeholder';
type LogEventTone = 'audit' | 'device' | 'money' | 'session' | 'warning';
type LogEventItem = [string, string, string, string, LogEventTone, LogEventKind, Record<string, unknown> | null];
type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

function logEventPlaceholder(loadStatus: LoadStatus, loadError: string | null, hasSearchMiss: boolean, t: TFn): LogEventItem {
  if (hasSearchMiss) {
    return ['—', t('op.logs.ph.noMatch.title'), t('op.logs.ph.noMatch.hint'), t('op.logs.ph.source.operator'), 'audit', 'placeholder', null];
  }

  if (loadStatus === 'loading') {
    return ['—', t('op.logs.ph.loading.title'), t('op.logs.ph.loading.hint'), t('op.logs.ph.source.platform'), 'audit', 'placeholder', null];
  }

  if (loadStatus === 'failed') {
    return ['—', t('op.logs.ph.failed.title'), loadError ?? t('op.logs.ph.failed.hint'), t('op.logs.ph.source.platform'), 'warning', 'placeholder', null];
  }

  if (loadStatus === 'backend') {
    return ['—', t('op.logs.ph.empty.title'), t('op.logs.ph.empty.hint'), t('op.logs.ph.source.platform'), 'audit', 'placeholder', null];
  }

  return ['—', t('op.logs.ph.local.title'), t('op.logs.ph.local.hint'), t('op.logs.ph.source.platform'), 'audit', 'placeholder', null];
}

function mapAuditRecordsToLogEvents(auditRecords: Record<string, unknown>[], t: TFn): LogEventItem[] {
  return auditRecords.map((record): LogEventItem => {
    const outcome = readString(record, 'outcome');

    return [
      formatTime(readString(record, 'createdAtUtc')),
      auditActionLabel(readString(record, 'action'), t),
      `${auditTargetLabel(readString(record, 'targetType'), t)} · ${auditOutcomeLabel(outcome, t)}`,
      auditSourceLabel(readString(record, 'sourceApp'), t),
      outcome.toLowerCase().includes('denied') || outcome.toLowerCase().includes('failed') ? 'warning' : 'audit',
      'audit',
      record
    ];
  });
}

function auditOutcomeLabel(outcome: string, t: TFn): string {
  switch (outcome.toLowerCase()) {
    case 'succeeded':
    case 'success':
    case 'ok':
      return t('op.logs.outcome.success');
    case 'failed':
    case 'failure':
      return t('op.logs.outcome.failed');
    case 'denied':
    case 'rejected':
      return t('op.logs.outcome.denied');
    case 'pending':
      return t('op.logs.outcome.pending');
    default:
      return outcome ? t('op.logs.outcome.unknown') : t('op.logs.outcome.empty');
  }
}

function auditTargetLabel(targetType: string, t: TFn): string {
  const normalized = targetType.toLowerCase();
  if (normalized.includes('pos') || normalized.includes('sale') || normalized.includes('receipt')) {
    return t('op.logs.target.receipt');
  }

  if (normalized.includes('session')) {
    return t('op.logs.target.session');
  }

  if (normalized.includes('device')) {
    return t('op.logs.target.device');
  }

  if (normalized.includes('staff') || normalized.includes('identity') || normalized.includes('user')) {
    return t('op.logs.target.staff');
  }

  if (normalized.includes('shift')) {
    return t('op.logs.target.shift');
  }

  if (normalized.includes('payment') || normalized.includes('ledger') || normalized.includes('wallet')) {
    return t('op.logs.target.payment');
  }

  if (normalized.includes('tariff')) {
    return t('op.logs.target.tariff');
  }

  if (normalized.includes('package')) {
    return t('op.logs.target.package');
  }

  if (normalized.includes('update') || normalized.includes('rollout')) {
    return t('op.logs.target.update');
  }

  if (normalized.includes('branch')) {
    return t('op.logs.target.branch');
  }

  return t('op.logs.target.object');
}

function auditSourceLabel(sourceApp: string, t: TFn): string {
  const normalized = sourceApp.toLowerCase();
  if (!normalized || normalized === 'audit' || normalized.includes('platform')) {
    return t('op.logs.sourceApp.platform');
  }

  if (normalized.includes('operator')) {
    return t('op.logs.sourceApp.operator');
  }

  if (normalized.includes('agent')) {
    return t('op.logs.sourceApp.agent');
  }

  if (normalized.includes('setup')) {
    return t('op.logs.sourceApp.setup');
  }

  return t('op.logs.sourceApp.platform');
}

function auditDetailKeyLabel(key: string, t: TFn): string {
  const normalized = key.toLowerCase();
  if (normalized.includes('reason')) {
    return t('op.logs.detailKey.reason');
  }

  if (normalized.includes('amount') || normalized.includes('money') || normalized.includes('price')) {
    return t('op.logs.detailKey.amount');
  }

  if (normalized.includes('currency')) {
    return t('op.logs.detailKey.currency');
  }

  if (normalized.includes('status') || normalized.includes('state') || normalized.includes('outcome')) {
    return t('op.logs.detailKey.status');
  }

  if (normalized.includes('device') || normalized.includes('machine')) {
    return t('op.logs.detailKey.device');
  }

  if (normalized.includes('session')) {
    return t('op.logs.detailKey.session');
  }

  if (normalized.includes('sale') || normalized.includes('receipt') || normalized.includes('pos')) {
    return t('op.logs.detailKey.sale');
  }

  if (normalized.includes('shift')) {
    return t('op.logs.detailKey.shift');
  }

  if (normalized.includes('tariff')) {
    return t('op.logs.detailKey.tariff');
  }

  if (normalized.includes('package')) {
    return t('op.logs.detailKey.package');
  }

  if (normalized.includes('staff') || normalized.includes('user')) {
    return t('op.logs.detailKey.staff');
  }

  if (normalized.includes('branch')) {
    return t('op.logs.detailKey.branch');
  }

  return t('op.logs.detailKey.param');
}

function auditDetailValueLabel(value: unknown, t: TFn): string {
  if (value === null || value === undefined) {
    return t('op.logs.detailVal.null');
  }

  if (typeof value === 'boolean') {
    return value ? t('op.logs.detailVal.yes') : t('op.logs.detailVal.no');
  }

  if (typeof value === 'number') {
    return String(value);
  }

  if (Array.isArray(value)) {
    return t('op.logs.detailVal.count', { count: value.length });
  }

  if (isRecord(value)) {
    return t('op.logs.detailVal.record');
  }

  const trimmed = String(value).trim();
  if (trimmed.length === 0) {
    return t('op.logs.detailVal.null');
  }

  if (isGuid(trimmed)) {
    return t('op.logs.detailVal.guid');
  }

  if (/^https?:\/\//i.test(trimmed)) {
    return t('op.logs.detailVal.url');
  }

  if (/^\d{4}-\d{2}-\d{2}T/.test(trimmed)) {
    return formatTime(trimmed);
  }

  return trimmed.length > 80 ? `${trimmed.slice(0, 80)}…` : trimmed;
}

function mapDiagnosticsToLogEvents(diagnostics: BranchDiagnosticsDto | null, agentSource: string, updatesSource: string, t: TFn): LogEventItem[] {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const recentCommandFailures = readArray<Record<string, unknown>>(commandSummary, 'recentFailures');
  const recentUpdateFailures = readArray<Record<string, unknown>>(updateSummary, 'recentFailures');
  const staleDevices = readArray<Record<string, unknown>>(diagnostics, 'staleDevices');

  return [
    ...recentCommandFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'machineName', t('op.logs.row.deviceFallback'))} ${commandTypeLabel(readString(failure, 'type', 'command'), t)}`,
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      agentSource,
      'device',
      'commandFailure',
      failure
    ]),
    ...recentUpdateFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${updateComponentLabel(readString(failure, 'component', t('op.logs.row.updateFallback')), t)} ${readString(failure, 'targetVersion', '')}`.trim(),
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      updatesSource,
      'warning',
      'updateFailure',
      failure
    ]),
    ...staleDevices.map((device): LogEventItem => [
      formatTime(readString(device, 'lastHeartbeatAtUtc')),
      `${readString(device, 'machineName', t('op.logs.row.deviceFallback'))} ${t('op.logs.diag.notResponding')}`,
      t('op.logs.diag.staleSeconds', { count: readNumber(device, 'lastHeartbeatAgeSeconds', 0) }),
      agentSource,
      'warning',
      'staleDevice',
      device
    ])
  ];
}

function compactAuditDetails(detailsJson: string, t: TFn): string {
  const trimmed = detailsJson.trim();
  if (trimmed.length === 0 || trimmed === '{}') {
    return t('op.logs.compact.noDetails');
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (!isRecord(parsed)) {
      return auditDetailValueLabel(parsed, t);
    }

    const entries = Object.entries(parsed)
      .filter(([, value]) => value !== null && value !== undefined && String(value).trim().length > 0)
      .slice(0, 3)
      .map(([key, value]) => `${auditDetailKeyLabel(key, t)}: ${auditDetailValueLabel(value, t)}`);

    return entries.length > 0 ? entries.join(' · ') : t('op.logs.compact.noDetails');
  } catch {
    return t('op.logs.compact.freeform');
  }
}

function logEventKey(event: LogEventItem): string {
  const record = event[6];
  const recordId = readString(record, 'auditRecordId')
    || readString(record, 'commandId')
    || readString(record, 'updateRolloutId')
    || readString(record, 'deviceId')
    || `${event[0]}-${event[1]}`;

  return `${event[5]}-${recordId}-${event[0]}-${event[1]}`;
}

function buildLogEventDetailRows(event: LogEventItem, backend: OperatorBackendContext | null, t: TFn): Array<[string, string]> {
  const record = event[6];

  if (event[5] === 'audit' && record !== null) {
    const targetType = readString(record, 'targetType');

    return [
      [t('op.logs.row.event'), auditActionLabel(readString(record, 'action'), t)],
      [t('op.logs.row.result'), auditOutcomeLabel(readString(record, 'outcome'), t)],
      [t('op.logs.row.section'), auditTargetLabel(targetType, t)],
      [t('op.logs.row.actor'), auditActorLabel(record, backend, t)],
      [t('op.logs.row.source'), auditSourceLabel(readString(record, 'sourceApp'), t)],
      [t('op.logs.row.details'), compactAuditDetails(readString(record, 'detailsJson'), t)]
    ];
  }

  if (event[5] === 'commandFailure' && record !== null) {
    return [
      [t('op.logs.row.device'), readString(record, 'machineName', t('op.logs.row.deviceFallback'))],
      [t('op.logs.row.command'), commandTypeLabel(readString(record, 'type', 'command'), t)],
      [t('op.logs.row.status'), commandStatusLabel(readString(record, 'status', 'failed'), t)],
      [t('op.logs.row.message'), commandStatusMessageLabel(readString(record, 'message')) || t('op.logs.row.noMessage')],
      [t('op.logs.row.updated'), readString(record, 'updatedAtUtc', event[0])]
    ];
  }

  if (event[5] === 'updateFailure' && record !== null) {
    return [
      [t('op.logs.row.device'), readString(record, 'machineName', t('op.logs.row.deviceFallback'))],
      [t('op.logs.row.component'), `${updateComponentLabel(readString(record, 'component', t('op.logs.row.updateFallback')), t)} ${readString(record, 'targetVersion')}`.trim()],
      [t('op.logs.row.status'), commandStatusLabel(readString(record, 'status', 'failed'), t)],
      [t('op.logs.row.message'), commandStatusMessageLabel(readString(record, 'message')) || t('op.logs.row.noMessage')]
    ];
  }

  if (event[5] === 'staleDevice' && record !== null) {
    return [
      [t('op.logs.row.device'), readString(record, 'machineName', t('op.logs.row.deviceFallback'))],
      [t('op.logs.row.agentVersion'), readString(record, 'agentVersion', t('op.logs.row.unknown'))],
      [t('op.logs.row.shellVersion'), readString(record, 'shellVersion', t('op.logs.row.unknown'))],
      [t('op.logs.row.lastSignal'), readString(record, 'lastHeartbeatAtUtc', event[0])],
      [t('op.logs.row.signalPause'), t('op.logs.diag.pauseSeconds', { count: readNumber(record, 'lastHeartbeatAgeSeconds', 0) })]
    ];
  }

  return [
    [t('op.logs.row.source'), event[3]],
    [t('op.logs.row.event'), event[1]],
    [t('op.logs.row.operator'), operatorDisplayNameLabel(backend?.session.displayName ?? null, t)],
    [t('op.logs.row.result'), event[2]]
  ];
}

function logToneLabel(tone: LogEventTone, t: TFn): string {
  switch (tone) {
    case 'warning':
      return t('op.logs.tone.warning');
    case 'device':
      return t('op.logs.tone.device');
    case 'money':
      return t('op.logs.tone.money');
    case 'session':
      return t('op.logs.tone.session');
    default:
      return t('op.logs.tone.audit');
  }
}

function logKindLabel(kind: LogEventKind, t: TFn): string {
  switch (kind) {
    case 'commandFailure':
      return t('op.logs.kind.commandFailure');
    case 'updateFailure':
      return t('op.logs.kind.updateFailure');
    case 'staleDevice':
      return t('op.logs.kind.staleDevice');
    case 'placeholder':
      return t('op.logs.kind.placeholder');
    default:
      return t('op.logs.kind.audit');
  }
}

function buildLogsExportJson(
  branchId: string,
  auditRecords: Record<string, unknown>[],
  diagnostics: BranchDiagnosticsDto | null,
  events: LogEventItem[],
  backend: OperatorBackendContext | null,
  t: TFn
): string {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  return JSON.stringify({
    exportedAtUtc: new Date().toISOString(),
    branch: branchId ? t('op.logs.export.branch') : t('op.logs.export.noBranch'),
    summary: {
      events: events.length,
      auditRecords: auditRecords.length,
      warnings: events.filter((event) => event[4] === 'warning').length,
      devices: {
        total: readNumber(deviceSummary, 'totalDevices', 0),
        online: readNumber(deviceSummary, 'onlineDevices', 0),
        stale: readNumber(deviceSummary, 'staleDevices', 0)
      },
      commands: {
        pending: readNumber(commandSummary, 'pendingCommands', 0),
        failed: readNumber(commandSummary, 'failedCommands', 0)
      },
      updates: {
        failedDevices: readNumber(updateSummary, 'failedDevices', 0),
        rollbackDevices: readNumber(updateSummary, 'rollbackDevices', 0)
      }
    },
    events: events.map(([time, title, detail, source, tone, kind, record]) => ({
      time,
      title,
      detail,
      source,
      result: logToneLabel(tone, t),
      section: logKindLabel(kind, t),
      details: Object.fromEntries(buildLogEventDetailRows([time, title, detail, source, tone, kind, record], backend, t))
    }))
  }, null, 2);
}

function matchesLogSource(event: LogEventItem, sourceFilter: string, allKey: string, agentKey: string, posKey: string, operatorKey: string, platformKey: string): boolean {
  if (sourceFilter === allKey) {
    return true;
  }

  const [, title, detail, source, tone, kind, record] = event;
  const normalizedTitle = title.toLowerCase();
  const normalizedDetail = detail.toLowerCase();
  const normalizedSource = source.toLowerCase();
  const action = readString(record, 'action').toLowerCase();
  const targetType = readString(record, 'targetType').toLowerCase();
  const isAgent = source === agentKey || tone === 'device' || kind === 'commandFailure' || kind === 'staleDevice';
  const isPos = normalizedTitle.includes('касс') || normalizedTitle.includes('чек') || normalizedDetail.includes('касс') || normalizedDetail.includes('чек') || action.includes('pos') || targetType.includes('pos');
  const isOperator = normalizedSource.includes('operator') || normalizedSource.includes('оператор') || normalizedTitle.includes('identity') || normalizedDetail.includes('staff') || action.includes('identity') || targetType.includes('staff');
  const isPlatform = source === platformKey || kind === 'updateFailure' || (!isAgent && !isPos && !isOperator);

  if (sourceFilter === agentKey) {
    return isAgent;
  }

  if (sourceFilter === posKey) {
    return isPos;
  }

  if (sourceFilter === operatorKey) {
    return isOperator;
  }

  if (sourceFilter === platformKey) {
    return isPlatform;
  }

  return true;
}

type AuditSearchOverrides = {
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  limit?: number | null;
};
type AuditPeriodPreset = 'today' | 'last24h' | 'last7d';

function auditPeriodPresetRange(preset: AuditPeriodPreset, now = new Date()): Pick<AuditSearchOverrides, 'fromUtc' | 'toUtc'> {
  const toUtc = now.toISOString();
  if (preset === 'today') {
    return {
      fromUtc: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())).toISOString(),
      toUtc
    };
  }

  const hours = preset === 'last24h' ? 24 : 24 * 7;
  return {
    fromUtc: new Date(now.getTime() - hours * 60 * 60 * 1000).toISOString(),
    toUtc
  };
}

export function BackendLogsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState(() => t('op.logs.logFilter.allEvents'));
  const [selectedEventKey, setSelectedEventKey] = useState('');
  const [selectedSource, setSelectedSource] = useState(() => t('op.logs.source.all'));
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [auditActionFilter, setAuditActionFilter] = useState('');
  const [auditOutcomeFilter, setAuditOutcomeFilter] = useState('');
  const [auditTargetTypeFilter, setAuditTargetTypeFilter] = useState('');
  const [auditFromUtcFilter, setAuditFromUtcFilter] = useState('');
  const [auditToUtcFilter, setAuditToUtcFilter] = useState('');
  const [auditLimit, setAuditLimit] = useState('30');

  // Sentinel constants — every comparison and every display comes from the same t() call
  const sourceAllKey = t('op.logs.source.all');
  const sourceAgentKey = t('op.logs.source.agent');
  const sourcePosKey = t('op.logs.source.pos');
  const sourceOperatorKey = t('op.logs.source.operator');
  const sourcePlatformKey = t('op.logs.source.platform');
  const sourceUpdatesKey = t('op.logs.source.updates');

  const logFilterAllKey = t('op.logs.logFilter.allEvents');
  const logFilterErrorsKey = t('op.logs.logFilter.errorsOnly');
  const logFilterPcKey = t('op.logs.logFilter.pcAndNet');
  const logFilterPosKey = t('op.logs.logFilter.pos');
  const logFilterOperatorKey = t('op.logs.logFilter.operator');
  const logFilterSystemKey = t('op.logs.logFilter.system');

  const supportBundleActionKey = t('op.logs.export.supportBundle');
  const actionListActionKey = t('op.logs.export.actionList');
  const onlyProblemsActionKey = t('op.logs.export.onlyProblems');
  const shiftSummaryActionKey = t('op.logs.export.shiftSummary');

  const applyFilterActionKey = t('op.logs.filter.applyBtn');

  const auditPeriodPresetOptions: Array<[string, AuditPeriodPreset]> = [
    [t('op.logs.period.today'), 'today'],
    [t('op.logs.period.last24h'), 'last24h'],
    [t('op.logs.period.last7d'), 'last7d']
  ];

  const buildAuditSearchRequest = (nextBackend: OperatorBackendContext, overrides: AuditSearchOverrides = {}) => {
    const limitValue = overrides.limit ?? Number(auditLimit);
    const limit = Number.isInteger(limitValue) && limitValue > 0 ? Math.min(limitValue, 200) : 30;

    return {
      branchId: nextBackend.branchId,
      action: overrides.action !== undefined ? overrides.action : auditActionFilter.trim(),
      outcome: overrides.outcome !== undefined ? overrides.outcome : auditOutcomeFilter.trim(),
      targetType: overrides.targetType !== undefined ? overrides.targetType : auditTargetTypeFilter.trim(),
      fromUtc: overrides.fromUtc !== undefined ? overrides.fromUtc : auditFromUtcFilter.trim(),
      toUtc: overrides.toUtc !== undefined ? overrides.toUtc : auditToUtcFilter.trim(),
      limit
    };
  };

  const loadLogs = async (nextBackend = backend, auditOverrides: AuditSearchOverrides = {}) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }

    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [audit, branchDiagnostics] = await Promise.all([
        apiClients.audit.search(buildAuditSearchRequest(nextBackend, auditOverrides)),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId)
      ]);
      setAuditResult(audit);
      setDiagnostics(branchDiagnostics);
      setSelectedEventKey('');
      setLoadError(null);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: t('op.logs.feedbackLabel'), state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadLogs();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const auditEvents = mapAuditRecordsToLogEvents(auditRecords, t);
  const diagnosticEvents = mapDiagnosticsToLogEvents(diagnostics, sourceAgentKey, sourceUpdatesKey, t);
  const events = [...diagnosticEvents, ...auditEvents];
  const filteredEvents = events.filter((event) => {
    const [time, title, detail, source, tone] = event;
    const filterMatches = activeLogFilter === logFilterAllKey
      || (activeLogFilter === logFilterErrorsKey && tone === 'warning')
      || (activeLogFilter === logFilterPcKey && matchesLogSource(event, sourceAgentKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey))
      || (activeLogFilter === logFilterPosKey && matchesLogSource(event, sourcePosKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey))
      || (activeLogFilter === logFilterOperatorKey && matchesLogSource(event, sourceOperatorKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey))
      || (activeLogFilter === logFilterSystemKey && matchesLogSource(event, sourcePlatformKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey));
    const sourceMatches = matchesLogSource(event, selectedSource, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey);
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && sourceMatches && searchMatches;
  });
  const visibleEvents = events.length === 0
    ? [logEventPlaceholder(loadStatus, loadError, false, t)]
    : filteredEvents.length > 0
      ? filteredEvents
      : [logEventPlaceholder(loadStatus, loadError, eventSearch.trim().length > 0 || activeLogFilter !== logFilterAllKey || selectedSource !== sourceAllKey, t)];
  const selectedEvent = visibleEvents.find((event) => logEventKey(event) === selectedEventKey) ?? visibleEvents[0];
  const selectedEventDetails = buildLogEventDetailRows(selectedEvent, backend, t);
  const sourceCards: Array<[string, string, LucideIcon]> = [
    [sourceAllKey, t('op.logs.source.all.detail', { count: events.length }), Search],
    [sourceAgentKey, t('op.logs.source.agent.detail', { count: events.filter((event) => matchesLogSource(event, sourceAgentKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey)).length, stale: readNumber(deviceSummary, 'staleDevices', 0) }), MonitorCheck],
    [sourcePosKey, t('op.logs.source.pos.detail', { count: events.filter((event) => matchesLogSource(event, sourcePosKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey)).length }), ReceiptText],
    [sourceOperatorKey, t('op.logs.source.operator.detail', { count: events.filter((event) => matchesLogSource(event, sourceOperatorKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey)).length }), UserRoundPlus],
    [sourcePlatformKey, t('op.logs.source.platform.detail', { count: events.filter((event) => matchesLogSource(event, sourcePlatformKey, sourceAllKey, sourceAgentKey, sourcePosKey, sourceOperatorKey, sourcePlatformKey)).length }), ShieldAlert]
  ];

  const applyAuditSearch = async (label: string, overrides: AuditSearchOverrides = {}) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, overrides));
      setAuditResult(audit);
      setSelectedEventKey('');
      setLoadStatus('backend');
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const applyAuditPeriodPreset = async (label: string, preset: AuditPeriodPreset) => {
    const range = auditPeriodPresetRange(preset);
    setAuditFromUtcFilter(range.fromUtc ?? '');
    setAuditToUtcFilter(range.toUtc ?? '');
    setAuditLimit('50');
    await applyAuditSearch(label, { ...range, limit: 50 });
  };

  const selectLogFilter = (filter: string) => {
    setActiveLogFilter(filter);
    const presets: Record<string, AuditSearchOverrides> = {
      [logFilterAllKey]: { action: '', outcome: '', targetType: '', limit: 30 },
      [logFilterErrorsKey]: { action: '', outcome: 'denied', targetType: '', limit: 50 },
      [logFilterPcKey]: { action: '', outcome: '', targetType: 'Device', limit: 50 },
      [logFilterPosKey]: { action: 'pos.sale.create', outcome: '', targetType: '', limit: 50 },
      [logFilterOperatorKey]: { action: 'identity.staff.create', outcome: '', targetType: '', limit: 50 },
      [logFilterSystemKey]: { action: 'updates.rollouts.view', outcome: '', targetType: '', limit: 50 }
    };
    const preset = presets[filter] ?? {};
    setAuditActionFilter(preset.action ?? '');
    setAuditOutcomeFilter(preset.outcome ?? '');
    setAuditTargetTypeFilter(preset.targetType ?? '');
    setAuditFromUtcFilter(preset.fromUtc ?? '');
    setAuditToUtcFilter(preset.toUtc ?? '');
    setAuditLimit(String(preset.limit ?? 30));
    void applyAuditSearch(filter, preset);
  };

  const runLogAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === supportBundleActionKey) {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: '', targetType: '', fromUtc: '', toUtc: '', limit: 100 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        downloadTextFile(
          `afk4-support-journal-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, [...mapDiagnosticsToLogEvents(diagnostics, sourceAgentKey, sourceUpdatesKey, t), ...mapAuditRecordsToLogEvents(nextAuditRecords, t)], nextBackend, t),
          'application/json;charset=utf-8'
        );
      } else if (label === actionListActionKey) {
        const csv = await apiClients.shifts.exportOperatorActionReportCsv(nextBackend.branchId, { limit: 100 });
        downloadTextFile(`afk4-operator-action-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === onlyProblemsActionKey) {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: 'denied', targetType: '', fromUtc: '', toUtc: '', limit: 50 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        const failureEvents = [...mapDiagnosticsToLogEvents(diagnostics, sourceAgentKey, sourceUpdatesKey, t), ...mapAuditRecordsToLogEvents(nextAuditRecords, t)]
          .filter((event) => event[4] === 'warning');
        downloadTextFile(
          `afk4-support-problems-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, failureEvents, nextBackend, t),
          'application/json;charset=utf-8'
        );
      } else {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-summary-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      }
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen logs-screen">
      <section className="screen-head logs-head">
        <div>
          <span>{t('op.logs.head.title')}</span>
          <h1>{t('op.logs.head.subtitle')}</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.logs.loadedLabel'), t)}</span>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label={t('op.logs.strip.aria')}>
        <StateFlag label={t('op.logs.strip.events')} value={String(events.length)} />
        <StateFlag label={t('op.logs.strip.errors')} value={String(events.filter((event) => event[4] === 'warning').length)} critical={events.some((event) => event[4] === 'warning')} />
        <StateFlag label={t('op.logs.strip.commands')} value={String(readNumber(commandSummary, 'pendingCommands', 0))} />
        <StateFlag label={t('op.logs.strip.records')} value={String(auditRecords.length)} />
        <StateFlag label={t('op.logs.strip.source')} value={workspaceLoadStatusLabel(loadStatus, sourcePlatformKey, t)} critical={loadStatus !== 'backend'} />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.events.title')}</span>
            <strong>{t('op.logs.events.subtitle')}</strong>
          </header>
          <label className="logs-search">
            <Search size={14} />
            <input
              placeholder={t('op.logs.events.searchPlaceholder')}
              value={eventSearch}
              onChange={(event) => setEventSearch(event.currentTarget.value)}
            />
          </label>
          <div className="logs-event-list">
            {visibleEvents.map((event) => {
              const [time, title, detail, source, tone] = event;
              const eventKey = logEventKey(event);
              return (
                <button
                  key={eventKey}
                  type="button"
                  className={`log-event-row ${tone}${eventKey === selectedEventKey ? ' active' : ''}`}
                  onClick={() => setSelectedEventKey(eventKey)}
                >
                  <span>{time}</span>
                  <div>
                    <strong>{title}</strong>
                    <em>{detail}</em>
                  </div>
                  <b>{source}</b>
                </button>
              );
            })}
          </div>
        </section>

        <section className="logs-panel logs-detail-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.detail.title')}</span>
            <strong>{t('op.logs.detail.subtitle')}</strong>
          </header>
          <div className={`log-detail-card ${selectedEvent[4]}`}>
            <span>{selectedEvent[0]} · {selectedEvent[3]}</span>
            <strong>{selectedEvent[1]}</strong>
            <em>{selectedEvent[2]}</em>
          </div>
          <div className="log-detail-list">
            {selectedEventDetails.map(([label, value]) => (
              <div key={label}><span>{label}</span><strong>{value}</strong></div>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="logs-panel logs-filter-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.filter.title')}</span>
            <strong>{t('op.logs.filter.subtitle')}</strong>
          </header>
          <div className="logs-filter-grid">
            {[logFilterAllKey, logFilterErrorsKey, logFilterPcKey, logFilterPosKey, logFilterOperatorKey, logFilterSystemKey].map((filter) => (
              <button
                key={filter}
                type="button"
                className={activeLogFilter === filter ? 'active' : undefined}
                onClick={() => selectLogFilter(filter)}
              >
                {filter}
              </button>
            ))}
          </div>
          <div className="logs-audit-filter-form">
            <div className="logs-period-presets" aria-label={t('op.logs.filter.periodAria')}>
              {auditPeriodPresetOptions.map(([label, preset]) => (
                <button key={preset} type="button" onClick={() => void applyAuditPeriodPreset(label, preset)}>{label}</button>
              ))}
            </div>
            <label>{t('op.logs.filter.fieldAction')}<input value={auditActionFilter} onChange={(event) => setAuditActionFilter(event.currentTarget.value)} placeholder={t('op.logs.filter.phAction')} /></label>
            <label>{t('op.logs.filter.fieldOutcome')}<input value={auditOutcomeFilter} onChange={(event) => setAuditOutcomeFilter(event.currentTarget.value)} placeholder={t('op.logs.filter.phOutcome')} /></label>
            <label>{t('op.logs.filter.fieldSection')}<input value={auditTargetTypeFilter} onChange={(event) => setAuditTargetTypeFilter(event.currentTarget.value)} placeholder={t('op.logs.filter.phSection')} /></label>
            <label>{t('op.logs.filter.fieldFrom')}<input value={auditFromUtcFilter} onChange={(event) => setAuditFromUtcFilter(event.currentTarget.value)} placeholder={t('op.logs.filter.phFrom')} /></label>
            <label>{t('op.logs.filter.fieldTo')}<input value={auditToUtcFilter} onChange={(event) => setAuditToUtcFilter(event.currentTarget.value)} placeholder={t('op.logs.filter.phTo')} /></label>
            <label>{t('op.logs.filter.fieldLimit')}<input inputMode="numeric" value={auditLimit} onChange={(event) => setAuditLimit(event.currentTarget.value)} /></label>
            <button type="button" onClick={() => applyAuditSearch(applyFilterActionKey)}>{applyFilterActionKey}</button>
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.audit.title')}</span>
            <strong>{t('op.logs.audit.subtitle')}</strong>
          </header>
          <div className="logs-audit-list">
            {auditRecords.slice(0, 4).map((record) => (
              <article key={readString(record, 'auditRecordId')} className="log-audit-row">
                <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                <strong>{auditActorLabel(record, backend, t)}</strong>
                <em>{auditActionLabel(readString(record, 'action'), t)}</em>
                <b>{auditOutcomeLabel(readString(record, 'outcome'), t)}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.sources.title')}</span>
            <strong>{t('op.logs.sources.subtitle')}</strong>
          </header>
          <div className="logs-source-grid">
            {sourceCards.map(([label, detail, Icon]) => (
              <button
                key={label}
                type="button"
                className={`log-source-card${selectedSource === label ? ' active' : ''}`}
                onClick={() => setSelectedSource(label)}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-export-panel">
          <header className="logs-panel-title">
            <span>{t('op.logs.export.title')}</span>
            <strong>{t('op.logs.export.subtitle')}</strong>
          </header>
          <div className="logs-export-grid">
            {[
              [shiftSummaryActionKey, ReceiptText],
              [onlyProblemsActionKey, AlertTriangle],
              [actionListActionKey, ArrowRightLeft],
              [supportBundleActionKey, ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runLogAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}
