import { useEffect, useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { ErrorBanner, Field } from './ui';

export interface StaffSignInProps {
  client: StaffAuthApiClient;
  initialOrganizationId: string | null;
  onSignedIn: () => void;
}

export function StaffSignIn({ client, initialOrganizationId, onSignedIn }: StaffSignInProps) {
  const [organizationId, setOrganizationId] = useState(initialOrganizationId ?? '');
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  useEffect(() => {
    setOrganizationId(initialOrganizationId ?? '');
  }, [initialOrganizationId]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedOrganizationId = organizationId.trim();
    const normalizedUserName = userName.trim();
    if (normalizedOrganizationId.length === 0 || normalizedUserName.length === 0) {
      setError('Organization and user name are required.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await client.signIn(normalizedOrganizationId, normalizedUserName, password);
      onSignedIn();
    } catch (cause) {
      setError(projectStaffSignInError(cause));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <h1>Club sign in</h1>
      <p className="muted">Sign in with your club staff account.</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label="Organization" htmlFor="staff-organization">
          <input
            id="staff-organization"
            name="organizationId"
            type="text"
            autoComplete="organization"
            value={organizationId}
            onChange={event => setOrganizationId(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="User name" htmlFor="staff-username">
          <input
            id="staff-username"
            name="userName"
            type="text"
            autoComplete="username"
            value={userName}
            onChange={event => setUserName(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="Password" htmlFor="staff-password">
          <input
            id="staff-password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}

function projectStaffSignInError(cause: unknown): string {
  if (cause instanceof PlatformApiError) {
    if (cause.status === 401) {
      return 'Wrong organization, user name, or password.';
    }
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return 'Sign-in failed.';
}
