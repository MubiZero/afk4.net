import { useState, type FormEvent } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  ConnectionResolutionError,
  ConnectionResolver,
  OperatorOrganizationStatus,
  type ResolveOperatorConnectionResponse
} from './connectionResolver';

type Translate = (key: MessageKey) => string;

export interface ConnectionResolutionScreenProps {
  resolver: ConnectionResolver;
  onResolved: (resolution: ResolveOperatorConnectionResponse) => void;
}

type Mode = 'slug' | 'setup_code';

export function ConnectionResolutionScreen({ resolver, onResolved }: ConnectionResolutionScreenProps) {
  const { t } = useI18n();
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
        setError(buildResolutionMessage(cause, t));
      } else if (cause instanceof Error) {
        setError(localizeResolutionErrorDetail(cause.message, t));
      } else {
        setError(t('operator.connect.error.generic'));
      }
    } finally {
      setResolving(false);
    }
  }

  return (
    <div className="operator-connection-screen">
      <h1>{t('operator.connect.title')}</h1>
      <p>{t('operator.connect.subtitle')}</p>
      <div className="operator-connection-modes">
        <button
          type="button"
          className={mode === 'slug' ? 'is-active' : ''}
          onClick={() => setMode('slug')}
          disabled={isResolving}
        >
          {t('operator.connect.mode.slug')}
        </button>
        <button
          type="button"
          className={mode === 'setup_code' ? 'is-active' : ''}
          onClick={() => setMode('setup_code')}
          disabled={isResolving}
        >
          {t('operator.connect.mode.setupCode')}
        </button>
      </div>
      <form onSubmit={handleSubmit} className="operator-connection-form">
        {error !== null && (
          <div role="alert" className="operator-connection-error">{error}</div>
        )}
        {mode === 'slug' ? (
          <>
            <label htmlFor="org-slug">{t('operator.connect.field.orgSlug')}</label>
            <input
              id="org-slug"
              value={organizationSlug}
              onChange={event => setOrganizationSlug(event.target.value)}
              autoComplete="off"
              required
              disabled={isResolving}
            />
            <label htmlFor="branch-slug">{t('operator.connect.field.branchSlug')}</label>
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
            <label htmlFor="setup-code">{t('operator.connect.field.setupCode')}</label>
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
          {isResolving ? t('operator.connect.action.resolving') : t('operator.connect.action.submit')}
        </button>
      </form>
    </div>
  );
}

function buildResolutionMessage(error: ConnectionResolutionError, t: Translate): string {
  switch (error.status) {
    case 404:
      return t('operator.connect.error.notFound');
    case 400:
      return localizeResolutionErrorDetail(error.message, t);
    default:
      return `${localizeResolutionErrorDetail(error.message, t)} ${t('operator.connect.error.platformCode')} ${error.status}.`;
  }
}

function localizeResolutionErrorDetail(message: string, t: Translate): string {
  const normalized = message.trim();
  if (!normalized || normalized === 'Failed to resolve operator connection.') {
    return t('operator.connect.error.generic');
  }

  if (/setup code is no longer usable/i.test(normalized)) {
    return t('operator.connect.error.setupCodeExpired');
  }

  return normalized;
}

export function isOperatorOrganizationBlocked(
  resolution: ResolveOperatorConnectionResponse | null
): boolean {
  if (resolution === null) {
    return false;
  }
  return resolution.organizationStatus === OperatorOrganizationStatus.Suspended
    || resolution.organizationStatus === OperatorOrganizationStatus.DeletionPending;
}
