import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { ArrowRight, HelpCircle, Loader2, X } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  discoverOwner,
  type WizardDiscoverResponse,
} from './wizardApi';
import { isHostBridgeUnavailableError } from './hostBridge';

interface OwnerCodeScreenProps {
  onDiscovered(ownerCode: string, response: WizardDiscoverResponse): void;
  onUsePhone(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

const OWNER_CODE_LENGTH = 8;
const OWNER_CODE_PATTERN = /^[0-9]+$/;

export function OwnerCodeScreen({ onDiscovered, onUsePhone }: OwnerCodeScreenProps) {
  const { t } = useI18n();
  const [code, setCode] = useState('');
  const [touched, setTouched] = useState(false);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });
  const [helpOpen, setHelpOpen] = useState(false);
  const helpWrapRef = useRef<HTMLDivElement>(null);
  const [showSlowSkeleton, setShowSlowSkeleton] = useState(false);

  // Skeleton показывается только если discover висит >300ms (правило 31), и удерживается
  // не меньше 400ms чтобы не моргать на быстрых ответах (interface-limb §8 «show-delay + min-visible»).
  useEffect(() => {
    if (request.kind !== 'loading') {
      setShowSlowSkeleton(false);
      return;
    }
    const showTimer = setTimeout(() => setShowSlowSkeleton(true), 300);
    return () => clearTimeout(showTimer);
  }, [request.kind]);

  // Закрытие popover'a по клику снаружи и Esc — правило 41 (обучение в контексте,
  // но не блокируем основной поток).
  useEffect(() => {
    if (!helpOpen) return;
    const onPointerDown = (event: MouseEvent) => {
      if (!helpWrapRef.current?.contains(event.target as Node)) {
        setHelpOpen(false);
      }
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setHelpOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [helpOpen]);

  const sanitized = code.replace(/\s+/g, '');
  const isValid = sanitized.length === OWNER_CODE_LENGTH && OWNER_CODE_PATTERN.test(sanitized);
  // По правилу «не обвиняй пока печатает»: подсказку формата показываем только после blur
  // или после попытки submit, не во время первой набивки кода.
  const showFormatHint =
    touched && code.length > 0 && !isValid && request.kind !== 'loading';

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      // Submit «учитывается» как первая попытка валидации — после неё подсказка формата уже разрешена
      setTouched(true);
      if (!isValid || request.kind === 'loading') {
        return;
      }

      setRequest({ kind: 'loading' });
      try {
        const response = await discoverOwner(sanitized);
        onDiscovered(sanitized, response);
      } catch (error) {
        setRequest({
          kind: 'error',
          message: messageForError(error, t),
        });
      }
    },
    [isValid, onDiscovered, request.kind, sanitized, t],
  );

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">{t('setup.wizard.common.step')} 1</span>
        <h1>{t('setup.wizard.ownerCode.title')}</h1>
        <p>{t('setup.wizard.ownerCode.subtitle')}</p>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label wizard-field-label-row">
            {t('setup.wizard.ownerCode.field.label')}
            <span className="wizard-help-anchor" ref={helpWrapRef}>
              <button
                type="button"
                className="wizard-help-trigger"
                aria-label={t('setup.wizard.ownerCode.help.aria')}
                aria-expanded={helpOpen}
                onClick={() => setHelpOpen((v) => !v)}
              >
                <HelpCircle size={14} aria-hidden />
              </button>
              {helpOpen && (
                <div role="dialog" className="wizard-popover">
                  <div className="wizard-popover-head">
                    <strong>{t('setup.wizard.ownerCode.help.title')}</strong>
                    <button
                      type="button"
                      className="wizard-popover-close"
                      aria-label={t('setup.wizard.titlebar.close')}
                      onClick={() => setHelpOpen(false)}
                    >
                      <X size={14} aria-hidden />
                    </button>
                  </div>
                  <ol className="wizard-popover-steps">
                    <li>{t('setup.wizard.ownerCode.help.step1')}</li>
                    <li>{t('setup.wizard.ownerCode.help.step2')}</li>
                    <li>{t('setup.wizard.ownerCode.help.step3')}</li>
                  </ol>
                  <p className="wizard-popover-note">{t('setup.wizard.ownerCode.help.note')}</p>
                </div>
              )}
            </span>
          </span>
          <input
            type="text"
            inputMode="numeric"
            autoComplete="off"
            autoFocus
            spellCheck={false}
            value={code}
            maxLength={OWNER_CODE_LENGTH + 2}
            onChange={(event) => {
              setCode(event.target.value);
              if (request.kind === 'error') {
                setRequest({ kind: 'idle' });
              }
            }}
            onBlur={() => setTouched(true)}
            placeholder="12345678"
            aria-invalid={request.kind === 'error' || showFormatHint}
            aria-describedby="owner-code-hint"
          />
          <span id="owner-code-hint" className="wizard-field-hint">
            {showFormatHint
              ? t('setup.wizard.ownerCode.hint.format')
              : t('setup.wizard.ownerCode.hint.default')}
          </span>
        </label>

        {showSlowSkeleton && (
          <div className="wizard-skeleton-list" aria-hidden>
            <div className="wizard-skeleton-card" />
            <div className="wizard-skeleton-card" />
            <div className="wizard-skeleton-card" />
          </div>
        )}

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <button
          type="submit"
          className="wizard-primary"
          disabled={!isValid || request.kind === 'loading'}
        >
          {request.kind === 'loading' ? (
            <>
              <Loader2 className="wizard-spinner" aria-hidden />
              <span>{t('setup.wizard.ownerCode.action.finding')}</span>
            </>
          ) : (
            <>
              <span>{t('setup.wizard.ownerCode.action.find')}</span>
              <ArrowRight aria-hidden />
            </>
          )}
        </button>

        <button type="button" className="wizard-link-action wizard-fallback-link" onClick={onUsePhone}>
          {t('setup.wizard.ownerCode.action.usePhone')}
        </button>
      </form>
    </section>
  );
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.ownerCode.error.bridgeMissing');
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return t('setup.wizard.ownerCode.error.generic');
}
