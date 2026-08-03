import type { MessageKey } from '@/i18n/messages';
import type { BadgeVariant } from '@/components/ui/badge';

export const STATUS_VARIANT: Record<string, BadgeVariant> = {
  active: 'success',
  suspended: 'destructive',
  deletion_pending: 'outline'
};
export const STATUS_LABEL: Record<string, MessageKey> = {
  active: 'platform.organization.status.active',
  suspended: 'platform.organization.status.suspended',
  deletion_pending: 'platform.organization.status.deletionPending'
};

export const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export const INVITE_STATUS_VARIANT: Record<string, BadgeVariant> = {
  pending: 'secondary',
  accepted: 'success',
  revoked: 'outline',
  expired: 'outline'
};
export const INVITE_STATUS_LABEL: Record<string, MessageKey> = {
  pending: 'platform.organization.invites.status.pending',
  accepted: 'platform.organization.invites.status.accepted',
  revoked: 'platform.organization.invites.status.revoked',
  expired: 'platform.organization.invites.status.expired'
};

export const STATUS_OPTIONS = ['active', 'suspended', 'deletion_pending'] as const;
