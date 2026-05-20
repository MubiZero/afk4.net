import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { App } from './App';

describe('App', () => {
  afterEach(() => {
    cleanup();
    delete window.__AFK4_OPERATOR_CONFIG__;
  });

  it('opens on the floor map operator workspace', () => {
    render(<App />);

    expect(screen.getByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
    expect(screen.getByLabelText('ПК зала')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /15 мин/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Свернуть' })).toBeInTheDocument();
  });

  it('uses the host currency in money surfaces', () => {
    window.__AFK4_OPERATOR_CONFIG__ = {
      runtime: 'webview2',
      shellMode: 'vite-dist',
      platformBaseUrl: 'https://afk4.staging.mubi.dev/',
      currencyCode: 'USD'
    };

    render(<App />);

    expect(screen.getByText('4 820 USD')).toBeInTheDocument();
    expect(screen.getAllByText(/Wallet/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/USD/).length).toBeGreaterThan(0);
  });

  it('switches to SmartShell-like booking, POS, and logs workspaces', () => {
    render(<App />);

    fireEvent.click(screen.getByTitle('Брони'));
    expect(screen.getByRole('heading', { name: /Брони/ })).toBeInTheDocument();
    expect(screen.getByText('Лента бронирований')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('POS'));
    expect(screen.getByRole('heading', { name: /POS/ })).toBeInTheDocument();
    expect(screen.getByText('Каталог')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Логи'));
    expect(screen.getByRole('heading', { name: /Логи/ })).toBeInTheDocument();
    expect(screen.getByText('Журнал событий')).toBeInTheDocument();
  });
});
