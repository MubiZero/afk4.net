import { useEffect, useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { ErrorBanner, Field } from './ui';

export interface AcceptInviteProps {
  client: StaffAuthApiClient;
  initialCode: string | null;
  onAccepted: () => void;
  onOpenSignIn: () => void;
}

export function AcceptInvite({ client, initialCode, onAccepted, onOpenSignIn }: AcceptInviteProps) {
  const [code, setCode] = useState(initialCode ?? '');
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  useEffect(() => {
    setCode(initialCode ?? '');
  }, [initialCode]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedCode = code.trim();
    const normalizedUserName = userName.trim();
    const normalizedDisplayName = displayName.trim();
    if (normalizedCode.length === 0) {
      setError('Setup code is required.');
      return;
    }
    if (normalizedUserName.length === 0 || normalizedDisplayName.length === 0) {
      setError('User name and display name are required.');
      return;
    }
    if (password.length < 8) {
      setError('Password must be at least 8 characters.');
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await client.acceptInvite({
        code: normalizedCode,
        userName: normalizedUserName,
        displayName: normalizedDisplayName,
        password
      });
      onAccepted();
    } catch (cause) {
      setError(projectAcceptInviteError(cause));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <h1>Accept setup code</h1>
      <p className="muted">Create the owner sign-in for this club.</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label="Setup code" htmlFor="accept-code">
          <input
            id="accept-code"
            name="code"
            type="text"
            autoComplete="one-time-code"
            value={code}
            onChange={event => setCode(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="User name" htmlFor="accept-username">
          <input
            id="accept-username"
            name="userName"
            type="text"
            autoComplete="username"
            value={userName}
            onChange={event => setUserName(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="Display name" htmlFor="accept-display-name">
          <input
            id="accept-display-name"
            name="displayName"
            type="text"
            autoComplete="name"
            value={displayName}
            onChange={event => setDisplayName(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="Password" htmlFor="accept-password">
          <input
            id="accept-password"
            name="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label="Confirm password" htmlFor="accept-confirm-password">
          <input
            id="accept-confirm-password"
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={event => setConfirmPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <div className="actions">
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Accepting...' : 'Accept and open club'}
          </button>
          <button type="button" onClick={onOpenSignIn} disabled={isSubmitting}>
            Sign in instead
          </button>
        </div>
      </form>
    </div>
  );
}

function projectAcceptInviteError(cause: unknown): string {
  if (cause instanceof PlatformApiError) {
    if (cause.status === 404) {
      return 'Setup code was not found.';
    }
    if (cause.status === 409) {
      return 'That user name is already in use.';
    }
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return 'Setup code acceptance failed.';
}
