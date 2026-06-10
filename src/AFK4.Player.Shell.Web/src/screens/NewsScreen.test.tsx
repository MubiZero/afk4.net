import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { NewsScreen } from './NewsScreen';
import type { ShellApi } from '../shellApi';

function api(over: Partial<ShellApi>): ShellApi {
  return {
    getNews: async () => [
      { id: '1', title: 'Турнир в субботу', body: 'Призовой фонд 1000', imageUrl: null, publishedAtUtc: '2026-06-09T10:00:00Z' }
    ],
    ...over
  } as unknown as ShellApi;
}

describe('NewsScreen', () => {
  it('renders news cards from the api', async () => {
    render(<NewsScreen api={api({})} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/Турнир в субботу/));
    expect(screen.getByText(/Призовой фонд 1000/)).toBeInTheDocument();
  });

  it('shows an empty state when there is no news', async () => {
    render(<NewsScreen api={api({ getNews: async () => [] })} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/новостей пока нет/i));
  });
});
