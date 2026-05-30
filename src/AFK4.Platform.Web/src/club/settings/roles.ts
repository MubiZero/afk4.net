// src/club/settings/roles.ts
import type { MessageKey } from '@/i18n/messages';

/**
 * Branch-assignable staff roles. `owner` is provisioned via the owner-invite flow
 * and is intentionally NOT assignable from this screen (mirrors the backend's
 * IsAssignableBranchStaffRole allow-list).
 */
export const ASSIGNABLE_ROLES = [
  'branch_manager',
  'shift_supervisor',
  'cashier_operator',
  'technician',
  'accountant_auditor'
] as const;

export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number];

const ROLE_LABEL_KEY: Record<string, MessageKey> = {
  owner: 'roles.owner',
  branch_manager: 'roles.branch_manager',
  shift_supervisor: 'roles.shift_supervisor',
  cashier_operator: 'roles.cashier_operator',
  technician: 'roles.technician',
  accountant_auditor: 'roles.accountant_auditor'
};

export function roleLabelKey(roleName: string): MessageKey {
  return ROLE_LABEL_KEY[roleName] ?? 'roles.unknown';
}
