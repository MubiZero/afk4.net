import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, MonitorCheck, ReceiptText, Search, ShieldAlert, UserRoundPlus } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
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
  pluralRu,
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

function logEventPlaceholder(loadStatus: LoadStatus, loadError: string | null, hasSearchMiss: boolean): LogEventItem {
  if (hasSearchMiss) {
    return ['—', 'Нет совпадений', 'измените поиск или фильтр', 'Оператор', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем события', 'ждём аудит и диагностику', 'Платформа', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'События недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'Платформа', 'warning', 'placeholder', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Событий за период нет', 'аудит и диагностика вернули пустой результат', 'Платформа', 'audit', 'placeholder', null];
  }

  return ['—', 'Локально: событий нет', 'локальные данные без платформы', 'Платформа', 'audit', 'placeholder', null];
}

function mapAuditRecordsToLogEvents(auditRecords: Record<string, unknown>[]): LogEventItem[] {
  return auditRecords.map((record): LogEventItem => {
    const outcome = readString(record, 'outcome');

    return [
      formatTime(readString(record, 'createdAtUtc')),
      auditActionLabel(readString(record, 'action')),
      `${auditTargetLabel(readString(record, 'targetType'))} · ${auditOutcomeLabel(outcome)}`,
      auditSourceLabel(readString(record, 'sourceApp')),
      outcome.toLowerCase().includes('denied') || outcome.toLowerCase().includes('failed') ? 'warning' : 'audit',
      'audit',
      record
    ];
  });
}

function auditOutcomeLabel(outcome: string): string {
  switch (outcome.toLowerCase()) {
    case 'succeeded':
    case 'success':
    case 'ok':
      return 'успешно';
    case 'failed':
    case 'failure':
      return 'ошибка';
    case 'denied':
    case 'rejected':
      return 'отказ';
    case 'pending':
      return 'ожидает';
    default:
      return outcome ? 'состояние неизвестно' : 'неизвестно';
  }
}

function auditTargetLabel(targetType: string): string {
  const normalized = targetType.toLowerCase();
  if (normalized.includes('pos') || normalized.includes('sale') || normalized.includes('receipt')) {
    return 'Чек';
  }

  if (normalized.includes('session')) {
    return 'Сессия';
  }

  if (normalized.includes('device')) {
    return 'ПК';
  }

  if (normalized.includes('staff') || normalized.includes('identity') || normalized.includes('user')) {
    return 'Сотрудник';
  }

  if (normalized.includes('shift')) {
    return 'Смена';
  }

  if (normalized.includes('payment') || normalized.includes('ledger') || normalized.includes('wallet')) {
    return 'Платёж';
  }

  if (normalized.includes('tariff')) {
    return 'Тариф';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('update') || normalized.includes('rollout')) {
    return 'Обновление';
  }

  if (normalized.includes('branch')) {
    return 'Филиал';
  }

  return targetType ? 'Объект' : 'Объект';
}

function auditSourceLabel(sourceApp: string): string {
  const normalized = sourceApp.toLowerCase();
  if (!normalized || normalized === 'audit' || normalized.includes('platform')) {
    return 'Платформа';
  }

  if (normalized.includes('operator')) {
    return 'Приложение оператора';
  }

  if (normalized.includes('agent')) {
    return 'Агент';
  }

  if (normalized.includes('setup')) {
    return 'Мастер установки';
  }

  return 'Платформа';
}

function auditDetailKeyLabel(key: string): string {
  const normalized = key.toLowerCase();
  if (normalized.includes('reason')) {
    return 'Причина';
  }

  if (normalized.includes('amount') || normalized.includes('money') || normalized.includes('price')) {
    return 'Сумма';
  }

  if (normalized.includes('currency')) {
    return 'Валюта';
  }

  if (normalized.includes('status') || normalized.includes('state') || normalized.includes('outcome')) {
    return 'Состояние';
  }

  if (normalized.includes('device') || normalized.includes('machine')) {
    return 'ПК';
  }

  if (normalized.includes('session')) {
    return 'Сессия';
  }

  if (normalized.includes('sale') || normalized.includes('receipt') || normalized.includes('pos')) {
    return 'Чек';
  }

  if (normalized.includes('shift')) {
    return 'Смена';
  }

  if (normalized.includes('tariff')) {
    return 'Тариф';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('staff') || normalized.includes('user')) {
    return 'Сотрудник';
  }

  if (normalized.includes('branch')) {
    return 'Филиал';
  }

  return 'Параметр';
}

function auditDetailValueLabel(value: unknown): string {
  if (value === null || value === undefined) {
    return 'не указано';
  }

  if (typeof value === 'boolean') {
    return value ? 'да' : 'нет';
  }

  if (typeof value === 'number') {
    return String(value);
  }

  if (Array.isArray(value)) {
    return `${value.length} ${pluralRu(value.length, ['значение', 'значения', 'значений'])}`;
  }

  if (isRecord(value)) {
    return 'заполнено';
  }

  const trimmed = String(value).trim();
  if (trimmed.length === 0) {
    return 'не указано';
  }

  if (isGuid(trimmed)) {
    return 'указано';
  }

  if (/^https?:\/\//i.test(trimmed)) {
    return 'ссылка';
  }

  if (/^\d{4}-\d{2}-\d{2}T/.test(trimmed)) {
    return formatTime(trimmed);
  }

  return trimmed.length > 80 ? `${trimmed.slice(0, 80)}…` : trimmed;
}

function mapDiagnosticsToLogEvents(diagnostics: BranchDiagnosticsDto | null): LogEventItem[] {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const recentCommandFailures = readArray<Record<string, unknown>>(commandSummary, 'recentFailures');
  const recentUpdateFailures = readArray<Record<string, unknown>>(updateSummary, 'recentFailures');
  const staleDevices = readArray<Record<string, unknown>>(diagnostics, 'staleDevices');

  return [
    ...recentCommandFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'machineName', 'Устройство')} ${commandTypeLabel(readString(failure, 'type', 'command'))}`,
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      'Агент',
      'device',
      'commandFailure',
      failure
    ]),
    ...recentUpdateFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${updateComponentLabel(readString(failure, 'component', 'Обновление'))} ${readString(failure, 'targetVersion', '')}`.trim(),
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      'Обновления',
      'warning',
      'updateFailure',
      failure
    ]),
    ...staleDevices.map((device): LogEventItem => [
      formatTime(readString(device, 'lastHeartbeatAtUtc')),
      `${readString(device, 'machineName', 'Устройство')} не отвечает`,
      `${readNumber(device, 'lastHeartbeatAgeSeconds', 0)} сек. без сигнала`,
      'Агент',
      'warning',
      'staleDevice',
      device
    ])
  ];
}

function compactAuditDetails(detailsJson: string): string {
  const trimmed = detailsJson.trim();
  if (trimmed.length === 0 || trimmed === '{}') {
    return 'нет подробностей';
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (!isRecord(parsed)) {
      return auditDetailValueLabel(parsed);
    }

    const entries = Object.entries(parsed)
      .filter(([, value]) => value !== null && value !== undefined && String(value).trim().length > 0)
      .slice(0, 3)
      .map(([key, value]) => `${auditDetailKeyLabel(key)}: ${auditDetailValueLabel(value)}`);

    return entries.length > 0 ? entries.join(' · ') : 'нет подробностей';
  } catch {
    return 'подробности в свободном формате';
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

function buildLogEventDetailRows(event: LogEventItem, backend: OperatorBackendContext | null): Array<[string, string]> {
  const record = event[6];

  if (event[5] === 'audit' && record !== null) {
    const targetType = readString(record, 'targetType');

    return [
      ['Событие', auditActionLabel(readString(record, 'action'))],
      ['Результат', auditOutcomeLabel(readString(record, 'outcome'))],
      ['Раздел', auditTargetLabel(targetType)],
      ['Исполнитель', auditActorLabel(record, backend)],
      ['Источник', auditSourceLabel(readString(record, 'sourceApp'))],
      ['Подробности', compactAuditDetails(readString(record, 'detailsJson'))]
    ];
  }

  if (event[5] === 'commandFailure' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Команда', commandTypeLabel(readString(record, 'type', 'command'))],
      ['Статус', commandStatusLabel(readString(record, 'status', 'failed'))],
      ['Сообщение', commandStatusMessageLabel(readString(record, 'message')) || 'нет сообщения'],
      ['Обновлено', readString(record, 'updatedAtUtc', event[0])]
    ];
  }

  if (event[5] === 'updateFailure' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Компонент', `${updateComponentLabel(readString(record, 'component', 'Обновление'))} ${readString(record, 'targetVersion')}`.trim()],
      ['Статус', commandStatusLabel(readString(record, 'status', 'failed'))],
      ['Сообщение', commandStatusMessageLabel(readString(record, 'message')) || 'нет сообщения']
    ];
  }

  if (event[5] === 'staleDevice' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Версия агента', readString(record, 'agentVersion', 'неизвестно')],
      ['Версия оболочки', readString(record, 'shellVersion', 'неизвестно')],
      ['Последний сигнал', readString(record, 'lastHeartbeatAtUtc', event[0])],
      ['Пауза связи', `${readNumber(record, 'lastHeartbeatAgeSeconds', 0)} сек.`]
    ];
  }

  return [
    ['Источник', event[3]],
    ['Событие', event[1]],
    ['Оператор', operatorDisplayNameLabel(backend?.session.displayName ?? 'система')],
    ['Результат', event[2]]
  ];
}

function logToneLabel(tone: LogEventTone): string {
  switch (tone) {
    case 'warning':
      return 'требует внимания';
    case 'device':
      return 'ПК и связь';
    case 'money':
      return 'касса';
    case 'session':
      return 'сессия';
    default:
      return 'запись';
  }
}

function logKindLabel(kind: LogEventKind): string {
  switch (kind) {
    case 'commandFailure':
      return 'команда ПК';
    case 'updateFailure':
      return 'обновление';
    case 'staleDevice':
      return 'связь с ПК';
    case 'placeholder':
      return 'пустой результат';
    default:
      return 'аудит';
  }
}

function buildLogsExportJson(
  branchId: string,
  auditRecords: Record<string, unknown>[],
  diagnostics: BranchDiagnosticsDto | null,
  events: LogEventItem[],
  backend: OperatorBackendContext | null
): string {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  return JSON.stringify({
    exportedAtUtc: new Date().toISOString(),
    branch: branchId ? 'текущий филиал' : 'филиал не выбран',
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
      result: logToneLabel(tone),
      section: logKindLabel(kind),
      details: Object.fromEntries(buildLogEventDetailRows([time, title, detail, source, tone, kind, record], backend))
    }))
  }, null, 2);
}

function matchesLogSource(event: LogEventItem, sourceFilter: string): boolean {
  if (sourceFilter === 'Все') {
    return true;
  }

  const [, title, detail, source, tone, kind, record] = event;
  const normalizedTitle = title.toLowerCase();
  const normalizedDetail = detail.toLowerCase();
  const normalizedSource = source.toLowerCase();
  const action = readString(record, 'action').toLowerCase();
  const targetType = readString(record, 'targetType').toLowerCase();
  const isAgent = source === 'Агент' || tone === 'device' || kind === 'commandFailure' || kind === 'staleDevice';
  const isPos = normalizedTitle.includes('касс') || normalizedTitle.includes('чек') || normalizedDetail.includes('касс') || normalizedDetail.includes('чек') || action.includes('pos') || targetType.includes('pos');
  const isOperator = normalizedSource.includes('operator') || normalizedSource.includes('оператор') || normalizedTitle.includes('identity') || normalizedDetail.includes('staff') || action.includes('identity') || targetType.includes('staff');
  const isPlatform = source === 'Обновления' || kind === 'updateFailure' || (!isAgent && !isPos && !isOperator);

  if (sourceFilter === 'Агент') {
    return isAgent;
  }

  if (sourceFilter === 'Касса') {
    return isPos;
  }

  if (sourceFilter === 'Оператор') {
    return isOperator;
  }

  if (sourceFilter === 'Платформа') {
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

const auditPeriodPresetOptions: Array<[string, AuditPeriodPreset]> = [
  ['Сегодня', 'today'],
  ['24 часа', 'last24h'],
  ['7 дней', 'last7d']
];

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
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('');
  const [selectedSource, setSelectedSource] = useState('Все');
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
      setFeedback({ label: 'Логи', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadLogs();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const auditEvents = mapAuditRecordsToLogEvents(auditRecords);
  const diagnosticEvents = mapDiagnosticsToLogEvents(diagnostics);
  const events = [...diagnosticEvents, ...auditEvents];
  const filteredEvents = events.filter((event) => {
    const [time, title, detail, source, tone] = event;
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и связь' && matchesLogSource(event, 'Агент'))
      || (activeLogFilter === 'Касса' && matchesLogSource(event, 'Касса'))
      || (activeLogFilter === 'Оператор' && matchesLogSource(event, 'Оператор'))
      || (activeLogFilter === 'Системные' && matchesLogSource(event, 'Платформа'));
    const sourceMatches = matchesLogSource(event, selectedSource);
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && sourceMatches && searchMatches;
  });
  const visibleEvents = events.length === 0
    ? [logEventPlaceholder(loadStatus, loadError, false)]
    : filteredEvents.length > 0
      ? filteredEvents
      : [logEventPlaceholder(loadStatus, loadError, eventSearch.trim().length > 0 || activeLogFilter !== 'Все события' || selectedSource !== 'Все')];
  const selectedEvent = visibleEvents.find((event) => logEventKey(event) === selectedEventKey) ?? visibleEvents[0];
  const selectedEventDetails = buildLogEventDetailRows(selectedEvent, backend);
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Все', `${events.length} событий`, Search],
    ['Агент', `${events.filter((event) => matchesLogSource(event, 'Агент')).length} событий · зависших ${readNumber(deviceSummary, 'staleDevices', 0)}`, MonitorCheck],
    ['Касса', `${events.filter((event) => matchesLogSource(event, 'Касса')).length} записей`, ReceiptText],
    ['Оператор', `${events.filter((event) => matchesLogSource(event, 'Оператор')).length} действий`, UserRoundPlus],
    ['Платформа', `${events.filter((event) => matchesLogSource(event, 'Платформа')).length} событий`, ShieldAlert]
  ];

  const applyAuditSearch = async (label: string, overrides: AuditSearchOverrides = {}) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
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
      'Все события': { action: '', outcome: '', targetType: '', limit: 30 },
      'Только ошибки': { action: '', outcome: 'denied', targetType: '', limit: 50 },
      'ПК и связь': { action: '', outcome: '', targetType: 'Device', limit: 50 },
      'Касса': { action: 'pos.sale.create', outcome: '', targetType: '', limit: 50 },
      'Оператор': { action: 'identity.staff.create', outcome: '', targetType: '', limit: 50 },
      'Системные': { action: 'updates.rollouts.view', outcome: '', targetType: '', limit: 50 }
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
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === 'Пакет для поддержки') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: '', targetType: '', fromUtc: '', toUtc: '', limit: 100 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        downloadTextFile(
          `afk4-support-journal-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)], nextBackend),
          'application/json;charset=utf-8'
        );
      } else if (label === 'Список действий') {
        const csv = await apiClients.shifts.exportOperatorActionReportCsv(nextBackend.branchId, { limit: 100 });
        downloadTextFile(`afk4-operator-action-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Только проблемы') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: 'denied', targetType: '', fromUtc: '', toUtc: '', limit: 50 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        const failureEvents = [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)]
          .filter((event) => event[4] === 'warning');
        downloadTextFile(
          `afk4-support-problems-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, failureEvents, nextBackend),
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
          <span>Журнал</span>
          <h1>Журнал · события смены</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Журнал загружен')}</span>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка журнала">
        <StateFlag label="События" value={String(events.length)} />
        <StateFlag label="Ошибки" value={String(events.filter((event) => event[4] === 'warning').length)} critical={events.some((event) => event[4] === 'warning')} />
        <StateFlag label="Команды ПК" value={String(readNumber(commandSummary, 'pendingCommands', 0))} />
        <StateFlag label="Записи" value={String(auditRecords.length)} />
        <StateFlag label="Источник" value={workspaceLoadStatusLabel(loadStatus, 'Платформа')} critical={loadStatus !== 'backend'} />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>Журнал событий</span>
            <strong>события платформы и ПК</strong>
          </header>
          <label className="logs-search">
            <Search size={14} />
            <input
              placeholder="ПК, клиент, оператор, событие"
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
            <span>Детали события</span>
            <strong>без внутренних ID</strong>
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
            <span>Фильтры</span>
            <strong>найти нужные записи</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и связь', 'Касса', 'Оператор', 'Системные'].map((filter) => (
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
            <div className="logs-period-presets" aria-label="Период аудита">
              {auditPeriodPresetOptions.map(([label, preset]) => (
                <button key={preset} type="button" onClick={() => void applyAuditPeriodPreset(label, preset)}>{label}</button>
              ))}
            </div>
            <label>Событие<input value={auditActionFilter} onChange={(event) => setAuditActionFilter(event.currentTarget.value)} placeholder="продажа / сессия / ПК" /></label>
            <label>Результат<input value={auditOutcomeFilter} onChange={(event) => setAuditOutcomeFilter(event.currentTarget.value)} placeholder="успешно / отказ" /></label>
            <label>Раздел<input value={auditTargetTypeFilter} onChange={(event) => setAuditTargetTypeFilter(event.currentTarget.value)} placeholder="сессии / касса / ПК" /></label>
            <label>С<input value={auditFromUtcFilter} onChange={(event) => setAuditFromUtcFilter(event.currentTarget.value)} placeholder="2026-05-21 00:00" /></label>
            <label>До<input value={auditToUtcFilter} onChange={(event) => setAuditToUtcFilter(event.currentTarget.value)} placeholder="2026-05-21 23:59" /></label>
            <label>Записей<input inputMode="numeric" value={auditLimit} onChange={(event) => setAuditLimit(event.currentTarget.value)} /></label>
            <button type="button" onClick={() => applyAuditSearch('Применить фильтр')}>Применить фильтр</button>
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>Операции смены</span>
            <strong>последние действия</strong>
          </header>
          <div className="logs-audit-list">
            {auditRecords.slice(0, 4).map((record) => (
              <article key={readString(record, 'auditRecordId')} className="log-audit-row">
                <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                <strong>{auditActorLabel(record, backend)}</strong>
                <em>{auditActionLabel(readString(record, 'action'))}</em>
                <b>{auditOutcomeLabel(readString(record, 'outcome'))}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>Источники</span>
            <strong>каналы событий</strong>
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
            <span>Экспорт</span>
            <strong>для проверки и поддержки</strong>
          </header>
          <div className="logs-export-grid">
            {[
              ['Сводка смены', ReceiptText],
              ['Только проблемы', AlertTriangle],
              ['Список действий', ArrowRightLeft],
              ['Пакет для поддержки', ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runLogAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}
