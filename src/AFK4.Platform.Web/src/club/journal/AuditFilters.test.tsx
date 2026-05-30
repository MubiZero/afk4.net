import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AuditFilters } from './AuditFilters';
import { presetRange, type DateRange } from '../reports/reportsModel';

const range: DateRange = presetRange('today', new Date('2026-05-30T12:00:00.000Z'));

it('applies the typed action filter', () => {
  const onApply = vi.fn();
  render(<I18nProvider>
    <AuditFilters range={range} onRangeChange={() => {}} onApply={onApply} onReset={() => {}} />
  </I18nProvider>);
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'login' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  expect(onApply).toHaveBeenCalledWith(expect.objectContaining({ action: 'login' }));
});

it('resets the draft', () => {
  const onReset = vi.fn();
  render(<I18nProvider>
    <AuditFilters range={range} onRangeChange={() => {}} onApply={() => {}} onReset={onReset} />
  </I18nProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить' }));
  expect(onReset).toHaveBeenCalled();
});
