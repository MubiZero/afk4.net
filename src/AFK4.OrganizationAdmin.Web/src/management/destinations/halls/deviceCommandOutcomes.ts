import type { MessageKey } from '@afk4/i18n';

// Чем закончилась команда на ПК. Агент присылает машинное имя исхода (DeviceCommandOutcomeNames),
// а словами о нём говорит уже клуб — на своём языке. Раньше приезжала готовая английская фраза
// вроде «Workstation locked (nothing)», и администратор читал её как есть.
const OUTCOME_LABELS: Record<string, MessageKey> = {
  accepted: 'op.helper.cmdOutcome.accepted',
  'lease-accepted': 'op.helper.cmdOutcome.leaseAccepted',
  'lease-refreshed': 'op.helper.cmdOutcome.leaseRefreshed',
  'workstation-locked': 'op.helper.cmdOutcome.workstationLocked',
  'machine-policies-unavailable': 'op.helper.cmdOutcome.machinePoliciesUnavailable',
  'warning-shown': 'op.helper.cmdOutcome.warningShown',
  'warning-reason-unknown': 'op.helper.cmdOutcome.warningReasonUnknown',
  'command-not-implemented': 'op.helper.cmdOutcome.commandNotImplemented',
  'command-execution-failed': 'op.helper.cmdOutcome.commandExecutionFailed',
  'lease-missing': 'op.helper.cmdOutcome.leaseMissing',
  'lease-unreadable': 'op.helper.cmdOutcome.leaseUnreadable',
  'lease-invalid': 'op.helper.cmdOutcome.leaseInvalid'
};

export function commandOutcomeLabelKey(outcome: string | null | undefined): MessageKey | null {
  if (!outcome) return null;
  return OUTCOME_LABELS[outcome] ?? null;
}
