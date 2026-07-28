import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WorkingHoursEditor } from './WorkingHoursEditor';
import { defaultWorkingHours } from './workingHours';

afterEach(cleanup);

describe('WorkingHoursEditor', () => {
  it('renders 7 day rows', () => {
    render(
      <I18nProvider initialLocale="ru">
        <WorkingHoursEditor value={defaultWorkingHours()} onChange={() => {}} />
      </I18nProvider>
    );
    expect(screen.getByText('Понедельник')).toBeInTheDocument();
    expect(screen.getByText('Воскресенье')).toBeInTheDocument();
  });

  it('toggling closed emits updated day', () => {
    const onChange = mock((_: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <WorkingHoursEditor value={defaultWorkingHours()} onChange={onChange} />
      </I18nProvider>
    );
    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[0]);
    expect(onChange).toHaveBeenCalled();
    const next = onChange.mock.calls[0][0] as ReturnType<typeof defaultWorkingHours>;
    expect(next[0].isClosed).toBe(true);
  });
});
