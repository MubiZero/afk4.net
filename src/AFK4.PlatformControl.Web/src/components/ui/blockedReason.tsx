import { useId } from 'react';

/**
 * Почему кнопка или поле неактивны — словами, рядом с ними.
 *
 * Тот же приём, что у Панели AFK4.net (`useBlockedReason` там): серая кнопка без объяснения
 * заставляет гадать. Всплывающая подсказка (`title`) не годится — на неактивной кнопке браузер её
 * не показывает, а на сенсорном экране её нет вовсе. Поэтому причина — обычный текст, а элемент
 * ссылается на него через `aria-describedby`, чтобы экранный диктор прочитал её вместе с ним.
 *
 * Причина нужна, когда элемент гасит условие, которого на экране не видно. Пока идёт запрос или
 * пустое поле стоит прямо над кнопкой, объяснять нечего.
 */
export function useBlockedReason(reason: string | null) {
  const id = useId();
  return {
    describedBy: reason === null ? undefined : id,
    hint: reason === null ? null : <p id={id} className="ui-blocked-reason" role="status">{reason}</p>
  };
}
