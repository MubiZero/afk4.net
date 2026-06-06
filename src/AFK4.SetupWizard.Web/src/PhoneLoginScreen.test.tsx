import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const signInByPhone = mock(async () => ({ displayName: 'Сотрудник' }));
const discoverAuthenticated = mock(async () => ({
  ownerName: 'Сотрудник',
  branches: [
    {
      branchId: '11111111-1111-1111-1111-111111111111',
      branchSlug: 'main',
      branchName: 'Главный',
      zones: [],
      seats: [],
      freeSeatIds: [],
    },
  ],
}));

mock.module('./wizardApi', () => ({ signInByPhone, discoverAuthenticated }));

const { PhoneLoginScreen } = await import('./PhoneLoginScreen');

function renderScreen(props: Partial<Parameters<typeof PhoneLoginScreen>[0]> = {}) {
  const onDiscovered = mock(() => {});
  const onUseOwnerCode = mock(() => {});
  render(
    <I18nProvider>
      <PhoneLoginScreen onDiscovered={onDiscovered} onUseOwnerCode={onUseOwnerCode} {...props} />
    </I18nProvider>,
  );
  return { onDiscovered, onUseOwnerCode };
}

describe('PhoneLoginScreen', () => {
  beforeEach(() => {
    signInByPhone.mockClear();
    discoverAuthenticated.mockClear();
  });

  it('signs in then discovers and reports branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/номер телефона/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText(/пароль/i), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('routes to the owner-code fallback', () => {
    const { onUseOwnerCode } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /коду владельца/i }));
    expect(onUseOwnerCode).toHaveBeenCalledTimes(1);
  });
});
