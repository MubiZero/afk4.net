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
const reply = mock(async (_branchId: string, _reviewId: string, _text: string) => {});
const hideComment = mock(async (_branchId: string, _reviewId: string, _reason: string) => {});
const showComment = mock(async (_branchId: string, _reviewId: string) => {});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ reviews: { list, reply, hideComment, showComment } })
}));

const { ReviewsDestination } = await import('./ReviewsDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

function renderScreen(permissions = ['organization.reviews.view']) {
  return render(
    <I18nProvider initialLocale="ru">
      <ReviewsDestination backend={backend} session={{ permissions, organizationId: 'o1' } as never} currencyCode="TJS" />
    </I18nProvider>
  );
}

afterEach(() => { list.mockClear(); reply.mockClear(); hideComment.mockClear(); showComment.mockClear(); cleanup(); });
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

  it('the manager answers under the review, and the answer shows once the server has it', async () => {
    renderScreen(['organization.reviews.view', 'organization.reviews.manage']);
    await screen.findByText('Мышь липкая');

    fireEvent.click(screen.getByRole('button', { name: 'Ответить' }));
    fireEvent.change(screen.getByLabelText('Ответ клуба'), { target: { value: 'Поменяли мышь, приходите.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Опубликовать' }));

    await waitFor(() => expect(reply).toHaveBeenCalledWith('b1', 'r1', 'Поменяли мышь, приходите.'));
    expect(await screen.findByText('Поменяли мышь, приходите.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Изменить ответ' })).toBeInTheDocument();
  });

  it('hiding asks why, then marks the text as hidden from players', async () => {
    renderScreen(['organization.reviews.view', 'organization.reviews.manage']);
    await screen.findByText('Мышь липкая');

    fireEvent.click(screen.getByRole('button', { name: 'Скрыть текст' }));
    expect(screen.getByRole('button', { name: 'Скрыть' })).toBeDisabled();
    fireEvent.click(screen.getByLabelText('Оскорбление'));
    fireEvent.click(screen.getByRole('button', { name: 'Скрыть' }));

    await waitFor(() => expect(hideComment).toHaveBeenCalledWith('b1', 'r1', 'insult'));
    expect(await screen.findByText('Текст скрыт от игроков: Оскорбление')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Показать снова' }));
    await waitFor(() => expect(showComment).toHaveBeenCalledWith('b1', 'r1'));
  });

  it('a reader without the right sees reviews but cannot answer or hide', async () => {
    renderScreen();
    await screen.findByText('Мышь липкая');

    expect(screen.queryByRole('button', { name: 'Ответить' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Скрыть текст' })).toBeNull();
  });

  it('no reviews says where they come from', async () => {
    page = { rating: null, reviewCount: 0, countsByRating: [0, 0, 0, 0, 0], items: [], nextBefore: null };
    renderScreen();

    expect(await screen.findByText('Отзывов пока нет')).toBeInTheDocument();
    expect(screen.getByText(/в конце сессии на ПК/)).toBeInTheDocument();
  });
});
