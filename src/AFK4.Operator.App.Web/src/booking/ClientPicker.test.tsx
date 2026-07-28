import { useState } from 'react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { PlayerClientItem } from '../operatorHelpers';
import { ClientPicker, type ClientPick } from './ClientPicker';

afterEach(cleanup);

function client(overrides: Partial<PlayerClientItem>): PlayerClientItem {
  return {
    playerAccountId: 'p1', name: 'Азиз П.', isActive: true, status: 'active', balanceMinorUnits: 0, debtMinorUnits: 0,
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

  it('выбирает одинакового клиента кликом и стрелкой с Enter', async () => {
    const clients = [
      client({ playerAccountId: 'p1', name: 'Азиз П.' }),
      client({ playerAccountId: 'p2', name: 'Мадина С.', phoneNumber: '+992 90 777 88 99', balanceMinorUnits: 45_000 })
    ];
    const clickPick = mock((_pick: ClientPick) => {});
    const keyboardPick = mock((_pick: ClientPick) => {});

    render(<Harness search={async () => clients} onPick={clickPick} />);
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Ма' } });
    fireEvent.click(await screen.findByText('Мадина С.'));
    const clicked = clickPick.mock.calls[0][0];

    cleanup();
    render(<Harness search={async () => clients} onPick={keyboardPick} />);
    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'Ма' } });
    await screen.findByText('Мадина С.');
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(keyboardPick).toHaveBeenCalledTimes(1);
    expect(keyboardPick.mock.calls[0][0]).toEqual(clicked);
    expect(clicked).toMatchObject({ playerAccountId: 'p2', name: 'Мадина С.', balanceMinorUnits: 45_000 });
  });

  it('отвязывает прежний аккаунт при изменении поисковой строки', () => {
    const onQueryChange = mock((_value: string) => {});
    render(
      <I18nProvider>
        <ClientPicker
          value="Азиз П."
          linked
          search={async () => []}
          onQueryChange={onQueryChange}
          onPick={() => {}}
          onClear={() => {}}
        />
      </I18nProvider>
    );

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'New name' } });

    expect(onQueryChange).toHaveBeenCalledWith('New name');
  });

  it('связывает combobox с активной option и возвращает фокус в input после сброса', async () => {
    const clients = [
      client({ playerAccountId: 'p1', name: 'Азиз П.' }),
      client({ playerAccountId: 'p2', name: 'Мадина С.' })
    ];
    render(<Harness search={async (query) => query === 'Об' ? [...clients].reverse() : clients} onPick={() => {}} />);

    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'Кл' } });
    const options = await screen.findAllByRole('option');
    const firstId = options[0].id;
    const secondId = options[1].id;

    expect(firstId).not.toBe('');
    expect(secondId).not.toBe('');
    expect(secondId).not.toBe(firstId);
    expect(input).toHaveAttribute('aria-activedescendant', firstId);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(input).toHaveAttribute('aria-activedescendant', secondId);
    expect(screen.getAllByRole('option')[0].id).toBe(firstId);

    fireEvent.change(input, { target: { value: 'Об' } });
    await waitFor(() => expect(screen.getAllByRole('option')[0]).toHaveTextContent('Мадина С.'));
    expect(screen.getAllByRole('option')[0].id).toBe(secondId);
    expect(screen.getAllByRole('option')[1].id).toBe(firstId);
    expect(input).toHaveAttribute('aria-activedescendant', secondId);
    fireEvent.keyDown(input, { key: 'Enter' });

    const linkedInput = screen.getByRole('combobox');
    fireEvent.click(screen.getByRole('button', { name: 'Сбросить' }));

    expect(linkedInput).toHaveValue('');
    expect(linkedInput).not.toHaveAttribute('aria-activedescendant');
    expect(document.activeElement).toBe(linkedInput);
    expect(screen.queryByRole('listbox')).toBeNull();
  });
});
