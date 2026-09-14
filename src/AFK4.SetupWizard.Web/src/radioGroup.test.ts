import { describe, expect, it, mock } from 'bun:test';
import { handleRadioGroupKeys, radioTabIndex } from './radioGroup';

function keyEvent(key: string, options: HTMLElement[] = []) {
  const focused: number[] = [];
  const currentTarget = {
    querySelectorAll: () => ({
      item: (index: number) => {
        focused.push(index);
        return options[index] ?? null;
      }
    })
  };
  let prevented = false;
  return {
    event: { key, preventDefault: () => { prevented = true; }, currentTarget } as never,
    focused,
    wasPrevented: () => prevented
  };
}

describe('клавиатура в группе переключателей', () => {
  const values = ['a', 'b', 'c'] as const;

  it('стрелка вперёд идёт к следующему и по кругу', () => {
    const onChange = mock((_: string) => {});
    const forward = keyEvent('ArrowRight');
    handleRadioGroupKeys(forward.event, values, 'a', onChange);
    expect(onChange).toHaveBeenLastCalledWith('b');

    const wrap = keyEvent('ArrowDown');
    handleRadioGroupKeys(wrap.event, values, 'c', onChange);
    expect(onChange).toHaveBeenLastCalledWith('a');
  });

  it('стрелка назад идёт к предыдущему и по кругу', () => {
    const onChange = mock((_: string) => {});
    handleRadioGroupKeys(keyEvent('ArrowLeft').event, values, 'b', onChange);
    expect(onChange).toHaveBeenLastCalledWith('a');
    handleRadioGroupKeys(keyEvent('ArrowUp').event, values, 'a', onChange);
    expect(onChange).toHaveBeenLastCalledWith('c');
  });

  it('Home и End прыгают к краям', () => {
    const onChange = mock((_: string) => {});
    handleRadioGroupKeys(keyEvent('Home').event, values, 'c', onChange);
    expect(onChange).toHaveBeenLastCalledWith('a');
    handleRadioGroupKeys(keyEvent('End').event, values, 'a', onChange);
    expect(onChange).toHaveBeenLastCalledWith('c');
  });

  // Иначе стрелка заодно прокрутит экран под группой.
  it('отменяет прокрутку только на своих клавишах', () => {
    const arrow = keyEvent('ArrowRight');
    handleRadioGroupKeys(arrow.event, values, 'a', () => {});
    expect(arrow.wasPrevented()).toBe(true);

    const letter = keyEvent('x');
    handleRadioGroupKeys(letter.event, values, 'a', () => {});
    expect(letter.wasPrevented()).toBe(false);
  });

  it('переносит фокус на тот переключатель, который выбрал', () => {
    const moved = keyEvent('ArrowRight');
    handleRadioGroupKeys(moved.event, values, 'a', () => {});
    expect(moved.focused).toEqual([1]);
  });

  it('ничего не выбрано — стрелка начинает с первого', () => {
    const onChange = mock((_: string) => {});
    handleRadioGroupKeys(keyEvent('ArrowRight').event, values, null, onChange);
    expect(onChange).toHaveBeenLastCalledWith('b');
  });

  // Группа, в которую нельзя попасть табом, недостижима с клавиатуры вовсе.
  it('точка входа табом одна: выбранный, а без выбора — первый', () => {
    expect([0, 1, 2].map((index) => radioTabIndex(index, 1))).toEqual([-1, 0, -1]);
    expect([0, 1, 2].map((index) => radioTabIndex(index, -1))).toEqual([0, -1, -1]);
  });
});
