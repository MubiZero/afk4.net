import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider, useToast } from './operatorToast';

const undoSpy = mock(() => {});

afterEach(() => {
  cleanup();
  undoSpy.mockClear();
});

function Harness() {
  const toast = useToast();
  return (
    <div>
      <button onClick={() => toast.success('Сохранено')}>fire-success</button>
      <button onClick={() => toast.error('Ошибка')}>fire-error</button>
      <button onClick={() => toast.info('Готово', { durationMs: 30 })}>fire-info-fast</button>
      <button onClick={() => toast.success('С действием', { action: { label: 'Отменить', onClick: undoSpy } })}>fire-action</button>
      <button onClick={() => { toast.success('a'); toast.success('b'); toast.success('c'); toast.success('d'); }}>fire-four</button>
    </div>
  );
}

function renderHarness() {
  return render(
    <I18nProvider>
      <ToastProvider>
        <Harness />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('Toast', () => {
  it('shows a success toast with status role', () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-success'));
    const toast = screen.getByText('Сохранено').closest('.toast');
    expect(toast).not.toBeNull();
    expect(toast).toHaveAttribute('role', 'status');
  });

  it('renders error toast as assertive alert and does NOT auto-dismiss', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-error'));
    const toast = screen.getByText('Ошибка').closest('.toast');
    expect(toast).toHaveAttribute('role', 'alert');
    await new Promise((resolve) => setTimeout(resolve, 60));
    expect(screen.getByText('Ошибка')).toBeInTheDocument();
  });

  it('auto-dismisses success/info after its duration', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-info-fast'));
    expect(screen.getByText('Готово')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Готово')).not.toBeInTheDocument());
  });

  it('shows at most 3 toasts at once', () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-four'));
    expect(document.querySelectorAll('.toast')).toHaveLength(3);
  });

  it('dismisses a toast via its close button', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-success'));
    fireEvent.click(screen.getByLabelText('Закрыть'));
    await waitFor(() => expect(screen.queryByText('Сохранено')).not.toBeInTheDocument());
  });

  it('runs the optional action and dismisses', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-action'));
    fireEvent.click(screen.getByText('Отменить'));
    expect(undoSpy).toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByText('С действием')).not.toBeInTheDocument());
  });

  it('throws when useToast is used without a provider', () => {
    function Orphan() { useToast(); return null; }
    expect(() => render(<Orphan />)).toThrow(/ToastProvider/);
  });
});
