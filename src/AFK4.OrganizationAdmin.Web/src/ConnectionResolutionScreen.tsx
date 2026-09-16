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
      // Сбой до ответа (сети нет, сервер не поднялся) несёт английский текст из fetch — он для
      // журнала, а не для человека, который ещё даже не вошёл.
      setError(cause instanceof ConnectionResolutionError
        ? buildResolutionMessage(cause, t)
        : t('operator.connect.error.generic'));
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

// Это первый экран, который видит человек: до входа и до всего остального. Английская фраза
// сервера — «OrganizationSlug must contain only lowercase letters…» — доезжала до него дословно
// и читалась как поломка программы. Теперь причину называет код отказа.
const CONNECT_ERROR_KEYS: Record<string, MessageKey> = {
  connection_input_missing: 'operator.connect.error.inputMissing',
  connection_input_ambiguous: 'operator.connect.error.inputAmbiguous',
  connection_slug_invalid: 'operator.connect.error.slugInvalid',
  setup_code_required: 'operator.connect.error.setupCodeRequired',
  setup_code_not_usable: 'operator.connect.error.setupCodeExpired'
};

function buildResolutionMessage(error: ConnectionResolutionError, t: Translate): string {
  const named = error.code !== null ? CONNECT_ERROR_KEYS[error.code] : undefined;
  if (named !== undefined) {
    return t(named);
  }

  switch (error.status) {
    case 404:
      return t('operator.connect.error.notFound');
    case 400:
      return t('operator.connect.error.generic');
    default:
      return `${t('operator.connect.error.generic')} ${t('operator.connect.error.platformCode')} ${error.status}.`;
  }
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
