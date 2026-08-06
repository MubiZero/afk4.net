import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SupportAccessSection } from './SupportAccessSection';

it('выдаёт доступ и открывает админку клиента', async () => {
  const issueGrant = mock().mockResolvedValue({
    grant: { grantId: 'g1', organizationId: 'o1', reason: 'Смена не открывается', issuedAtUtc: '', expiresAtUtc: '', revokedAtUtc: null },
    ticket: 't1',
    adminUrl: 'https://admin.example/support-access?ticket=t1'
  });
  const opened: string[] = [];

  render(
    <I18nProvider><ToastProvider>
      <SupportAccessSection
        client={{ issueGrant, revokeGrant: mock() } as never}
        organizationId="o1"
        openUrl={url => opened.push(url)}
      />
    </ToastProvider></I18nProvider>
  );

  fireEvent.change(screen.getByLabelText('Причина'), {
    target: { value: 'Клуб сообщает, что не открывается смена' }
  });
  fireEvent.click(screen.getByRole('button', { name: 'Войти под клиента' }));

  await waitFor(() => expect(issueGrant).toHaveBeenCalledWith('o1', 'Клуб сообщает, что не открывается смена', 30));
  expect(opened).toEqual(['https://admin.example/support-access?ticket=t1']);
});
