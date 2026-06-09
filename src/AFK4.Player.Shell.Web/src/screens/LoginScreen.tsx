import { useState, type FormEvent } from 'react';

export interface LoginScreenProps {
  /** returns true on success, false on bad credentials */
  onSubmit: (phoneNumber: string, password: string) => Promise<boolean>;
}

export function LoginScreen({ onSubmit }: LoginScreenProps) {
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handle(e: FormEvent) {
    e.preventDefault();
    setPending(true);
    setFailed(false);
    const ok = await onSubmit(phone.trim(), password).catch(() => false);
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
      <label htmlFor="password">Пароль</label>
      <input
        id="password"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      {failed && <p role="alert">Неверный телефон или пароль</p>}
      <button type="submit" disabled={pending || !phone || !password}>
        Войти
      </button>
    </form>
  );
}
