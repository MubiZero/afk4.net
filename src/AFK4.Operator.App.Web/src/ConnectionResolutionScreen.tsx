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
        setError(localizeResolutionErrorDetail(cause.message));
      } else {
        setError('Не удалось настроить подключение оператора.');
      }
    } finally {
      setResolving(false);
    }
  }

  return (
    <div className="operator-connection-screen">
      <h1>Подключение клуба</h1>
      <p>
        Введите ключ клуба и ключ филиала или используйте код подключения от администратора.
      </p>
      <div className="operator-connection-modes">
        <button
          type="button"
          className={mode === 'slug' ? 'is-active' : ''}
          onClick={() => setMode('slug')}
          disabled={isResolving}
        >
          Клуб и филиал
        </button>
        <button
          type="button"
          className={mode === 'setup_code' ? 'is-active' : ''}
          onClick={() => setMode('setup_code')}
          disabled={isResolving}
        >
          Код подключения
        </button>
      </div>
      <form onSubmit={handleSubmit} className="operator-connection-form">
        {error !== null && (
          <div role="alert" className="operator-connection-error">{error}</div>
        )}
        {mode === 'slug' ? (
          <>
            <label htmlFor="org-slug">Ключ клуба</label>
            <input
              id="org-slug"
              value={organizationSlug}
              onChange={event => setOrganizationSlug(event.target.value)}
              autoComplete="off"
              required
              disabled={isResolving}
            />
            <label htmlFor="branch-slug">Ключ филиала</label>
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
            <label htmlFor="setup-code">Код подключения</label>
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
          {isResolving ? 'Проверяем' : 'Продолжить'}
        </button>
      </form>
    </div>
  );
}

function buildResolutionMessage(error: ConnectionResolutionError): string {
  switch (error.status) {
    case 404:
      return 'Не нашли клуб по этим ключам или коду подключения. Проверьте данные у администратора.';
    case 400:
      return localizeResolutionErrorDetail(error.message);
    default:
      return `${localizeResolutionErrorDetail(error.message)} Код ошибки платформы: ${error.status}.`;
  }
}

function localizeResolutionErrorDetail(message: string): string {
  const normalized = message.trim();
  if (!normalized || normalized === 'Failed to resolve operator connection.') {
    return 'Не удалось настроить подключение оператора.';
  }

  if (/setup code is no longer usable/i.test(normalized)) {
    return 'Код подключения больше не действует.';
  }

  return normalized;
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
