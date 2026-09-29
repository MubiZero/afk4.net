import { describe, it, expect, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ManagementScreen } from './ManagementScreen';

const renderScreen = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

describe('ManagementScreen', () => {
  // Шапка — общая шапка раздела кита: название, главная кнопка раздела и вкладки под ними. Мелкой
  // строки над названием больше нет (решение владельца 29.09) — в шапке только то, что передали.
  it('renders the section header with its action and tabs, then the body', () => {
    const { container } = renderScreen(
      <ManagementScreen title="Товары" action={<button type="button">+ Товар</button>} tabs={<div role="tablist" aria-label="Вкладки" />}>
        <p>тело</p>
      </ManagementScreen>
    );
    const header = container.querySelector('.ui-section-header');
    expect(header?.querySelector('h1')?.textContent).toBe('Товары');
    expect(header?.querySelector('button')?.textContent).toBe('+ Товар');
    expect(header?.querySelector('[role="tablist"]')).toBeTruthy();
    expect(screen.getByText('тело')).toBeTruthy();
  });

  it('disables save while clean and enables it while dirty', () => {
    const { rerender } = renderScreen(
      <ManagementScreen title="t" save={{ state: 'clean', onSave: () => {} }}><i/></ManagementScreen>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(true);
    rerender(
      <I18nProvider initialLocale="ru">
        <ManagementScreen title="t" save={{ state: 'dirty', onSave: () => {} }}><i/></ManagementScreen>
      </I18nProvider>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(false);
  });

  it('omits the save bar when no save prop is given', () => {
    renderScreen(<ManagementScreen title="t"><i/></ManagementScreen>);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).toBeNull();
  });

  it('renders the screen\'s own skeleton instead of children, with no save bar, when state is loading', async () => {
    const { container } = renderScreen(
      <ManagementScreen title="t" state="loading" skeleton={<div data-skeleton="table" />} save={{ state: 'dirty', onSave: () => {} }}>
        <p>тело</p>
      </ManagementScreen>
    );
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(screen.queryByText('тело')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Сохранить' })).toBeNull();
  });

  // Быстрый ответ не должен мигать ожиданием: первые 180 мс на месте тела ничего нет, и ответ,
  // пришедший раньше, подменяет пустоту содержимым без промежуточной заглушки. То же правило, что
  // у Platform Control.
  it('holds the skeleton back for an instant so quick answers do not flash', async () => {
    const { container } = renderScreen(
      <ManagementScreen title="t" state="loading" skeleton={<div data-skeleton="table" />}><p>тело</p></ManagementScreen>
    );
    expect(container.querySelector('[data-skeleton]')).toBeNull();
    await new Promise((resolve) => setTimeout(resolve, 120));
    expect(container.querySelector('[data-skeleton]')).toBeNull();
    await new Promise((resolve) => setTimeout(resolve, 120));
    expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy();
  });

  // Право известно до ответа: строка «только просмотр» стоит над заглушкой так же, как встанет над
  // содержимым, а не вдвигается сверху в момент подмены.
  it('keeps the view-only line above the skeleton while loading', () => {
    renderScreen(
      <ManagementScreen title="t" state="loading" skeleton={null} viewOnly="Менять может владелец."><p>тело</p></ManagementScreen>
    );
    expect(screen.getByText('Менять может владелец.', { exact: false })).toBeTruthy();
  });

  it('renders the concrete error detail and a retry button that calls onRetry when state is error', () => {
    const onRetry = mock(() => {});
    renderScreen(
      <ManagementScreen title="t" state="error" skeleton={null} failure={{ title: '', detail: 'boom', retryCanHelp: true }} onRetry={onRetry}>
        <p>тело</p>
      </ManagementScreen>
    );
    expect(screen.getByText('boom')).toBeTruthy();
    expect(screen.queryByText('тело')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders children as usual when state is ready or omitted (regression)', () => {
    renderScreen(<ManagementScreen title="t" state="ready" skeleton={null}><p>тело</p></ManagementScreen>);
    expect(screen.getByText('тело')).toBeTruthy();
  });
});
