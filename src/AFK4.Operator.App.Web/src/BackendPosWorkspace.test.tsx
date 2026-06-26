import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BackendPosWorkspace } from './BackendPosWorkspace';
import { ToastProvider } from './operatorToast';

afterEach(cleanup);

// backend=null → fixture-режим: каталог/корзина из заглушек, без сетевых запросов.
function renderPos(embedded: boolean) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPosWorkspace currencyCode="TJS" backend={null} embedded={embedded} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('BackendPosWorkspace', () => {
  it('standalone: рендерит шапку «Продажи» и панели каталог + продажа (корзина+оплата)', () => {
    renderPos(false);
    expect(screen.getByRole('heading', { name: /Продажи/ })).toBeInTheDocument();
    expect(document.querySelector('main.pos-screen')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    // Оплата слита в колонку «продажи» — отдельной панели нет, остаётся действие.
    expect(document.querySelector('.pos-sale-panel')).not.toBeNull();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
  });

  it('embedded: без собственного <main>/heading, корень — section.pos-embed, панели на месте', () => {
    renderPos(true);
    // Заголовок «Продажи» даёт сегмент-вкладка, не сам POS → собственного heading нет.
    expect(screen.queryByRole('heading', { name: /Продажи/ })).toBeNull();
    expect(document.querySelector('main.pos-screen')).toBeNull();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(document.querySelector('.pos-sale-panel')).not.toBeNull();
  });
});
