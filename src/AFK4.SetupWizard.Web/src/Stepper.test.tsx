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

  it('нумерует шаги подряд, без дыр от пропущенных', () => {
    const { container } = renderStepper(['phoneLogin', 'role', 'device', 'finished'], 'phoneLogin');

    const numbers = [...container.querySelectorAll('.wizard-stepper-dot')].map((dot) => dot.textContent);
    expect(numbers).toEqual(['1', '2', '3', '4']);
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
