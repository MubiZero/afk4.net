import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { RowActions, type RowAction } from './RowActions';

const actions = (overrides: Partial<Record<string, Partial<RowAction>>> = {}): RowAction[] => [
  { id: 'edit', label: 'Переименовать', onSelect: () => {}, ...overrides.edit },
  { id: 'hide', label: 'Скрыть', onSelect: () => {}, ...overrides.hide },
  { id: 'del', label: 'Удалить', onSelect: () => {}, danger: true, ...overrides.del },
];

const open = () => fireEvent.click(screen.getByRole('button', { name: 'Действия' }));

describe('RowActions', () => {
  it('renders nothing when there is nothing to do', () => {
    const { container } = render(<RowActions label="Действия" actions={[]} />);
    expect(container.innerHTML).toBe('');
  });

  it('announces a menu and moves focus into it on open', () => {
    render(<RowActions label="Действия" actions={actions()} />);
    const trigger = screen.getByRole('button', { name: 'Действия' });
    expect(trigger).toHaveAttribute('aria-haspopup', 'menu');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    open();
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('menuitem', { name: 'Переименовать' })).toHaveFocus();
  });

  it('walks the items with arrows in a circle and jumps with Home/End', () => {
    render(<RowActions label="Действия" actions={actions()} />);
    open();
    const [edit, hide, del] = screen.getAllByRole('menuitem');
    fireEvent.keyDown(edit, { key: 'ArrowDown' });
    expect(hide).toHaveFocus();
    fireEvent.keyDown(hide, { key: 'End' });
    expect(del).toHaveFocus();
    fireEvent.keyDown(del, { key: 'ArrowDown' });
    expect(edit).toHaveFocus();
    fireEvent.keyDown(edit, { key: 'ArrowUp' });
    expect(del).toHaveFocus();
    fireEvent.keyDown(del, { key: 'Home' });
    expect(edit).toHaveFocus();
  });

  it('closes on Escape and hands focus back to the trigger', () => {
    render(<RowActions label="Действия" actions={actions()} />);
    open();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
    expect(screen.getByRole('button', { name: 'Действия' })).toHaveFocus();
  });

  it('closes on Tab and on a click outside', () => {
    render(<div><button type="button">Снаружи</button><RowActions label="Действия" actions={actions()} /></div>);
    open();
    fireEvent.keyDown(screen.getAllByRole('menuitem')[0], { key: 'Tab' });
    expect(screen.queryByRole('menu')).toBeNull();
    open();
    fireEvent.pointerDown(screen.getByRole('button', { name: 'Снаружи' }));
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('runs the chosen action once and closes', () => {
    const onSelect = mock(() => {});
    render(<RowActions label="Действия" actions={actions({ hide: { onSelect } })} />);
    open();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Скрыть' }));
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('never runs a disabled action', () => {
    const onSelect = mock(() => {});
    render(<RowActions label="Действия" actions={actions({ edit: { onSelect, disabled: true } })} />);
    open();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Переименовать' }));
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('sets danger apart with a separator', () => {
    render(<RowActions label="Действия" actions={actions()} />);
    open();
    expect(screen.getByRole('menuitem', { name: 'Удалить' })).toHaveClass('is-danger');
    expect(screen.getAllByRole('separator')).toHaveLength(1);
  });
});
