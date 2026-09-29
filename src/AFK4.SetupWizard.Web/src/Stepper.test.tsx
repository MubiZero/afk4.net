import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { Stepper, type WizardStep } from './Stepper';

function renderStepper(steps: WizardStep[], current: WizardStep) {
  return render(
    <I18nProvider initialLocale="ru">
      <Stepper steps={steps} current={current} />
    </I18nProvider>,
  );
}

describe('Stepper', () => {
  afterEach(cleanup);

  // Степпер рисует шаги ИМЕННО этого прогона. Раньше он показывал зашитые девять позиций, включая
  // четыре, которых на игровом ПК не бывает никогда, — и обещал человеку работу, которой не будет.
  it('показывает только шаги этого прогона', () => {
    renderStepper(['phoneLogin', 'role', 'device', 'finished'], 'role');

    expect(screen.getAllByRole('listitem')).toHaveLength(4);
    expect(screen.queryByText('Тариф')).toBeNull();
    expect(screen.queryByText('Зал')).toBeNull();
  });

  // Номер — только у текущего шага, и он считается по шагам этого прогона, без дыр от
  // пропущенных. У будущих номера нет: после «Игровой ПК» настройка клуба уходит из списка, и
  // уже показанный номер «ПК» сменился бы с восьмого на четвёртый.
  it('нумерует только текущий шаг, по шагам этого прогона', () => {
    const { container } = renderStepper(['phoneLogin', 'role', 'device', 'finished'], 'device');

    const numbers = [...container.querySelectorAll('.wizard-stepper-dot')].map((dot) => dot.textContent);
    expect(numbers).toEqual(['', '', '3', '']);
  });

  // Подпись видна всегда, а не во всплывающей подсказке: по одним кружкам не понять, что впереди.
  it('подписывает каждый шаг', () => {
    renderStepper(['phoneLogin', 'role', 'device', 'finished'], 'role');

    for (const label of ['Вход', 'Роль', 'ПК', 'Готово']) expect(screen.getByText(label)).toBeVisible();
  });

  it('на «Готово» все шаги отмечены сделанными', () => {
    const { container } = renderStepper(['phoneLogin', 'role', 'device', 'finished'], 'finished');

    const numbers = [...container.querySelectorAll('.wizard-stepper-dot')].map((dot) => dot.textContent);
    expect(numbers).toEqual(['', '', '', '']);
    expect(screen.getByText('Готово').closest('li')?.getAttribute('aria-current')).toBe('step');
  });

  it('отмечает текущий шаг для скринридера', () => {
    renderStepper(['phoneLogin', 'role', 'device'], 'role');

    const current = screen.getByText('Роль').closest('li');
    expect(current?.getAttribute('aria-current')).toBe('step');
  });

  it('пройденные шаги помечает галкой вместо номера', () => {
    const { container } = renderStepper(['phoneLogin', 'role', 'device'], 'device');

    const dots = [...container.querySelectorAll('.wizard-stepper-dot')].map((dot) => dot.textContent);
    expect(dots[0]).toBe('');
    expect(dots[1]).toBe('');
    expect(dots[2]).toBe('3');
  });

  // Сброс пароля — ответвление от входа: своей позиции у него нет, и подсветка остаётся на входе.
  it('на сбросе пароля подсвечивает вход', () => {
    renderStepper(['phoneLogin', 'role', 'device'], 'forgotPassword');

    expect(screen.getByText('Вход').closest('li')?.getAttribute('aria-current')).toBe('step');
  });
});
