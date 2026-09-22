import { useEffect, type RefObject } from 'react';

// Модальное окно забирает экран целиком, поэтому оно обязано забрать и фокус: иначе человек с
// клавиатурой продолжает ходить табом по странице ПОД окном — жмёт кнопки, которых не видит.
// И наоборот, при закрытии фокус должен вернуться туда, откуда окно позвали, а не улететь в
// начало документа: иначе после каждого диалога приходится заново пробираться табом к месту.
//
// Такой же хук лежит в админке клуба. Общий @afk4/ui намеренно оставлен CSS-только (иначе он
// тянет React в сборку обоих приложений), и пока приложений два, копия дешевле общей зависимости.
const FOCUSABLE = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])'
].join(',');

export function useDialogFocus(ref: RefObject<HTMLElement | null>, active: boolean) {
  useEffect(() => {
    if (!active) {
      return;
    }
    const dialog = ref.current;
    if (!dialog) {
      return;
    }
    const restoreTo = document.activeElement as HTMLElement | null;
    const focusable = () => [...dialog.querySelectorAll<HTMLElement>(FOCUSABLE)];

    // Первым фокусируем содержимое, а не кнопку закрытия: человек пришёл заполнять форму.
    const first = focusable().find(el => !el.classList.contains('panel-modal-close'));
    (first ?? dialog).focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Tab') {
        return;
      }
      const items = focusable();
      if (items.length === 0) {
        event.preventDefault();
        return;
      }
      const edge = event.shiftKey ? items[0] : items[items.length - 1];
      if (document.activeElement === edge || !dialog.contains(document.activeElement)) {
        event.preventDefault();
        (event.shiftKey ? items[items.length - 1] : items[0]).focus();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      // Элемент мог исчезнуть вместе с закрытым разделом — тогда возвращать фокус некуда.
      if (restoreTo?.isConnected) {
        restoreTo.focus();
      }
    };
  }, [active, ref]);
}
