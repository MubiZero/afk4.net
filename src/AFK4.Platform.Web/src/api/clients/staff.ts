import type { ApiTransport } from '../apiTransport';
import type {
  CreateStaffInviteRequest,
  ResetStaffUserPasswordRequest,
  StaffInviteDto,
  StaffUser,
  UpdateStaffUserProfileRequest,
  UpdateStaffUserRolesRequest,
  UpdateStaffUserStateRequest
} from '../types';

export class StaffApi {
  public constructor(private readonly transport: ApiTransport) {}

  public listStaff(branchId: string): Promise<StaffUser[]> {
    return this.transport.send<StaffUser[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/staff`);
  }

  public createStaffInvite(branchId: string, request: CreateStaffInviteRequest): Promise<StaffInviteDto> {
    return this.transport.send<StaffInviteDto>('POST', `/api/branches/${encodeURIComponent(branchId)}/staff/invites`, request);
  }

  public updateStaffRoles(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserRolesRequest
  ): Promise<StaffUser> {
    return this.transport.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/roles`,
      request
    );
  }

  public updateStaffProfile(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserProfileRequest
  ): Promise<StaffUser> {
    return this.transport.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/profile`,
      request
    );
  }

  public updateStaffState(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserStateRequest
  ): Promise<StaffUser> {
    return this.transport.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/state`,
      request
    );
  }

  public resetStaffPassword(
    branchId: string,
    staffUserId: string,
    request: ResetStaffUserPasswordRequest
  ): Promise<StaffUser> {
    return this.transport.send<StaffUser>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/password-reset`,
      request
    );
  }
}
