import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ReputationCard } from './ReputationCard';
import type { ReputationController, ReputationState } from './useReputation';

afterEach(cleanup);

const asOf = '2026-08-20T00:00:00Z';

function renderCard(state: ReputationState, ask = () => {}) {
  const controller: ReputationController = { state, ask };
  return render(<I18nProvider initialLocale="ru"><ReputationCard controller={controller} /></I18nProvider>);
}

describe('ReputationCard', () => {
  it('без номера и без личности объясняет, почему спрашивать нечего, и не предлагает кнопку', () => {
    renderCard({ status: 'noPhone' });
    expect(screen.getByText('Ни номера, ни личности — сеть не поймёт, о ком спрашивать.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('сеть спрашивается по нажатию, и оператор заранее знает про журнал', () => {
    const ask = mock(() => {});
    renderCard({ status: 'idle' }, ask);
    expect(screen.getByText('Запрос попадает в журнал клуба.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Спросить сеть' }).click();
    expect(ask).toHaveBeenCalledTimes(1);
  });

  it('показывает четыре величины ответа и ни слова о чужих клубах', () => {
    const { container } = renderCard({
      status: 'ready',
      reputation: { networkVisits: 14, networkNoShows: 0, networkBanned: false, calculatedAtUtc: asOf }
    });
    expect(screen.getByText('Визитов в сети')).toBeInTheDocument();
    expect(screen.getByText('14')).toBeInTheDocument();
    expect(screen.getByText('Неявок')).toBeInTheDocument();
    expect(container.textContent).toContain('Посчитано');
    expect(container.textContent).toContain('Сеть отвечает числами и не называет, где человек играл.');
  });

  it('неявки подсвечены, но не превращают человека в запрет', () => {
    const { container } = renderCard({
      status: 'ready',
      reputation: { networkVisits: 3, networkNoShows: 2, networkBanned: false, calculatedAtUtc: asOf }
    });
    expect(container.querySelector('.reputation-card')).toHaveClass('is-watch');
    expect(container.querySelector('.reputation-numbers .is-attention')).not.toBeNull();
    expect(screen.queryByText('Сеть закрыла этому человеку вход')).toBeNull();
  });

  it('сетевой запрет виден отдельным предупреждением', () => {
    const { container } = renderCard({
      status: 'ready',
      reputation: { networkVisits: 40, networkNoShows: 0, networkBanned: true, calculatedAtUtc: asOf }
    });
    expect(container.querySelector('.reputation-card')).toHaveClass('is-banned');
    expect(screen.getByRole('alert')).toHaveTextContent('Сеть закрыла этому человеку вход');
  });

  it('отказ показывает настоящую причину и даёт спросить ещё раз', () => {
    const ask = mock(() => {});
    renderCard({ status: 'failed', detail: 'Слишком много запросов подряд — подождите минуту.' }, ask);
    expect(screen.getByRole('alert')).toHaveTextContent('Слишком много запросов подряд');
    screen.getByRole('button', { name: 'Спросить ещё раз' }).click();
    expect(ask).toHaveBeenCalledTimes(1);
  });
});
