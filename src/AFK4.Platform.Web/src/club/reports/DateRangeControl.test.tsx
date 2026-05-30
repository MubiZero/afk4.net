import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { DateRangeControl } from './DateRangeControl';
import type { DateRange } from './reportsModel';

const range: DateRange = { fromUtc: '2026-05-30T00:00:00.000Z', toUtc: '2026-05-30T23:59:59.000Z' };

it('emits a new range when the from-date input changes', () => {
  const onChange = vi.fn();
  render(<I18nProvider><DateRangeControl value={range} onChange={onChange} /></I18nProvider>);
  fireEvent.change(screen.getByLabelText('С'), { target: { value: '2026-05-01' } });
  expect(onChange).toHaveBeenCalledWith({
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-30T23:59:59.000Z'
  });
});

it('emits a preset range when a preset button is clicked', () => {
  const onChange = vi.fn();
  render(<I18nProvider><DateRangeControl value={range} onChange={onChange} /></I18nProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Сегодня' }));
  expect(onChange).toHaveBeenCalledTimes(1);
});
