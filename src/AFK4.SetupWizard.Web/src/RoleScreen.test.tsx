import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { RoleScreen } from './RoleScreen';
import type { WizardRole } from './wizardApi';

function renderScreen(initialRole: WizardRole = 'gaming_pc') {
  const onContinue = mock((_role: WizardRole) => {});
  const onBack = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <RoleScreen
        stepNumber={2}
        ownerName="Владелец"
        branchName="Главный"
        initialRole={initialRole}
        onContinue={onContinue}
        onBack={onBack}
      />
    </I18nProvider>,
  );
  return { onContinue, onBack };
}

describe('RoleScreen', () => {
  afterEach(cleanup);

  // Роль решает всё дальнейшее: игровой ПК получает оболочку игрока и место в зале, рабочее место
  // управляющего — панель и настройку клуба. Ошибка здесь видна человеку только в конце установки.
  it('отдаёт наверх выбранную роль', () => {
    const { onContinue } = renderScreen();

    fireEvent.click(screen.getByRole('radio', { name: /Админ \/ кассир/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));

    expect(onContinue.mock.calls[0]![0]).toBe('manager_workstation');
  });

  it('без выбора продолжает с ролью по умолчанию', () => {
    const { onContinue } = renderScreen('manager_workstation');

    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));

    expect(onContinue.mock.calls[0]![0]).toBe('manager_workstation');
  });

  // Мастер ставят с клавиатуры не реже, чем мышью: ПК новый, мыши под рукой может не быть.
  it('стрелка переключает роль с клавиатуры', () => {
    const { onContinue } = renderScreen('gaming_pc');

    fireEvent.keyDown(screen.getByRole('radiogroup'), { key: 'ArrowDown' });
    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));

    expect(onContinue.mock.calls[0]![0]).toBe('manager_workstation');
  });

  it('показывает выбранную роль как отмеченную', () => {
    renderScreen('manager_workstation');

    expect(screen.getByRole('radio', { name: /Админ \/ кассир/ }).getAttribute('aria-checked')).toBe('true');
    expect(screen.getByRole('radio', { name: /Игровой ПК/ }).getAttribute('aria-checked')).toBe('false');
  });

  it('кнопка «Назад» уводит на предыдущий шаг', () => {
    const { onBack } = renderScreen();

    fireEvent.click(screen.getByRole('button', { name: 'Назад' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });
});
