import { useCallback, useState, type FormEvent } from 'react';
import { ArrowRight, Loader2 } from 'lucide-react';
import {
  discoverOwner,
  type WizardDiscoverResponse,
} from './wizardApi';
import { isHostBridgeUnavailableError } from './hostBridge';

interface OwnerCodeScreenProps {
  onDiscovered(response: WizardDiscoverResponse): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

const OWNER_CODE_LENGTH = 8;
const OWNER_CODE_PATTERN = /^[0-9]+$/;

export function OwnerCodeScreen({ onDiscovered }: OwnerCodeScreenProps) {
  const [code, setCode] = useState('');
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });

  const sanitized = code.replace(/\s+/g, '');
  const isValid = sanitized.length === OWNER_CODE_LENGTH && OWNER_CODE_PATTERN.test(sanitized);
  const showFormatHint = code.length > 0 && !isValid && request.kind !== 'loading';

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!isValid || request.kind === 'loading') {
        return;
      }

      setRequest({ kind: 'loading' });
      try {
        const response = await discoverOwner(sanitized);
        onDiscovered(response);
      } catch (error) {
        setRequest({
          kind: 'error',
          message: messageForError(error),
        });
      }
    },
    [isValid, onDiscovered, request.kind, sanitized],
  );

  return (
    <section className="wizard-screen">
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">Step 1</span>
        <h1>Подключение клуба</h1>
        <p>Введите 8-значный код владельца из панели клуба, чтобы найти ваш клуб и его филиалы.</p>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">Owner code</span>
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
            placeholder="12345678"
            aria-invalid={request.kind === 'error' || showFormatHint}
            aria-describedby="owner-code-hint"
          />
          <span id="owner-code-hint" className="wizard-field-hint">
            {showFormatHint
              ? 'Код состоит из 8 цифр.'
              : 'Найдите код в разделе «Установка» панели клуба.'}
          </span>
        </label>

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
              <span>Ищем клуб…</span>
            </>
          ) : (
            <>
              <span>Найти клуб</span>
              <ArrowRight aria-hidden />
            </>
          )}
        </button>
      </form>
    </section>
  );
}

function messageForError(error: unknown): string {
  if (isHostBridgeUnavailableError(error)) {
    return 'Не удаётся связаться с локальным агентом. Запустите приложение из ярлыка Setup Wizard.';
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return 'Не удалось проверить код. Попробуйте ещё раз.';
}
