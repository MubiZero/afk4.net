import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '../i18n/I18nProvider';
import { AccountActivation } from './AccountActivation';

it('activates the owner and directs them to Organization Admin without exposing a staff session', async () => {
  const accept = mock(async () => {});
  render(<I18nProvider><AccountActivation client={{ accept } as never} initialCode=" invite-1 " /></I18nProvider>);

  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: ' owner@example.test ' } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
  fireEvent.change(screen.getByLabelText('Повторите пароль'), { target: { value: 'Passw0rd!' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать владельца' }));

  await waitFor(() => expect(accept).toHaveBeenCalledWith({
    code: 'invite-1',
    userName: 'owner@example.test',
    displayName: '',
    password: 'Passw0rd!'
  }));
  expect(screen.getByRole('heading', { name: 'Владелец активирован' })).toBeInTheDocument();
  expect(screen.getByText('Теперь войдите с этой учётной записью в Organization Admin.')).toBeInTheDocument();
});
