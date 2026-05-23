import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { OwnerInvite, OwnerInviteSummary, TenantBranch } from '../api/types';
import { EmptyState, ErrorBanner, Field, Loading, formatDate } from './ui';

export interface OwnerInvitesSectionProps {
  client: PlatformApiClient;
  organizationId: string;
  branches: TenantBranch[];
  initialInvite?: OwnerInvite | null;
}

export function OwnerInvitesSection({ client, organizationId, branches, initialInvite }: OwnerInvitesSectionProps) {
  const [invites, setInvites] = useState<OwnerInviteSummary[] | null>(null);
  const [revealedCodes, setRevealedCodes] = useState<Map<string, string>>(() => {
    const seed = new Map<string, string>();
    if (initialInvite) {
      seed.set(initialInvite.ownerInviteId, initialInvite.code);
    }
    return seed;
  });
  const [isLoading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const [ownerUserName, setOwnerUserName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [isCreating, setCreating] = useState(false);
  const [revokingInviteId, setRevokingInviteId] = useState<string | null>(null);
  const [revokeReason, setRevokeReason] = useState('');
  const [isRevoking, setIsRevoking] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await client.listOwnerInvites(organizationId);
      setInvites(list);
    } catch (cause) {
      setError(toMessage(cause, 'Failed to load owner invites.'));
    } finally {
      setLoading(false);
    }
  }, [client, organizationId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

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
      setRevealedCodes((current) => {
        const next = new Map(current);
        next.set(created.ownerInviteId, created.code);
        return next;
      });
      setOwnerUserName('');
      setOwnerDisplayName('');
      await refresh();
    } catch (cause) {
      setError(toMessage(cause, 'Failed to create owner invite.'));
    } finally {
      setCreating(false);
    }
  }

  function openRevoke(ownerInviteId: string) {
    setRevokingInviteId(ownerInviteId);
    setRevokeReason('');
  }

  function cancelRevoke() {
    setRevokingInviteId(null);
    setRevokeReason('');
  }

  async function handleRevoke(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (revokingInviteId === null) {
      return;
    }
    const trimmed = revokeReason.trim();
    if (trimmed.length === 0) {
      setError('Provide a revoke reason.');
      return;
    }
    setIsRevoking(true);
    setError(null);
    try {
      await client.revokeOwnerInvite(revokingInviteId, trimmed);
      setRevealedCodes((current) => {
        if (!current.has(revokingInviteId)) {
          return current;
        }
        const next = new Map(current);
        next.delete(revokingInviteId);
        return next;
      });
      setRevokingInviteId(null);
      setRevokeReason('');
      await refresh();
    } catch (cause) {
      setError(toMessage(cause, 'Failed to revoke invite.'));
    } finally {
      setIsRevoking(false);
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
      {isLoading && invites === null && <Loading label="Loading invites…" />}
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
            {invites.map(invite => {
              const revealed = revealedCodes.get(invite.ownerInviteId);
              return (
                <tr key={invite.ownerInviteId}>
                  <td>{invite.status}</td>
                  <td>
                    <code className="code-block">
                      {revealed !== undefined ? revealed : `•••• ${invite.codeSuffix}`}
                    </code>
                  </td>
                  <td>{invite.ownerUserName ?? '—'}</td>
                  <td>{formatDate(invite.expiresAtUtc)}</td>
                  <td>
                    {invite.status === 'pending' && revokingInviteId !== invite.ownerInviteId && (
                      <button type="button" className="link" onClick={() => openRevoke(invite.ownerInviteId)}>
                        Revoke
                      </button>
                    )}
                    {revokingInviteId === invite.ownerInviteId && (
                      <form className="form-inline form-revoke" onSubmit={handleRevoke}>
                        <input
                          aria-label="Revoke reason"
                          placeholder="Reason"
                          value={revokeReason}
                          onChange={(e) => setRevokeReason(e.target.value)}
                          autoFocus
                          disabled={isRevoking}
                        />
                        <button type="submit" className="link" disabled={isRevoking}>
                          {isRevoking ? 'Revoking…' : 'Confirm'}
                        </button>
                        <button type="button" className="link" onClick={cancelRevoke} disabled={isRevoking}>
                          Cancel
                        </button>
                      </form>
                    )}
                  </td>
                </tr>
              );
            })}
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
