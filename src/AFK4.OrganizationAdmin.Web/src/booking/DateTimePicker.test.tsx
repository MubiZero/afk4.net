import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { DateTimePicker } from './DateTimePicker';

afterEach(cleanup);

function renderPicker(onChange: (v: string) => void) {
  return render(
    <I18nProvider>
      <DateTimePicker value="2026-06-18T13:00" ariaLabel="Старт" onChange={onChange} />
    </I18nProvider>
  );
}

describe('DateTimePicker', () => {
  it('сдвигает дату стрелками, сохраняя время', () => {
    const onChange = mock((_v: string) => {});
    const { getByRole } = renderPicker(onChange);

    fireEvent.click(getByRole('button', { name: 'Следующий день' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-06-19T13:00');

    fireEvent.click(getByRole('button', { name: 'Предыдущий день' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-06-17T13:00');
  });

  it('выбирает час из дропдауна, сохраняя дату и минуты', () => {
    const onChange = mock((_v: string) => {});
    const { getAllByRole, getByRole } = renderPicker(onChange);

    const [hours] = getAllByRole('combobox'); // первый — часы, второй — минуты
    fireEvent.click(hours);
    fireEvent.click(getByRole('option', { name: '20' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-06-18T20:00');
  });

  it('выбирает минуты шагом 15', () => {
    const onChange = mock((_v: string) => {});
    const { getAllByRole, getByRole } = renderPicker(onChange);

    const minutes = getAllByRole('combobox')[1];
    fireEvent.click(minutes);
    fireEvent.click(getByRole('option', { name: '45' }));
    expect(onChange).toHaveBeenLastCalledWith('2026-06-18T13:45');
  });
});
