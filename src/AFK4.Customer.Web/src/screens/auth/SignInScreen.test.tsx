import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { SignInScreen } from './SignInScreen';

it('submits phone + password and reports the resulting session', async () => {
  const onSignedIn = mock();
  const signIn = mock().mockResolvedValue({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  });

  render(<SignInScreen organizationId="org1" brandName="CyberX" signIn={signIn} onSignedIn={onSignedIn} />);
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000001' } });
  fireEvent.change(screen.getByLabelText('PIN или пароль'), { target: { value: '1234' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));

  await waitFor(() => expect(onSignedIn).toHaveBeenCalled());
  expect(signIn).toHaveBeenCalledWith({ organizationId: 'org1', phoneNumber: '+992900000001', password: '1234' });
});

it('shows a generic error when sign-in fails', async () => {
  const signIn = mock().mockRejectedValue(new Error('nope'));
  render(<SignInScreen organizationId="org1" brandName="CyberX" signIn={signIn} onSignedIn={() => {}} />);
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000001' } });
  fireEvent.change(screen.getByLabelText('PIN или пароль'), { target: { value: 'x' } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Неверный номер или пароль');
});
