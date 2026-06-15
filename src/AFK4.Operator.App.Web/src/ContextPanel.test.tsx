import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ContextPanel } from './ContextPanel';

describe('ContextPanel', () => {
  it('shows children and a collapse button when expanded', () => {
    const onToggle = mock(() => {});
    render(
      <I18nProvider>
        <ContextPanel collapsed={false} onToggle={onToggle} title="Details">
          <div>PANEL BODY</div>
        </ContextPanel>
      </I18nProvider>
    );

    expect(screen.getByText('PANEL BODY')).toBeDefined();
    const collapse = screen.getByLabelText('Свернуть панель');
    fireEvent.click(collapse);
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('hides children and shows only an expand button when collapsed', () => {
    const onToggle = mock(() => {});
    render(
      <I18nProvider>
        <ContextPanel collapsed={true} onToggle={onToggle}>
          <div>PANEL BODY</div>
        </ContextPanel>
      </I18nProvider>
    );

    expect(screen.queryByText('PANEL BODY')).toBeNull();
    const expand = screen.getByLabelText('Развернуть панель');
    fireEvent.click(expand);
    expect(onToggle).toHaveBeenCalledTimes(1);
  });
});
