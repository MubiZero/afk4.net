import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render } from '@testing-library/react';
import { useHotkeys } from './useHotkeys';

afterEach(cleanup);

function Harness({ onK, allowInInputs = true }: { onK: () => void; allowInInputs?: boolean }) {
  useHotkeys([{ key: 'k', ctrl: true, onTrigger: onK, allowInInputs }]);
  return <input data-testid="field" />;
}
function press(init: KeyboardEventInit & { target?: EventTarget }) {
  const ev = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
  (init.target ?? window).dispatchEvent(ev);
}

describe('useHotkeys', () => {
  it('fires when the combo matches', () => {
    const onK = mock(() => {});
    render(<Harness onK={onK} />);
    press({ key: 'k', ctrlKey: true });
    expect(onK).toHaveBeenCalledTimes(1);
  });
  it('ignores key repeat', () => {
    const onK = mock(() => {});
    render(<Harness onK={onK} />);
    press({ key: 'k', ctrlKey: true, repeat: true });
    expect(onK).not.toHaveBeenCalled();
  });
  it('does not fire from an input when allowInInputs is false', () => {
    const onK = mock(() => {});
    const { getByTestId } = render(<Harness onK={onK} allowInInputs={false} />);
    press({ key: 'k', ctrlKey: true, target: getByTestId('field') });
    expect(onK).not.toHaveBeenCalled();
  });
  it('removes its listener on unmount', () => {
    const onK = mock(() => {});
    const { unmount } = render(<Harness onK={onK} />);
    unmount();
    press({ key: 'k', ctrlKey: true });
    expect(onK).not.toHaveBeenCalled();
  });
});
