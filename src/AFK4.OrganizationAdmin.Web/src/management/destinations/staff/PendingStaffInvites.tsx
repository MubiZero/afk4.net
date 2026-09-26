import { useEffect, useState } from 'react';
import { KeyRound, Trash2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { StaffInviteStatusNames, type StaffInviteSummaryDto } from '@afk4/contracts';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, staffRoleLabel } from '../../../operatorHelpers';
import type { Feedback } from '../../../operatorTypes';
import type { DestinationProps } from '../types';
import { groupCode } from './firstSignInCode';

/**
 * «Ждут первого входа» — добавленные сотрудники, которые ещё не входили. Без этого списка
 * выданный код нельзя было ни увидеть, ни отозвать: руководитель добавлял человека заново и
 * надеялся, что старый код больше никого не пустит.
 */
export function PendingStaffInvites({ backend, refreshKey, onFeedback }: {
  backend: NonNullable<DestinationProps['backend']>;
  /// Растёт после добавления сотрудника — список перечитывается.
  refreshKey: number;
  onFeedback?: (feedback: Feedback) => void;
}) {
  const { t, formatDate } = useI18n();
  const [invites, setInvites] = useState<StaffInviteSummaryDto[] | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [freshCodes, setFreshCodes] = useState<Record<string, string>>({});
  const [reload, setReload] = useState(0);

  const clients = () => createAuthenticatedOperatorClients(backend.config, backend.session).settings;

  useEffect(() => {
    let alive = true;
    // Список вспомогательный: не загрузился — блока просто нет, экран сотрудников живёт дальше.
    Promise.resolve()
      .then(() => clients().listStaffInvites(backend.branchId))
      .then((list) => { if (alive) setInvites(list); })
      .catch(() => { if (alive) setInvites([]); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend.branchId, backend.session.accessToken, refreshKey, reload]);

  if (invites === null || invites.length === 0) return null;

  // Новый код — то же добавление с теми же данными: сервер гасит старый код этого номера сам.
  const reissue = async (invite: StaffInviteSummaryDto) => {
    const label = t('op.management.staff.pending.reissue');
    setBusyId(invite.staffInviteId);
    onFeedback?.({ label, state: 'pending' });
    try {
      const created = await clients().createStaffInvite(backend.branchId, {
        organizationId: backend.session.organizationId,
        userName: invite.userName,
        displayName: invite.displayName,
        phoneNumber: invite.phoneNumber,
        email: invite.email,
        roleNames: [...invite.roleNames]
      });
      setFreshCodes((codes) => ({ ...codes, [created.staffInviteId]: created.code }));
      onFeedback?.({ label, state: 'confirmed' });
      setReload((value) => value + 1);
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusyId(null);
    }
  };

  const revoke = async (invite: StaffInviteSummaryDto) => {
    const label = t('op.management.staff.pending.revoke');
    setBusyId(invite.staffInviteId);
    onFeedback?.({ label, state: 'pending' });
    try {
      await clients().revokeStaffInvite(backend.branchId, invite.staffInviteId);
      onFeedback?.({ label, state: 'confirmed' });
      setReload((value) => value + 1);
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section className="staff-pending" aria-labelledby="staff-pending-title">
      <h3 id="staff-pending-title" className="mgmt-section-title">
        <span>{t('op.management.staff.pending.title', { count: invites.length })}</span>
      </h3>
      <ul className="staff-pending-list">
        {invites.map((invite) => {
          const fresh = freshCodes[invite.staffInviteId];
          return (
            <li key={invite.staffInviteId} data-status={invite.status}>
              <div className="staff-pending-who">
                <strong>{invite.displayName}</strong>
                <span>{invite.phoneNumber} · {invite.roleNames.map((role) => staffRoleLabel(role, t)).join(', ')}</span>
              </div>
              <div className="staff-pending-state">
                {fresh
                  ? <span className="staff-pending-code">{t('op.management.staff.pending.freshCode', { code: groupCode(fresh) })}</span>
                  : <span>{statusText(invite)}</span>}
              </div>
              <div className="staff-pending-actions">
                <button type="button" className="ui-btn" disabled={busyId !== null} onClick={() => void reissue(invite)}>
                  <KeyRound size={14} aria-hidden="true" />
                  {t('op.management.staff.pending.reissue')}
                </button>
                <button type="button" className="ui-btn" disabled={busyId !== null} onClick={() => void revoke(invite)}>
                  <Trash2 size={14} aria-hidden="true" />
                  {t('op.management.staff.pending.revoke')}
                </button>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );

  function statusText(invite: StaffInviteSummaryDto): string {
    if (invite.status === StaffInviteStatusNames.Exhausted) return t('op.management.staff.pending.exhausted');
    if (invite.status === StaffInviteStatusNames.Expired) return t('op.management.staff.pending.expired');
    return t('op.management.staff.pending.until', { time: formatDate(invite.expiresAtUtc) });
  }
}
