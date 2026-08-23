import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

const acceptStaffInvite = mock(async () => ({ organizationId: 'o1', userName: 'new.cashier' }));

// Тот же приём, что в ForgotPassword.test: снимок настоящего модуля до подмены и возврат его
// в afterAll — иначе заглушки протекают в соседние файлы и роняют их по порядку запуска.
const realAuthClient = { ...(await import('./authClient')) };

mock.module('./authClient', () => ({ ...realAuthClient, acceptStaffInvite }));

const { AcceptInvite } = await import('./AcceptInvite');

afterAll(() => {
  mock.module('./authClient', () => realAuthClient);
});

afterEach(() => acceptStaffInvite.mockClear());

function renderScreen() {
  return render(
    <I18nProvider>
      <AcceptInvite onBackToSignIn={() => {}} />
    </I18nProvider>
  );
}

function fill(phone: string, code: string, password: string) {
  fireEvent.change(screen.getByPlaceholderText('93 738 00 70'), { target: { value: phone } });
  fireEvent.change(screen.getByLabelText('Код из SMS'), { target: { value: code } });
  fireEvent.change(screen.getByLabelText('Ваш пароль'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Принять приглашение' }));
}

describe('AcceptInvite (operator)', () => {
  it('принимает приглашение номером, кодом и своим паролем', async () => {
    renderScreen();

    fill('937380070', '123456', 'FreshPass123');

    await waitFor(() =>
      expect(acceptStaffInvite).toHaveBeenCalledWith('992937380070', '123456', 'FreshPass123'));
    expect(await screen.findByText(/Теперь входите по своему номеру/u)).toBeTruthy();
  });

  // Короткий пароль сервер всё равно отвергнет — экран говорит это сразу, не тратя человеку
  // попытку кода, которых всего три.
  it('не отправляет короткий пароль на сервер', async () => {
    renderScreen();

    fill('937380070', '123456', 'short');

    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy());
    expect(acceptStaffInvite).not.toHaveBeenCalled();
  });

  it('не отправляет неполный номер', async () => {
    renderScreen();

    fill('9373', '123456', 'FreshPass123');

    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy());
    expect(acceptStaffInvite).not.toHaveBeenCalled();
  });
});
