import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../../PanelModal';
import { EmptyState, LoadFailureState } from '../../operatorPrimitives';
import { DeferredSkeleton, SkeletonLine } from '../../LoadingSkeleton';
import { projectOperatorError, type OperatorErrorProjection } from '../../apiErrors';
import { staffRoleOptions } from '../../operatorPermissions';
import { createAuthenticatedOperatorClients, operatorDisplayNameLabel, staffRoleLabel } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { StaffBranchCandidateDto, StaffUserDto } from '../../operatorApiClients';
import { toggleRole } from './StaffRolesDestination';

/**
 * Добавить в филиал человека, который уже работает в сети.
 *
 * Приглашение на тот же телефон сервер отклоняет — человек уже есть, — а роли раньше менялись
 * только там, где он назначен. Сотрудника второго филиала было не поставить в этот, а того, кого
 * сняли с последнего филиала, не показывал ни один список. Здесь видны все сотрудники
 * организации без назначения в этом филиале: где они работают или что филиала у них нет вовсе.
 */
export function StaffFromNetworkModal({ backend, onClose, onAdded }: {
  backend: OperatorBackendContext;
  onClose: () => void;
  onAdded: (staffUser: StaffUserDto) => void;
}) {
  const { t } = useI18n();
  const [candidates, setCandidates] = useState<StaffBranchCandidateDto[] | null>(null);
  const [loadFailure, setLoadFailure] = useState<OperatorErrorProjection | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [roleNames, setRoleNames] = useState<string[]>(['operator']);
  const [busy, setBusy] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoadFailure(null);
    createAuthenticatedOperatorClients(backend.config, backend.session).settings
      .getStaffCandidates(backend.branchId)
      .then((rows) => {
        if (!active) return;
        setCandidates(rows);
        setSelectedId((current) => current ?? rows[0]?.staffUserId ?? null);
      })
      .catch((reason: unknown) => { if (active) setLoadFailure(projectOperatorError(reason, t)); });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend.branchId, backend.config, backend.session, attempt]);

  const submit = async () => {
    if (selectedId === null || roleNames.length === 0) return;
    setBusy(true);
    setSubmitError(null);
    try {
      const staffUser = await createAuthenticatedOperatorClients(backend.config, backend.session).settings
        .updateStaffUserRoles(backend.branchId, selectedId, { organizationId: backend.session.organizationId, roleNames });
      onAdded(staffUser);
    } catch (reason) {
      setSubmitError(projectOperatorError(reason, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const body = loadFailure !== null ? (
    <LoadFailureState title={t('op.management.state.errorTitle')} failure={loadFailure} onRetry={() => setAttempt((value) => value + 1)} />
  ) : candidates === null ? (
    <DeferredSkeleton>
      <div className="staff-network-list" data-skeleton="list" aria-hidden="true">
        {[0, 1, 2].map((row) => <div key={row} className="staff-network-option"><SkeletonLine width="40%" /><SkeletonLine width="60%" /></div>)}
      </div>
    </DeferredSkeleton>
  ) : candidates.length === 0 ? (
    <EmptyState
      inline
      title={t('op.management.staff.network.emptyTitle')}
      next={{ kind: 'calm', hint: t('op.management.staff.network.emptyHint') }}
    />
  ) : (
    <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submit(); }}>
      <fieldset className="staff-network-list">
        <legend>{t('op.management.staff.network.personLabel')}</legend>
        {candidates.map((candidate) => (
          <label key={candidate.staffUserId} className="staff-network-option">
            <input
              type="radio"
              name="staff-network-person"
              checked={selectedId === candidate.staffUserId}
              disabled={busy}
              onChange={() => setSelectedId(candidate.staffUserId)}
            />
            <span>
              <b>{operatorDisplayNameLabel(candidate.displayName, t)}</b>
              <small>
                {candidate.branchNames.length > 0
                  ? t('op.management.staff.network.worksIn', { branches: candidate.branchNames.join(', ') })
                  : t('op.management.staff.network.noBranch')}
                {!candidate.isActive && ` · ${t('op.settings.staff.inactive')}`}
              </small>
            </span>
          </label>
        ))}
      </fieldset>
      <fieldset className="mgmt-role-set">
        <legend>{t('op.management.staff.network.rolesLabel')}</legend>
        {staffRoleOptions.map((role) => (
          <label key={role} className="mgmt-check">
            <input
              type="checkbox"
              checked={roleNames.includes(role)}
              disabled={busy}
              onChange={() => setRoleNames((current) => toggleRole(current, role))}
            />
            {staffRoleLabel(role, t)}
          </label>
        ))}
      </fieldset>
      {submitError !== null && <p className="mgmt-drawer-hint" role="alert">{submitError}</p>}
      <div className="mgmt-form-actions">
        <button type="button" className="ui-btn" onClick={onClose} disabled={busy}>{t('common.cancel')}</button>
        <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || selectedId === null || roleNames.length === 0}>
          {t('op.management.staff.network.submit')}
        </button>
      </div>
    </form>
  );

  return (
    <PanelModal title={t('op.management.staff.network.title')} subtitle={t('op.management.staff.network.subtitle')} onClose={onClose} closeDisabled={busy}>
      {body}
    </PanelModal>
  );
}
