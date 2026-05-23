import { useState, type FormEvent } from 'react';
import {
  ConnectionResolutionError,
  ConnectionResolver,
  OperatorTenantStatus,
  type ResolveOperatorConnectionResponse
} from './connectionResolver';

export interface ConnectionResolutionScreenProps {
  resolver: ConnectionResolver;
  onResolved: (resolution: ResolveOperatorConnectionResponse) => void;
}

type Mode = 'slug' | 'setup_code';

export function ConnectionResolutionScreen({ resolver, onResolved }: ConnectionResolutionScreenProps) {
  const [mode, setMode] = useState<Mode>('slug');
  const [organizationSlug, setOrganizationSlug] = useState('');
  const [branchSlug, setBranchSlug] = useState('');
  const [setupCode, setSetupCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isResolving, setResolving] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setResolving(true);
    setError(null);
    try {
      const resolution = mode === 'setup_code'
        ? await resolver.resolveBySetupCode(setupCode.trim())
        : await resolver.resolveBySlugPair(organizationSlug.trim(), branchSlug.trim());
      onResolved(resolution);
    } catch (cause) {
      if (cause instanceof ConnectionResolutionError) {
        setError(buildResolutionMessage(cause));
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError('Failed to resolve operator connection.');
      }
    } finally {
      setResolving(false);
    }
  }

  return (
    <div className="operator-connection-screen">
      <h1>Connect to your club</h1>
      <p>
        Sign in to your club by entering its organisation and branch slugs, or paste the setup
        code your operator gave you.
      </p>
      <div className="operator-connection-modes">
        <button
          type="button"
          className={mode === 'slug' ? 'is-active' : ''}
          onClick={() => setMode('slug')}
          disabled={isResolving}
        >
          Slug pair
        </button>
        <button
          type="button"
          className={mode === 'setup_code' ? 'is-active' : ''}
          onClick={() => setMode('setup_code')}
          disabled={isResolving}
        >
          Setup code
        </button>
      </div>
      <form onSubmit={handleSubmit} className="operator-connection-form">
        {error !== null && (
          <div role="alert" className="operator-connection-error">{error}</div>
        )}
        {mode === 'slug' ? (
          <>
            <label htmlFor="org-slug">Organisation slug</label>
            <input
              id="org-slug"
              value={organizationSlug}
              onChange={event => setOrganizationSlug(event.target.value)}
              autoComplete="off"
              required
              disabled={isResolving}
            />
            <label htmlFor="branch-slug">Branch slug</label>
            <input
              id="branch-slug"
              value={branchSlug}
              onChange={event => setBranchSlug(event.target.value)}
              autoComplete="off"
              required
              disabled={isResolving}
            />
          </>
        ) : (
          <>
            <label htmlFor="setup-code">Setup code</label>
            <input
              id="setup-code"
              value={setupCode}
              onChange={event => setSetupCode(event.target.value)}
              autoComplete="off"
              required
              disabled={isResolving}
            />
          </>
        )}
        <button type="submit" disabled={isResolving}>
          {isResolving ? 'Resolving…' : 'Continue'}
        </button>
      </form>
    </div>
  );
}

function buildResolutionMessage(error: ConnectionResolutionError): string {
  switch (error.status) {
    case 404:
      return 'No tenant matched the slugs / setup code. Double-check with your operator.';
    case 400:
      return error.message;
    default:
      return `${error.message} (HTTP ${error.status})`;
  }
}

export function isOperatorTenantBlocked(
  resolution: ResolveOperatorConnectionResponse | null
): boolean {
  if (resolution === null) {
    return false;
  }
  return resolution.organizationStatus === OperatorTenantStatus.Suspended
    || resolution.organizationStatus === OperatorTenantStatus.DeletionPending;
}
