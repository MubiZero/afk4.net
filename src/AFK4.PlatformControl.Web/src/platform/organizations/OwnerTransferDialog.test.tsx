import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OwnerTransferDialog } from './OwnerTransferDialog';

function renderDialog() {
  render(
    <I18nProvider>
      <ToastProvider>
        <OwnerTransferDialog client={{ transferOwner: mock() } as never} organizationId="org-1" onClose={() => {}} onTransferred={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
}

const REASON = 'Проверьте адрес почты: он должен выглядеть как name@example.com.';

describe('OwnerTransferDialog', () => {
  // Пустое поле видно и так. А адрес без домена выглядит заполненным, и кнопка гасла молча.
  it('адрес почты с ошибкой называет у кнопки', () => {
    renderDialog();
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Продажа клуба' } });

    fireEvent.change(screen.getByLabelText('Email нового владельца'), { target: { value: 'owner@orion' } });
    const submit = screen.getByRole('button', { name: 'Передать' });
    expect(submit).toBeDisabled();
    expect(submit.getAttribute('aria-describedby')).toBe(screen.getByText(REASON).id);

    fireEvent.change(screen.getByLabelText('Email нового владельца'), { target: { value: 'owner@orion.tj' } });
    expect(submit).toBeEnabled();
    expect(submit.getAttribute('aria-describedby')).toBeNull();
    expect(screen.queryByText(REASON)).toBeNull();
  });

  it('пока адрес не начат, о нём не говорит', () => {
    renderDialog();

    expect(screen.getByRole('button', { name: 'Передать' })).toBeDisabled();
    expect(screen.queryByText(REASON)).toBeNull();
  });
});
