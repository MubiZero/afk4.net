import { useState } from 'react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { PlayerClientItem } from '../operatorHelpers';
import { ClientPicker, type ClientPick } from './ClientPicker';

afterEach(cleanup);

function client(overrides: Partial<PlayerClientItem>): PlayerClientItem {
  return {
    playerAccountId: 'p1', name: 'Азиз П.', status: 'active', balanceMinorUnits: 0, debtMinorUnits: 0,
    last: '', tone: 'active', detail: '', phoneNumber: '+992 90 555 22 11', source: 'backend',
    createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0,
    ...overrides
  };
}

// Контролируемая обёртка: ClientPicker управляется value/onQueryChange извне, как в drawer.
function Harness({ search, onPick }: { search: (q: string) => Promise<PlayerClientItem[]>; onPick: (p: ClientPick) => void }) {
  const [value, setValue] = useState('');
  const [linked, setLinked] = useState(false);
  return (
    <I18nProvider>
      <ClientPicker
        value={value}
        linked={linked}
        search={search}
        onQueryChange={(name) => { setValue(name); setLinked(false); }}
        onPick={(pick) => { setValue(pick.name); setLinked(true); onPick(pick); }}
        onClear={() => { setValue(''); setLinked(false); }}
      />
    </I18nProvider>
  );
}

describe('ClientPicker', () => {
  it('ищет клиента и привязывает аккаунт при выборе', async () => {
    const search = mock(async () => [client({})]);
    const onPick = mock((_pick: ClientPick) => {});
    render(<Harness search={search} onPick={onPick} />);

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Аз' } });

    const option = await screen.findByText('Азиз П.');
    fireEvent.click(option);

    expect(search).toHaveBeenCalled();
    expect(onPick).toHaveBeenCalledTimes(1);
    expect(onPick.mock.calls[0][0]).toMatchObject({ playerAccountId: 'p1', name: 'Азиз П.' });
    // После выбора появляется бейдж «привязан».
    expect(screen.getByText('Клиент клуба')).toBeInTheDocument();
  });

  it('не ищет при вводе короче 2 символов', async () => {
    const search = mock(async () => [client({})]);
    render(<Harness search={search} onPick={() => {}} />);

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'А' } });
    await new Promise((resolve) => setTimeout(resolve, 350));
    expect(search).not.toHaveBeenCalled();
  });
});
