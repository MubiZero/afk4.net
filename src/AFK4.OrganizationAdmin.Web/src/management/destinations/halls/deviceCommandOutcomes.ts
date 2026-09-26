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
  'lease-invalid': 'op.helper.cmdOutcome.leaseInvalid',
  'reboot-scheduled': 'op.helper.cmdOutcome.rebootScheduled',
  'shutdown-scheduled': 'op.helper.cmdOutcome.shutdownScheduled',
  'session-in-progress': 'op.helper.cmdOutcome.sessionInProgress',
  'wake-packet-sent': 'op.helper.cmdOutcome.wakePacketSent',
  'wake-target-invalid': 'op.helper.cmdOutcome.wakeTargetInvalid',
  'maintenance-started': 'op.helper.cmdOutcome.maintenanceStarted',
  'maintenance-ended': 'op.helper.cmdOutcome.maintenanceEnded',
  'delivered-to-shell': 'op.helper.cmdOutcome.deliveredToShell',
  'shell-not-connected': 'op.helper.cmdOutcome.shellNotConnected',
  'nothing-to-refresh': 'op.helper.cmdOutcome.nothingToRefresh',
  'protection-applied': 'op.helper.cmdOutcome.protectionApplied',
  'protection-unavailable': 'op.helper.cmdOutcome.protectionUnavailable'
};

export function commandOutcomeLabelKey(outcome: string | null | undefined): MessageKey | null {
  if (!outcome) return null;
  return OUTCOME_LABELS[outcome] ?? null;
}
