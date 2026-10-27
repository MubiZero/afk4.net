import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

const getMyTelegram = mock(async () => ({ botConfigured: true, linked: false, linkedAtUtc: null as string | null }));
const createTelegramLink = mock(async () => ({
  deepLink: 'https://t.me/afk4_bot?start=code',
  expiresAtUtc: new Date(Date.now() + 600_000).toISOString()
}));
const unlinkTelegram = mock(async () => undefined);

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({ account: { getMyTelegram, createTelegramLink, unlinkTelegram } })
}));

const { TelegramReportsCard } = await import('./TelegramReportsCard');

// Как в PhoneVerificationCard.test.tsx: вернуть настоящий модуль остальным файлам прогона.
afterAll(() => {
  mock.module('./operatorApiClients', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorApiClients: typeof import('./operatorApiClients');
  }).__afk4RealOperatorApiClients);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08' }
};

describe('TelegramReportsCard', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('shows a QR with the bot link after Connect', async () => {
    render(<I18nProvider><TelegramReportsCard backend={backend} /></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Подключить' }));

    await waitFor(() => expect(createTelegramLink).toHaveBeenCalled());
    expect(await screen.findByRole('link', { name: 'Открыть ссылку на этом ПК' })).toHaveAttribute('href', 'https://t.me/afk4_bot?start=code');
    expect(await screen.findByAltText('QR-код для подключения Telegram')).toBeInTheDocument();
  });

  it('offers Disconnect when the chat is linked', async () => {
    getMyTelegram.mockImplementationOnce(async () => ({ botConfigured: true, linked: true, linkedAtUtc: '2026-10-27T08:00:00Z' }));
    render(<I18nProvider><TelegramReportsCard backend={backend} /></I18nProvider>);

    fireEvent.click(await screen.findByRole('button', { name: 'Отключить' }));
    await waitFor(() => expect(unlinkTelegram).toHaveBeenCalled());
    expect(await screen.findByRole('button', { name: 'Подключить' })).toBeInTheDocument();
  });

  // Без бота у платформы подключать нечего: кнопка кончилась бы ошибкой.
  it('hides itself when the platform has no bot', async () => {
    getMyTelegram.mockImplementationOnce(async () => ({ botConfigured: false, linked: false, linkedAtUtc: null }));
    const { container } = render(<I18nProvider><TelegramReportsCard backend={backend} /></I18nProvider>);
    await waitFor(() => expect(container.querySelector('.account-telegram')).toBeNull());
    expect(screen.queryByRole('button', { name: 'Подключить' })).toBeNull();
  });
});
