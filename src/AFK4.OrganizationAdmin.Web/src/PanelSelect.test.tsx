import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { PanelSelect } from './PanelSelect';

afterEach(cleanup);

const options = [
  { value: 'pc-01', label: 'PC-01' },
  { value: 'pc-02', label: 'PC-02' }
];

describe('PanelSelect', () => {
  it('shows the selected option label on the trigger', () => {
    const { getByRole } = render(
      <PanelSelect ariaLabel="Перенести на" value="pc-02" options={options} onChange={() => {}} />
    );
    expect(getByRole('combobox').textContent).toContain('PC-02');
  });

  it('falls back to the placeholder when nothing is selected', () => {
    const { getByRole } = render(
      <PanelSelect ariaLabel="Перенести на" value="" options={[]} onChange={() => {}} placeholder="Нет свободных ПК" disabled />
    );
    const trigger = getByRole('combobox');
    expect(trigger.textContent).toContain('Нет свободных ПК');
    expect(trigger).toHaveProperty('disabled', true);
  });

  it('opens the list and reports the chosen value', () => {
    const onChange = mock(() => {});
    const { getByRole, queryByRole } = render(
      <PanelSelect ariaLabel="Перенести на" value="pc-01" options={options} onChange={onChange} />
    );
    expect(queryByRole('listbox')).toBeNull();
    fireEvent.click(getByRole('combobox'));
    const list = getByRole('listbox');
    expect(list).not.toBeNull();
    fireEvent.click(getByRole('option', { name: 'PC-02' }));
    expect(onChange).toHaveBeenCalledWith('pc-02');
    expect(queryByRole('listbox')).toBeNull();
  });

  it('marks the active value as selected in the open list', () => {
    const { getByRole } = render(
      <PanelSelect ariaLabel="Перенести на" value="pc-02" options={options} onChange={() => {}} />
    );
    fireEvent.click(getByRole('combobox'));
    expect(getByRole('option', { name: 'PC-02' }).getAttribute('aria-selected')).toBe('true');
    expect(getByRole('option', { name: 'PC-01' }).getAttribute('aria-selected')).toBe('false');
  });
});
