import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { BranchReviewDto, BranchReviewsPageDto } from '../../../api/clients/reviews';

function review(overrides: Partial<BranchReviewDto> = {}): BranchReviewDto {
  return {
    reviewId: 'r1', playerAccountId: 'p1', authorName: 'Азиз', rating: 1, comment: 'Мышь липкая',
    createdAtUtc: '2026-09-20T18:00:00Z', sessionId: 's1', seatName: 'ПК 07', ...overrides
  };
}

let page: BranchReviewsPageDto = { rating: 3.8, reviewCount: 4, countsByRating: [1, 0, 0, 1, 2], items: [review()], nextBefore: null };
const list = mock(async (_branchId: string, _query: Record<string, unknown>) => page);

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ reviews: { list } })
}));

const { ReviewsDestination } = await import('./ReviewsDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

function renderScreen() {
  return render(
    <I18nProvider initialLocale="ru">
      <ReviewsDestination backend={backend} session={{ permissions: ['organization.reviews.view'], organizationId: 'o1' } as never} currencyCode="TJS" />
    </I18nProvider>
  );
}

afterEach(() => { list.mockClear(); cleanup(); });
afterAll(() => mock.module('../../../operatorHelpers', () => actual));

describe('ReviewsDestination', () => {
  it('shows the average, the split by stars and the seat of each review', async () => {
    renderScreen();

    expect(await screen.findByText('Мышь липкая')).toBeInTheDocument();
    expect(screen.getByText('4 оценки')).toBeInTheDocument();
    expect(screen.getByText('ПК 07')).toBeInTheDocument();
  });

  it('a star bar filters by that rating', async () => {
    renderScreen();
    await screen.findByText('Мышь липкая');

    fireEvent.click(screen.getAllByRole('button').find((button) => button.textContent?.startsWith('1'))!);

    await waitFor(() => expect(list).toHaveBeenLastCalledWith('b1', { rating: 1, withComment: false }));
  });

  it('no reviews says where they come from', async () => {
    page = { rating: null, reviewCount: 0, countsByRating: [0, 0, 0, 0, 0], items: [], nextBefore: null };
    renderScreen();

    expect(await screen.findByText('Отзывов пока нет')).toBeInTheDocument();
    expect(screen.getByText(/в конце сессии на ПК/)).toBeInTheDocument();
  });
});
