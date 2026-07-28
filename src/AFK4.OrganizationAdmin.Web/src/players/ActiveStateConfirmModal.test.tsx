import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ActiveStateConfirmModal } from './ActiveStateConfirmModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof ActiveStateConfirmModal>[0]> = {}) => {
  const onConfirm = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ActiveStateConfirmModal mode="deactivate" onClose={() => {}} onConfirm={onConfirm} busy={false} {...over} />
    </I18nProvider>
  );
  return { onConfirm };
};

describe('ActiveStateConfirmModal', () => {
  it('shows deactivate copy in deactivate mode', () => {
    renderModal();
    expect(screen.getByText('Деактивировать клиента?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Деактивировать/ })).toBeInTheDocument();
  });

  it('shows reactivate copy in reactivate mode', () => {
    renderModal({ mode: 'reactivate' });
    expect(screen.getByText('Активировать клиента?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Активировать/ })).toBeInTheDocument();
  });

  it('fires onConfirm on confirm click', () => {
    const { onConfirm } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Деактивировать/ }));
    expect(onConfirm).toHaveBeenCalled();
  });

  it('disables confirm when busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Деактивировать/ })).toBeDisabled();
  });
});
