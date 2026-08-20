import { useState, type FormEvent } from 'react';

export interface LoginScreenProps {
  /** returns true on success, false on bad credentials */
  onSubmit: (phoneNumber: string, password: string) => Promise<boolean>;
}

// Объяснение перехода на сетевой PIN. Сервер отвечает на любую неудачу входа одинаково — иначе по
// экрану игрового ПК можно проверять, у кого в сети есть аккаунт, — поэтому объясняет оболочка, и
// объясняет всем одно и то же, независимо от того, что было введено. Уходит вместе с обновлением
// оболочки, когда переход закончится (30 дней и 90% задавших PIN).
const PIN_CHANGED_TITLE = 'PIN изменился';
const PIN_CHANGED_TEXT =
  'Теперь он один на все клубы сети, и задаёте его вы сами — в приложении, в профиле.';
const PIN_CHANGED_FALLBACK = 'Нет приложения под рукой — позовите администратора, он посадит вас за ПК.';

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
      <p>
        <strong>{PIN_CHANGED_TITLE}.</strong> {PIN_CHANGED_TEXT}
      </p>
      {failed && (
        <p role="alert">
          <strong>{PIN_CHANGED_TITLE}.</strong> {PIN_CHANGED_TEXT} {PIN_CHANGED_FALLBACK}
        </p>
      )}
      <button type="submit" disabled={pending || !phone || !pin}>
        Войти
      </button>
    </form>
  );
}
