import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { CashMetricStrip, CashRegisterRows, CashTerminalSplit } from './CashTerminalFrame';

afterEach(cleanup);

describe('CashMetricStrip', () => {
  it('renders decision metrics with explicit tones', () => {
    render(<CashMetricStrip ariaLabel="Ключевые показатели смены" items={[
      { label: 'Выручка', value: '1 230 с.' },
      { label: 'Расхождение', value: '−50 с.', tone: 'danger', detail: 'Требует сверки' }
    ]} />);
    expect(screen.getByLabelText('Ключевые показатели смены')).toBeInTheDocument();
    expect(screen.getByText('−50 с.').closest('.cash-terminal-metric')).toHaveClass('tone-danger');
    expect(screen.getByText('Требует сверки')).toBeInTheDocument();
  });
});

describe('CashRegisterRows', () => {
  const rows = [{ id: 'a' }, { id: 'b' }];

  it('selects a row without changing its semantic role', () => {
    const onSelect = mock();
    render(<CashRegisterRows rows={rows} selectedId="a" getId={(row) => row.id} renderRow={(row) => <span>{row.id}</span>} onSelect={onSelect} ariaLabel="Операции" />);
    fireEvent.click(screen.getByRole('row', { name: 'b' }));
    expect(onSelect).toHaveBeenCalledWith('b');
    expect(screen.getByRole('row', { name: 'a' })).toHaveAttribute('aria-selected', 'true');
  });

  it('moves focus and selection with ArrowDown', () => {
    const onSelect = mock();
    render(<CashRegisterRows rows={rows} selectedId="a" getId={(row) => row.id} renderRow={(row) => <span>{row.id}</span>} onSelect={onSelect} ariaLabel="Операции" />);
    const first = screen.getByRole('row', { name: 'a' });
    first.focus();
    fireEvent.keyDown(first, { key: 'ArrowDown' });
    expect(onSelect).toHaveBeenCalledWith('b');
    expect(screen.getByRole('row', { name: 'b' })).toHaveFocus();
  });
});

describe('CashTerminalSplit', () => {
  it('renders a stable inspector and closes it explicitly', () => {
    const onClose = mock();
    render(<CashTerminalSplit register={<div>Реестр</div>} inspector={<div>Деталь</div>} inspectorOpen closeLabel="Закрыть детали" onCloseInspector={onClose} />);
    expect(screen.getByLabelText('Детали выбранной записи')).toHaveTextContent('Деталь');
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть детали' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('closes an open inspector with Escape', () => {
    const onClose = mock(() => {});
    render(<CashTerminalSplit register={<div>Register</div>} inspector={<div>Detail</div>} inspectorOpen closeLabel="Close" onCloseInspector={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
