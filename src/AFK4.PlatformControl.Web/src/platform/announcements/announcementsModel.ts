import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';
import type { PlatformAnnouncement } from '@/api/types';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

export const SEVERITIES = ['info', 'warning', 'critical'] as const;
export const AUDIENCE_KINDS = ['all', 'plans', 'organizations'] as const;

export type Severity = (typeof SEVERITIES)[number];
export type AudienceKind = (typeof AUDIENCE_KINDS)[number];

// Каждый машинный код отказа получает свою фразу. Общее «не удалось сохранить» на все случаи —
// это потеря конкретики, которая уже пришла с сервера.
const ERROR_CODE_KEY: Record<string, MessageKey> = {
  invalid_announcement: 'platform.announcements.error.invalid',
  unknown_plan: 'platform.announcements.error.unknownPlan',
  unknown_organization: 'platform.announcements.error.unknownOrganization',
  published_audience_locked: 'platform.announcements.error.audienceLocked',
  withdrawn_announcement: 'platform.announcements.error.withdrawn',
  invalid_transition: 'platform.announcements.error.invalidTransition',
  conflict: 'platform.announcements.error.conflict'
};

export function describeAnnouncementError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = ERROR_CODE_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}

export function severityLabelKey(severity: string): MessageKey {
  if (severity === 'critical') return 'platform.announcements.severity.critical';
  if (severity === 'warning') return 'platform.announcements.severity.warning';
  return 'platform.announcements.severity.info';
}

export function statusLabelKey(status: string): MessageKey {
  if (status === 'published') return 'platform.announcements.status.published';
  if (status === 'withdrawn') return 'platform.announcements.status.withdrawn';
  return 'platform.announcements.status.draft';
}

export function audienceLabelKey(audienceKind: string): MessageKey {
  if (audienceKind === 'plans') return 'platform.announcements.audience.plans';
  if (audienceKind === 'organizations') return 'platform.announcements.audience.organizations';
  return 'platform.announcements.audience.all';
}

/**
 * Важность и аудитория опубликованного анонса не редактируются: письма уже ушли по прежнему
 * списку, и сервер такую правку отклонит. Показывать поля активными — обещать то, чего не будет.
 */
export function audienceIsLocked(announcement: PlatformAnnouncement | null): boolean {
  return announcement !== null && announcement.status === 'published';
}

/** Значение для `<input type="datetime-local">` из ISO-строки, в местном времени читающего. */
export function toLocalInput(iso: string): string {
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function fromLocalInput(localValue: string): string | null {
  if (localValue === '') return null;
  const date = new Date(localValue);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
