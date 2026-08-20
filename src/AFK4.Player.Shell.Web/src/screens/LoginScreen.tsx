import { useState, type FormEvent } from 'react';

export interface LoginScreenProps {
  /** returns true on success, false on bad credentials */
  onSubmit: (phoneNumber: string, pin: string) => Promise<boolean>;
}

// Где живёт PIN — постоянная подсказка, а не объявление: человек за игровым ПК должен видеть,
// куда идти, если PIN он не помнит. Отказ во входе один на все причины — сервер не называет,
// знаком ли ему номер, — поэтому и текст отказа один и тот же для любого введённого номера.
const PIN_HINT = 'PIN один на все клубы сети — задать или сменить его можно в приложении, в профиле.';
const SIGN_IN_FAILED = 'Неверный телефон или PIN.';
const SIGN_IN_FAILED_FALLBACK = 'Нет приложения под рукой — позовите администратора, он посадит вас за ПК.';

export function LoginScreen({ onSubmit }: LoginScreenProps) {
  const [phone, setPhone] = useState('');
  const [pin, setPin] = useState('');
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handle(e: FormEvent) {
    e.preventDefault();
    setPending(true);
    setFailed(false);
    const ok = await onSubmit(phone.trim(), pin).catch(() => false);
    setPending(false);
    if (!ok) setFailed(true);
  }

  return (
    <form onSubmit={handle} aria-label="login">
      <h1>Войти</h1>
      <label htmlFor="phone">Телефон</label>
      <input
        id="phone"
        inputMode="tel"
        autoComplete="username"
        value={phone}
        onChange={(e) => setPhone(e.target.value)}
      />
      <label htmlFor="pin">PIN</label>
      <input
        id="pin"
        type="password"
        inputMode="numeric"
        autoComplete="current-password"
        value={pin}
        onChange={(e) => setPin(e.target.value)}
      />
      <p>{PIN_HINT}</p>
      {failed && (
        <p role="alert">
          {SIGN_IN_FAILED} {PIN_HINT} {SIGN_IN_FAILED_FALLBACK}
        </p>
      )}
      <button type="submit" disabled={pending || !phone || !pin}>
        Войти
      </button>
    </form>
  );
}
