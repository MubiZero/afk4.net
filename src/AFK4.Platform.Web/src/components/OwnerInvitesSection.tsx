import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { OwnerInvite, TenantBranch } from '../api/types';
import { EmptyState, ErrorBanner, Field, Loading, formatDate } from './ui';

export interface OwnerInvitesSectionProps {
  client: PlatformApiClient;
  organizationId: string;
  branches: TenantBranch[];
  initialInvite?: OwnerInvite | null;
}

export function OwnerInvitesSection({ client, organizationId, branches, initialInvite }: OwnerInvitesSectionProps) {
  const [invites, setInvites] = useState<OwnerInvite[] | null>(
    initialInvite !== undefined && initialInvite !== null ? [initialInvite] : null
  );
  const [isLoading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const [ownerUserName, setOwnerUserName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [isCreating, setCreating] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const detail = await client.getTenant(organizationId);
      // The tenant detail endpoint doesn't include invites today; pull pending invites by
      // fetching the rotation result for each branch is also unsafe (it would create new
      // pending invites). For Slice 5 MVP, we keep the in-memory list returned by the
      // create/rotate/revoke calls and just re-show whatever was returned last.
      void detail;
    } catch (cause) {
      setError(toMessage(cause, 'Failed to load owner invites.'));
    } finally {
      setLoading(false);
    }
  }, [client, organizationId]);

  useEffect(() => {
    if (invites === null) {
      void refresh();
    }
  }, [invites, refresh]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (branchId === '') {
      setError('Choose a branch first.');
      return;
    }
    setCreating(true);
    setError(null);
    try {
      const created = await client.createOwnerInvite(
        organizationId,
        branchId,
        ownerUserName.trim() === '' ? null : ownerUserName.trim(),
        ownerDisplayName.trim() === '' ? null : ownerDisplayName.trim(),
        null
      );
      setInvites(current => [created, ...((current ?? []).filter(inv => inv.status !== 'pending' || inv.branchId !== branchId))]);
      setOwnerUserName('');
      setOwnerDisplayName('');
    } catch (cause) {
      setError(toMessage(cause, 'Failed to create owner invite.'));
    } finally {
      setCreating(false);
    }
  }

  async function handleRevoke(ownerInviteId: string) {
    const reason = window.prompt('Revoke reason?', 'Owner asked to cancel');
    if (reason === null || reason.trim().length === 0) {
      return;
    }
    setError(null);
    try {
      const revoked = await client.revokeOwnerInvite(ownerInviteId, reason);
      setInvites(current => (current ?? []).map(inv => (inv.ownerInviteId === revoked.ownerInviteId ? revoked : inv)));
    } catch (cause) {
      setError(toMessage(cause, 'Failed to revoke invite.'));
    }
  }

  return (
    <section className="section">
      <header className="section-header">
        <h2>Owner invites</h2>
      </header>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      <form className="form form-inline" onSubmit={handleCreate}>
        <Field label="Branch" htmlFor="invite-branch">
          <select id="invite-branch" value={branchId} onChange={e => setBranchId(e.target.value)}>
            {branches.map(branch => (
              <option key={branch.branchId} value={branch.branchId}>{branch.slug} — {branch.name}</option>
            ))}
          </select>
        </Field>
        <Field label="Owner user name (email)" htmlFor="invite-username">
          <input id="invite-username" value={ownerUserName} onChange={e => setOwnerUserName(e.target.value)} />
        </Field>
        <Field label="Owner display name" htmlFor="invite-display">
          <input id="invite-display" value={ownerDisplayName} onChange={e => setOwnerDisplayName(e.target.value)} />
        </Field>
        <button type="submit" className="primary" disabled={isCreating || branches.length === 0}>
          {isCreating ? 'Creating…' : 'Create invite'}
        </button>
      </form>
      {isLoading && <Loading label="Loading invites…" />}
      {invites !== null && invites.length === 0 && (
        <EmptyState>No invites yet. Use the form above to send the first one.</EmptyState>
      )}
      {invites !== null && invites.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Status</th>
              <th>Code</th>
              <th>Owner</th>
              <th>Expires</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {invites.map(invite => (
              <tr key={invite.ownerInviteId}>
                <td>{invite.status}</td>
                <td><code className="code-block">{invite.code}</code></td>
                <td>{invite.ownerUserName ?? '—'}</td>
                <td>{formatDate(invite.expiresAtUtc)}</td>
                <td>
                  {invite.status === 'pending' && (
                    <button type="button" className="link" onClick={() => void handleRevoke(invite.ownerInviteId)}>
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

function toMessage(cause: unknown, fallback: string): string {
  if (cause instanceof PlatformApiError) {
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return fallback;
}
