import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';

export interface OwnerCodeView {
  code: string;
  hasCode: boolean;
  expiresAtUtc: string | null;
  lastUsedAtUtc: string | null;
  failedAttemptCount: number;
}

export function toOwnerCodeView(summary: OwnerCodeSummary | null, issued: OwnerCodeIssued | null): OwnerCodeView {
  if (issued !== null) {
    return { code: issued.ownerCode, hasCode: true, expiresAtUtc: issued.expiresAtUtc, lastUsedAtUtc: null, failedAttemptCount: 0 };
  }
  if (summary === null) {
    return { code: '—', hasCode: false, expiresAtUtc: null, lastUsedAtUtc: null, failedAttemptCount: 0 };
  }
  return {
    code: `**** ${summary.codeSuffix}`,
    hasCode: true,
    expiresAtUtc: summary.expiresAtUtc,
    lastUsedAtUtc: summary.lastUsedAtUtc,
    failedAttemptCount: summary.failedAttemptCount
  };
}

export function getSetupMsiUrl(): string {
  const configured = import.meta.env.VITE_SETUP_MSI_URL;
  return typeof configured === 'string' && configured.trim().length > 0
    ? configured.trim()
    : '/downloads/AFK4-Agent.msi';
}
