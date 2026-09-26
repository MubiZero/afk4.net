import { useEffect, useRef } from 'react';
import { PIN_LENGTH, keepPinDigits } from '@afk4/contracts';

/**
 * Шесть клеток под ПИН-код или код первого входа.
 *
 * Под клетками одно настоящее поле, а не шесть: вставка из буфера, код из SMS, менеджер паролей
 * и экранная клавиатура работают как с обычным полем. Клетки только показывают, сколько набрано
 * и куда пойдёт следующая цифра. Шестая цифра сразу отправляет — кнопка «Войти» не нужна.
 */
export function PinBoxes({
  id,
  label,
  value,
  onChange,
  onComplete,
  secret = false,
  autoComplete,
  invalid = false,
  disabled = false,
  describedBy
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  onComplete: (value: string) => void;
  secret?: boolean;
  autoComplete: 'current-password' | 'new-password' | 'one-time-code';
  invalid?: boolean;
  disabled?: boolean;
  describedBy?: string;
}) {
  const input = useRef<HTMLInputElement>(null);

  // Шаг открылся или проверка закончилась — рука уже на цифрах, искать поле мышью не нужно.
  useEffect(() => {
    if (!disabled) {
      input.current?.focus();
    }
  }, [disabled]);

  return (
    <div className="auth-pin" data-invalid={invalid || undefined}>
      <input
        ref={input}
        id={id}
        className="auth-pin-input"
        type={secret ? 'password' : 'text'}
        inputMode="numeric"
        pattern="[0-9]*"
        maxLength={PIN_LENGTH}
        autoComplete={autoComplete}
        spellCheck={false}
        aria-label={label}
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        value={value}
        disabled={disabled}
        onChange={(event) => {
          const next = keepPinDigits(event.currentTarget.value);
          onChange(next);
          if (next.length === PIN_LENGTH && value.length < PIN_LENGTH) {
            onComplete(next);
          }
        }}
      />
      <div className="auth-pin-cells" aria-hidden>
        {Array.from({ length: PIN_LENGTH }, (_, index) => (
          <span
            key={index}
            className="auth-pin-cell"
            data-filled={index < value.length || undefined}
            data-next={index === value.length || undefined}
          >
            {index < value.length ? (secret ? <span className="auth-pin-dot" /> : value[index]) : null}
          </span>
        ))}
      </div>
    </div>
  );
}
