import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { BranchSelectionScreen } from './BranchSelectionScreen';
import type { WizardBranch } from './wizardApi';

function branch(name: string, slug: string, seats: number, free: number): WizardBranch {
  return {
    branchId: `branch-${slug}`,
    branchSlug: slug,
    branchName: name,
    zones: [],
    seats: Array.from({ length: seats }, (_, index) => ({
      seatId: `${slug}-seat-${index}`,
      pcName: `ПК-${index + 1}`,
      zoneId: 'zone',
      zoneName: 'Главный зал',
      sortOrder: index,
      status: 'free',
      deviceId: null,
      deviceName: null,
      isOnline: null,
    })),
    freeSeatIds: Array.from({ length: free }, (_, index) => `${slug}-seat-${index}`),
    hasTariff: false,
    hasStaffBesidesOwner: false,
  };
}

function renderScreen(branches: WizardBranch[]) {
  const onSelect = mock((_branch: WizardBranch) => {});
  const onBack = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <BranchSelectionScreen
        stepNumber={2}
        ownerName="Владелец"
        branches={branches}
        onSelect={onSelect}
        onBack={onBack}
      />
    </I18nProvider>,
  );
  return { onSelect, onBack };
}

describe('BranchSelectionScreen', () => {
  afterEach(cleanup);

  it('отдаёт наверх выбранный филиал целиком, а не его имя', () => {
    const club = branch('Центральный', 'central', 20, 5);
    const { onSelect } = renderScreen([club, branch('Южный', 'south', 10, 10)]);

    fireEvent.click(screen.getByRole('button', { name: /Центральный/ }));

    expect(onSelect.mock.calls[0]![0]).toBe(club);
  });

  // Ставят обычно в том зале, где сейчас стоят: сколько мест свободно — то, по чему его узнают.
  it('показывает, сколько мест в филиале свободно', () => {
    renderScreen([branch('Центральный', 'central', 20, 5)]);

    const card = screen.getByRole('button', { name: /Центральный/ });
    expect(card.textContent).toContain('5');
    expect(card.textContent).toContain('20');
    expect(card.textContent).toContain('central');
  });

  // Пустой список — это не «ничего не найдено», а «нечего выбирать, идите в панель»: молча
  // пустой экран человек читает как поломку мастера.
  it('без филиалов объясняет, что делать', () => {
    renderScreen([]);

    expect(screen.getByText('У этого клуба нет филиалов')).toBeInTheDocument();
    expect(screen.getByText(/Создайте филиал в Панели AFK4.net/)).toBeInTheDocument();
  });

  it('кнопка «Назад» уводит на вход', () => {
    const { onBack } = renderScreen([branch('Центральный', 'central', 20, 5)]);

    fireEvent.click(screen.getByRole('button', { name: 'Назад' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });
});
