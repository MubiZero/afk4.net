import { test, expect, mock, afterEach } from 'bun:test';
import { cleanup, render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BranchSwitcher } from './BranchSwitcher';

afterEach(() => cleanup());

const renderSwitcher = (activeBranchId: string | null, onSelect = mock((_branchId: string) => {})) => {
  render(
    <I18nProvider>
      <BranchSwitcher
        branches={[
          { branchId: 'b1', name: 'Center' },
          { branchId: 'b2', name: 'Sever' }
        ]}
        activeBranchId={activeBranchId}
        onSelect={onSelect}
      />
    </I18nProvider>
  );
  return onSelect;
};

test('renders active branch and fires onSelect', () => {
  const onSelect = renderSwitcher('b1');
  expect(screen.getByText('Center')).toBeInTheDocument();
  fireEvent.click(screen.getByText('Sever'));
  expect(onSelect).toHaveBeenCalledWith('b2');
});

test('marks the active branch via aria-current', () => {
  renderSwitcher('b2');
  const active = screen.getByText('Sever');
  expect(active.closest('button')).toHaveAttribute('aria-current', 'true');
  expect(screen.getByText('Center').closest('button')).not.toHaveAttribute('aria-current');
});

test('falls back to a generic label for a branch without a name', () => {
  const onSelect = mock((_branchId: string) => {});
  render(
    <I18nProvider>
      <BranchSwitcher branches={[{ branchId: 'b1', name: '' }]} activeBranchId="b1" onSelect={onSelect} />
    </I18nProvider>
  );
  expect(screen.getByText('Филиал')).toBeInTheDocument();
});

test('renders nothing for an empty branch list', () => {
  const { container } = render(
    <I18nProvider>
      <BranchSwitcher branches={[]} activeBranchId={null} onSelect={mock(() => {})} />
    </I18nProvider>
  );
  expect(container).toBeEmptyDOMElement();
});
