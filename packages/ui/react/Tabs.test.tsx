import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it } from 'bun:test';
import { Segmented, Tabs } from './Tabs';

type Tab = 'clubs' | 'invoices' | 'history';
const TABS = [
  { value: 'clubs' as const, label: 'Клубы' },
  { value: 'invoices' as const, label: 'Счета', count: 3 },
  { value: 'history' as const, label: 'Журнал' },
];

function Harness({ initial = 'clubs' }: { initial?: Tab }) {
  const [tab, setTab] = useState<Tab>(initial);
  return <Tabs label="Разделы организации" items={TABS} value={tab} onChange={setTab} />;
}

describe('Tabs', () => {
  it('is a labelled tablist with the chosen tab selected', () => {
    render(<Harness initial="invoices" />);
    expect(screen.getByRole('tablist', { name: 'Разделы организации' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Счета 3' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Клубы' })).toHaveAttribute('aria-selected', 'false');
  });

  it('puts only the selected tab in the tab order', () => {
    render(<Harness initial="invoices" />);
    expect(screen.getAllByRole('tab').map((tab) => tab.tabIndex)).toEqual([-1, 0, -1]);
  });

  it('follows arrows, Home and End with focus and selection', () => {
    render(<Harness />);
    const [clubs, invoices, history] = screen.getAllByRole('tab');
    fireEvent.keyDown(clubs, { key: 'ArrowRight' });
    expect(invoices).toHaveFocus();
    expect(invoices).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(invoices, { key: 'End' });
    expect(history).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(history, { key: 'ArrowRight' });
    expect(clubs).toHaveFocus();
    fireEvent.keyDown(clubs, { key: 'ArrowLeft' });
    expect(history).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(history, { key: 'Home' });
    expect(clubs).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps the strip reachable while nothing is selected yet', () => {
    render(<Tabs label="Разделы" items={TABS} value={'none' as Tab} onChange={() => {}} />);
    expect(screen.getAllByRole('tab')[0].tabIndex).toBe(0);
  });
});

describe('Segmented', () => {
  function Pay() {
    const [method, setMethod] = useState<'cash' | 'card' | 'qr'>('cash');
    return (
      <Segmented
        label="Способ оплаты"
        value={method}
        onChange={setMethod}
        options={[{ value: 'cash', label: 'Наличные' }, { value: 'card', label: 'Карта', disabled: true }, { value: 'qr', label: 'QR' }]}
      />
    );
  }

  it('reads as a radio group, not as navigation', () => {
    render(<Pay />);
    expect(screen.getByRole('radiogroup', { name: 'Способ оплаты' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Наличные' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('skips a disabled option with the arrows', () => {
    render(<Pay />);
    fireEvent.keyDown(screen.getByRole('radio', { name: 'Наличные' }), { key: 'ArrowRight' });
    expect(screen.getByRole('radio', { name: 'QR' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: 'QR' })).toHaveFocus();
  });
});
