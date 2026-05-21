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
    expect(screen.getByText('Сессии')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Техрежим/ })).toBeInTheDocument();
    expect(screen.getByText('Сессия активна')).toBeInTheDocument();
    expect(screen.getByText('Сессия подтверждена')).toBeInTheDocument();
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
    expect(screen.getAllByText(/Депозит/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/USD/).length).toBeGreaterThan(0);
  });

  it('switches to SmartShell-like booking, POS, and logs workspaces', () => {
    render(<App />);

    fireEvent.click(screen.getByTitle('Дашборд'));
    expect(screen.getByRole('heading', { name: /Что требует внимания/ })).toBeInTheDocument();
    expect(screen.getByText('Главный фокус')).toBeInTheDocument();
    expect(screen.getByText('PC-11 · блокировка не подтверждена')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Сегодня' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Неделя' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Месяц' })).toBeInTheDocument();
    expect(screen.getByLabelText('Начало периода')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Экспорт дашборда за/ })).toBeInTheDocument();
    expect(screen.getByText('Пульс смены')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Неделя' }));
    expect(screen.getByText('14 чеков')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Брони'));
    const bookingHead = screen.getByRole('heading', { name: /Брони/ }).closest('.screen-head');
    expect(bookingHead).toBeInTheDocument();
    expect(bookingHead).not.toHaveTextContent('Сегодня');
    expect(bookingHead).not.toHaveTextContent('Завтра');
    expect(bookingHead).not.toHaveTextContent('Неделя');
    expect(screen.getByText('Лента броней')).toBeInTheDocument();
    expect(screen.getByText('Выбранная бронь')).toBeInTheDocument();
    expect(screen.getByText('Онлайн-заявки')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Открыть карту/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Создать бронь/ })).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('POS'));
    const posHead = screen.getByRole('heading', { name: /POS/ }).closest('.screen-head');
    expect(posHead).toBeInTheDocument();
    expect(posHead).not.toHaveTextContent('Продажа');
    expect(posHead).not.toHaveTextContent('Возврат');
    expect(posHead).not.toHaveTextContent('Склад');
    expect(posHead).not.toHaveTextContent('История');
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
    expect(screen.getByText('Последние чеки')).toBeInTheDocument();
    expect(screen.getByText('Быстрые операции')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Клиенты'));
    const clientsHead = screen.getByRole('heading', { name: /Клиенты/ }).closest('.screen-head');
    expect(clientsHead).toBeInTheDocument();
    expect(clientsHead).not.toHaveTextContent('Все');
    expect(clientsHead).not.toHaveTextContent('VIP');
    expect(clientsHead).not.toHaveTextContent('Долги');
    expect(screen.getByText('Список клиентов')).toBeInTheDocument();
    expect(screen.getByText('Карточка клиента')).toBeInTheDocument();
    expect(screen.getByText('Операции')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeInTheDocument();
    expect(screen.getByText('История клиента')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Платежи'));
    const paymentsHead = screen.getByRole('heading', { name: /Платежи/ }).closest('.screen-head');
    expect(paymentsHead).toBeInTheDocument();
    expect(paymentsHead).not.toHaveTextContent('Смена');
    expect(paymentsHead).not.toHaveTextContent('Операции');
    expect(paymentsHead).not.toHaveTextContent('Сверка');
    expect(paymentsHead).not.toHaveTextContent('Экспорт');
    expect(screen.getByText('Операции смены')).toBeInTheDocument();
    expect(screen.getByText('Итоги смены')).toBeInTheDocument();
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Подготовить закрытие/ })).toBeInTheDocument();
    expect(screen.getByText('Методы оплаты')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Логи'));
    const logsHead = screen.getByRole('heading', { name: /Логи/ }).closest('.screen-head');
    expect(logsHead).toBeInTheDocument();
    expect(logsHead).not.toHaveTextContent('Смена');
    expect(logsHead).not.toHaveTextContent('Ошибки');
    expect(logsHead).not.toHaveTextContent('Аудит');
    expect(logsHead).not.toHaveTextContent('Экспорт');
    expect(screen.getByText('Журнал событий')).toBeInTheDocument();
    expect(screen.getByText('Детали события')).toBeInTheDocument();
    expect(screen.getByText('Фильтры')).toBeInTheDocument();
    expect(screen.getByText('Аудит смены')).toBeInTheDocument();
    expect(screen.getByText('Источники')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Audit trail/ })).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Настройки'));
    const settingsHead = screen.getByRole('heading', { name: /Настройки/ }).closest('.screen-head');
    expect(settingsHead).toBeInTheDocument();
    expect(settingsHead).not.toHaveTextContent('Основное');
    expect(settingsHead).not.toHaveTextContent('Залы и ПК');
    expect(screen.getAllByText('Профиль клуба').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    expect(screen.getByText('Залы и рабочие места')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    expect(screen.getAllByText('Тарифы').length).toBeGreaterThan(0);
    expect(screen.getByText('Готовность клуба')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Пригласить сотрудника/ })).toBeInTheDocument();
  });
});
