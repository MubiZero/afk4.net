// src/club/settings/settingsModel.ts
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

export interface OperatorRow {
  staffUserId: string;
  organizationId: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  roleNames: string[];
}

export interface BranchProfileView {
  branchId: string;
  organizationId: string;
  name: string;
  city: string;
}

export interface SettingsViewModel {
  profile: BranchProfileView;
  requireManualDeviceApproval: boolean;
  operators: OperatorRow[];
}

export function buildSettings(
  profile: BranchProfile,
  settings: BranchSettings,
  staff: StaffUser[]
): SettingsViewModel {
  return {
    profile: {
      branchId: profile.branchId,
      organizationId: profile.organizationId,
      name: profile.name,
      city: profile.city
    },
    requireManualDeviceApproval: settings.requireManualDeviceApproval,
    operators: staff
      .map(u => ({
        staffUserId: u.staffUserId,
        organizationId: u.organizationId,
        userName: u.userName,
        displayName: u.displayName,
        isActive: u.isActive,
        roleNames: u.roleNames
      }))
      .sort((a, b) => a.displayName.localeCompare(b.displayName))
  };
}
