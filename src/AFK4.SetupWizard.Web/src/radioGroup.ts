import type { KeyboardEvent } from 'react';

/**
 * Клавиатура в группе переключателей.
 *
 * Три группы мастера объявляли себя `role="radiogroup"` и вели себя как набор обычных кнопок:
 * стрелки не работали, а Tab обходил каждую по очереди. Для экранного диктора это противоречие —
 * он объявляет «переключатель 1 из 2» и ждёт стрелок, — а для человека без мыши просто медленно.
 *
 * Правило одно на все три: стрелки двигают выбор по кругу и переносят фокус, Home и End прыгают
 * к краям. Фокус ищется по разметке самой группы, а не по массиву ссылок: групп три, они разной
 * формы, и общий код не должен требовать от каждой заводить ссылки на свои кнопки.
 */
export function handleRadioGroupKeys(
  event: KeyboardEvent<HTMLElement>,
  values: readonly string[],
  current: string | null,
  onChange: (value: string) => void
): void {
  if (values.length === 0) return;

  const currentIndex = current === null ? -1 : values.indexOf(current);
  const from = currentIndex < 0 ? 0 : currentIndex;
  const next = ((): number | null => {
    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        return (from + 1) % values.length;
      case 'ArrowLeft':
      case 'ArrowUp':
        return (from - 1 + values.length) % values.length;
      case 'Home':
        return 0;
      case 'End':
        return values.length - 1;
      default:
        return null;
    }
  })();

  if (next === null) return;

  // Иначе стрелка заодно прокрутит экран под группой.
  event.preventDefault();
  onChange(values[next]);

  const options = event.currentTarget.querySelectorAll<HTMLElement>('[role="radio"]');
  options.item(next)?.focus();
}

/**
 * Единственная точка входа Tab в группу — выбранный переключатель. Ничего не выбрано — первый:
 * группа, в которую нельзя попасть табом, недостижима с клавиатуры вовсе.
 */
export function radioTabIndex(index: number, checkedIndex: number): 0 | -1 {
  const entry = checkedIndex < 0 ? 0 : checkedIndex;
  return index === entry ? 0 : -1;
}
