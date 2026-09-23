import { fireEvent } from '@testing-library/react';

/**
 * Enter в поле — так, как его обрабатывает браузер.
 *
 * happy-dom неявную отправку формы не делает, поэтому здесь повторён её порядок из спецификации
 * HTML: поле вне формы — Enter ничего не отправляет; в форме нажимается кнопка по умолчанию
 * (первая кнопка отправки по порядку в разметке), а если она неактивна, не происходит ничего.
 * Форму без кнопки отправки браузер по Enter отправляет, только когда поле в ней одно; у экранов
 * мастера такой формы нет, и хелпер её не отправляет.
 */
export function pressEnter(field: HTMLElement): void {
  fireEvent.keyDown(field, { key: 'Enter', code: 'Enter' });
  const form = (field as HTMLInputElement).form ?? null;
  if (form === null) return;
  const defaultButton = Array.from(form.querySelectorAll<HTMLButtonElement | HTMLInputElement>('button, input'))
    .find((element) => element.type === 'submit');
  if (defaultButton === undefined || defaultButton.disabled) return;
  fireEvent.click(defaultButton);
}
