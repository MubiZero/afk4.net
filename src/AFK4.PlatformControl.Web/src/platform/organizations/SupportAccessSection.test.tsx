import { render, screen, fireEvent, waitFor, cleanup } from '@testing-library/react';
import { afterEach, it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformApi';
import type { SupportAccessGrantListItem } from '@/api/types';
import { SupportAccessSection } from './SupportAccessSection';

afterEach(cleanup);

function grant(overrides: Partial<SupportAccessGrantListItem> = {}): SupportAccessGrantListItem {
  return {
    grantId: 'g1',
    organizationId: 'o1',
    reason: 'Клуб сообщает, что не открывается смена',
    issuedAtUtc: '2026-09-14T10:00:00Z',
    expiresAtUtc: '2026-09-14T10:30:00Z',
    platformAdminUserId: 'a1',
    platformAdminDisplayName: 'Первая поддержка',
    enteredAtUtc: null,
    ...overrides
  };
}

function renderSection(client: Record<string, unknown>, openUrl: (url: string) => void = () => {}) {
  render(
    <I18nProvider><ToastProvider>
      <SupportAccessSection client={client as never} organizationId="o1" openUrl={openUrl} />
    </ToastProvider></I18nProvider>
  );
}

it('выдаёт доступ и открывает админку клиента', async () => {
  const issueGrant = mock().mockResolvedValue({
    grant: { grantId: 'g1', organizationId: 'o1', reason: 'Смена не открывается', issuedAtUtc: '', expiresAtUtc: '', revokedAtUtc: null },
    ticket: 't1',
    adminUrl: 'https://admin.example/support-access?ticket=t1'
  });
  const opened: string[] = [];
  renderSection({ issueGrant, listGrants: mock().mockResolvedValue([]), revokeGrant: mock() }, url => opened.push(url));

  fireEvent.change(screen.getByLabelText('Причина'), {
    target: { value: 'Клуб сообщает, что не открывается смена' }
  });
  fireEvent.click(screen.getByRole('button', { name: 'Войти под клиента' }));

  await waitFor(() => expect(issueGrant).toHaveBeenCalledWith('o1', 'Клуб сообщает, что не открывается смена', 30));
  expect(opened).toEqual(['https://admin.example/support-access?ticket=t1']);
});

// Выданный доступ был невидим: кто сейчас внутри клуба, в панели не показывалось нигде.
it('показывает, кто допущен в клуб, зачем и до какого времени', async () => {
  renderSection({ issueGrant: mock(), listGrants: mock().mockResolvedValue([grant()]), revokeGrant: mock() });

  await screen.findByText('Первая поддержка');
  expect(screen.getByText('Клуб сообщает, что не открывается смена')).toBeInTheDocument();
  expect(screen.getByText(/ещё не входил/)).toBeInTheDocument();
});

it('отличает доступ, которым уже воспользовались, от невостребованного', async () => {
  renderSection({
    issueGrant: mock(),
    listGrants: mock().mockResolvedValue([grant({ enteredAtUtc: '2026-09-14T10:05:00Z' })]),
    revokeGrant: mock()
  });

  await screen.findByText(/вошёл в/);
});

// Метод отзыва существовал и не вызывался ниоткуда: оборвать выданный доступ было нельзя.
it('обрывает доступ и обновляет список', async () => {
  const revokeGrant = mock().mockResolvedValue(undefined);
  const listGrants = mock()
    .mockResolvedValueOnce([grant()])
    .mockResolvedValueOnce([]);
  renderSection({ issueGrant: mock(), listGrants, revokeGrant });

  fireEvent.click(await screen.findByRole('button', { name: 'Отозвать' }));
  // Кнопка подтверждения названа иначе, чем кнопка в строке: одинаковые подписи в одном дереве
  // означают, что тест (и человек с клавиатуры) не различает «открыть диалог» и «сделать».
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отзыв' }));

  await waitFor(() => expect(revokeGrant).toHaveBeenCalledWith('g1'));
  await screen.findByText('Сейчас в этот клуб никто не допущен.');
});

// Сбой загрузки и «никого нет» — разные ответы. Показать первое как второе значит уверенно
// сказать «в клубе никого», ничего при этом не зная.
it('не выдаёт сбой загрузки за пустой список', async () => {
  renderSection({
    issueGrant: mock(),
    listGrants: mock().mockRejectedValue(new PlatformApiError(500, 'boom')),
    revokeGrant: mock()
  });

  await screen.findByText('Не удалось загрузить список доступов.');
  expect(screen.queryByText('Сейчас в этот клуб никто не допущен.')).toBeNull();
});
