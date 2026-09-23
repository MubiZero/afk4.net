import { expect, it } from 'bun:test';

// Сообщение о проваленной проверке элемента должно быть коротким: иначе bun печатает весь граф
// happy-dom (для голой кнопки — больше двух мегабайт) и под нагрузкой тест висит минутами.
it('печатает элемент в провале проверки коротко', () => {
  const button = document.createElement('button');
  button.disabled = true;
  button.textContent = 'Посадить за ПК';
  let message = '';
  try {
    expect(button).not.toBeDisabled();
  } catch (error) {
    message = String((error as Error).message);
  }
  expect(message).toContain('<button disabled=""></button>');
  expect(message.length).toBeLessThan(2_000);
});
