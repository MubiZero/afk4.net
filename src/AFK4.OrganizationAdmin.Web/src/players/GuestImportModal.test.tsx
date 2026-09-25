import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { GuestImportResultDto } from '@afk4/contracts';
import { GuestImportModal } from './GuestImportModal';

afterEach(cleanup);

const result = (committed: boolean): GuestImportResultDto => ({
  committed, total: 2, created: 1, matched: 1, skipped: 0,
  balanceTotal: { currencyCode: 'TJS', minorUnits: 7000 }, bonusTotal: { currencyCode: 'TJS', minorUnits: 1000 },
  issues: []
});

describe('GuestImportModal', () => {
  it('сначала проверяет без записи, потом переносит тем же ключом', async () => {
    const importGuests = mock(async (request: { dryRun: boolean }) => result(!request.dryRun));
    const onImported = mock(() => {});
    render(
      <I18nProvider initialLocale="ru">
        <GuestImportModal client={{ importGuests }} organizationId="org" currencyCode="TJS" onClose={() => {}} onImported={onImported} />
      </I18nProvider>
    );

    const file = new File(['Телефон;Имя;Баланс;Бонусы\n937370070;Фарход;50;10\n+992900000001;Зарина;20;0\n'], 'guests.csv', { type: 'text/csv' });
    fireEvent.change(screen.getByLabelText('Файл выгрузки'), { target: { files: [file] } });
    fireEvent.click(await screen.findByRole('button', { name: 'Проверить' }));

    expect(await screen.findByText('Проверка — ещё ничего не записано')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Перенести 2 гостей' }));

    await waitFor(() => expect(onImported).toHaveBeenCalledTimes(1));
    const [check, commit] = importGuests.mock.calls.map(([request]) => request as unknown as { dryRun: boolean; idempotencyKey: string; rows: unknown[] });
    expect(check.dryRun).toBe(true);
    expect(commit.dryRun).toBe(false);
    expect(commit.idempotencyKey).toBe(check.idempotencyKey);
    expect(commit.rows).toHaveLength(2);
    expect(screen.getByText('Перенесено')).toBeTruthy();
  });
});
