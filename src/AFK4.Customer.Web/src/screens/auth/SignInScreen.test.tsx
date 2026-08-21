import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { SignInScreen } from './SignInScreen';
import { PlayerApiError } from '@/api/playerApi';

const started = { expiresInSeconds: 300, resendAfterSeconds: 60 };
const sessionResponse = {
  playerAccountId: null, organizationId: null, displayName: '', phoneVerified: true,
  accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z',
  platformPersonId: 'person1', preferredLocale: null, profileCompleted: false
};

function renderScreen(overrides: {
  startSignIn?: ReturnType<typeof mock>;
  confirmSignIn?: ReturnType<typeof mock>;
  onSignedIn?: ReturnType<typeof mock>;
} = {}) {
  const startSignIn = overrides.startSignIn ?? mock().mockResolvedValue(started);
  const confirmSignIn = overrides.confirmSignIn ?? mock().mockResolvedValue(sessionResponse);
  const onSignedIn = overrides.onSignedIn ?? mock();
  render(
    <I18nProvider>
      <SignInScreen brandName="CyberX" startSignIn={startSignIn} confirmSignIn={confirmSignIn} onSignedIn={onSignedIn} />
    </I18nProvider>
  );
  return { startSignIn, confirmSignIn, onSignedIn };
}

async function askForCode(phone = '+992900000001') {
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: phone } });
  fireEvent.click(screen.getByRole('button', { name: 'Прислать код' }));
  return await screen.findByLabelText('Код из SMS');
}

it('просит код на номер, не спрашивая ни пароля, ни клуба', async () => {
  const { startSignIn } = renderScreen();
  expect(screen.queryByLabelText(/пароль/i)).not.toBeInTheDocument();
  await askForCode();
  expect(startSignIn).toHaveBeenCalledWith({ phoneNumber: '+992900000001' });
  expect(await screen.findByText(/Код отправлен на \+992900000001/)).toBeInTheDocument();
});

it('обменивает код на сессию и отдаёт её приложению', async () => {
  const { confirmSignIn, onSignedIn } = renderScreen();
  const code = await askForCode();
  fireEvent.change(code, { target: { value: '4321' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
  await waitFor(() => expect(onSignedIn).toHaveBeenCalledWith(sessionResponse));
  expect(confirmSignIn).toHaveBeenCalledWith({ phoneNumber: '+992900000001', code: '4321' });
});

// Незнакомый номер входит той же дверью — иначе сама пара «регистрация / вход» рассказывала бы
// звонящему, знаком ли нам его номер.
it('обещает новому человеку тот же вход по номеру', () => {
  renderScreen();
  expect(screen.getByText(/Впервые здесь/)).toBeInTheDocument();
});

it('называет неверный код своим именем, а не общей ошибкой', async () => {
  const confirmSignIn = mock().mockRejectedValue(new PlayerApiError(400, 'invalid_code'));
  renderScreen({ confirmSignIn });
  const code = await askForCode();
  fireEvent.change(code, { target: { value: '0000' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Неверный код');
});

it('устаревший код зовёт запросить новый', async () => {
  const confirmSignIn = mock().mockRejectedValue(new PlayerApiError(410, 'code_expired'));
  renderScreen({ confirmSignIn });
  const code = await askForCode();
  fireEvent.change(code, { target: { value: '0000' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Код устарел');
});

it('кривой номер поправляют на шаге номера, а не после SMS', async () => {
  const startSignIn = mock().mockRejectedValue(new PlayerApiError(400, 'invalid_phone'));
  renderScreen({ startSignIn });
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '123' } });
  fireEvent.click(screen.getByRole('button', { name: 'Прислать код' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Проверьте номер телефона');
  expect(screen.queryByLabelText('Код из SMS')).not.toBeInTheDocument();
});

it('позволяет вернуться и ввести другой номер', async () => {
  renderScreen();
  await askForCode();
  fireEvent.click(screen.getByRole('button', { name: 'Другой номер' }));
  expect(await screen.findByLabelText('Телефон')).toBeInTheDocument();
  expect(screen.queryByLabelText('Код из SMS')).not.toBeInTheDocument();
});
