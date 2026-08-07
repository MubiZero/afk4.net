import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OrganizationDynamicsTab } from './OrganizationDynamicsTab';
import type { BranchDynamics, BranchDynamicsDay, OrganizationBranch } from '@/api/types';

function branch(overrides: Partial<OrganizationBranch> = {}): OrganizationBranch {
  return {
    branchId: 'branch-1',
    slug: 'main',
    name: 'Главный клуб',
    city: 'Душанбе',
    createdAtUtc: '2026-01-01T00:00:00Z',
    ...overrides
  };
}

function day(overrides: Partial<BranchDynamicsDay> = {}): BranchDynamicsDay {
  return {
    date: '2026-08-01',
    sessionCount: 4,
    revenue: { currencyCode: 'TJS', minorUnits: 12_345 },
    shiftOpenedCount: 1,
    agentAlive: true,
    ...overrides
  };
}

function dynamics(overrides: Partial<BranchDynamics> = {}): BranchDynamics {
  return {
    organizationId: 'org-1',
    branchId: 'branch-1',
    fromDate: '2026-07-02',
    toDate: '2026-08-01',
    totalRevenue: { currencyCode: 'TJS', minorUnits: 12_345 },
    totalSessionCount: 4,
    daysWithoutAgent: 0,
    daysWithUnknownAgent: 0,
    missingDayCount: 0,
    days: [day()],
    ...overrides
  };
}

describe('OrganizationDynamicsTab', () => {
  it('рисует сводку и сноску о суточной точности при успешном ответе', async () => {
    const client = { getBranchDynamics: mock().mockResolvedValue(dynamics()) };
    render(<I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branch()]} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Выручка по дням')).toBeInTheDocument());
    expect(screen.getByText('Выручка за период')).toBeInTheDocument();
    expect(screen.getByText('Сеансов')).toBeInTheDocument();
    expect(screen.getByText(/проверяется раз в сутки/)).toBeInTheDocument();
  });

  it('показывает «нет данных о связи» отдельно от «не выходил на связь»', async () => {
    const client = {
      getBranchDynamics: mock().mockResolvedValue(dynamics({
        daysWithoutAgent: 2,
        daysWithUnknownAgent: 3,
        days: [
          day({ date: '2026-07-30', agentAlive: false }),
          day({ date: '2026-07-31', agentAlive: null }),
          day({ date: '2026-08-01', agentAlive: true })
        ]
      }))
    };
    render(<I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branch()]} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Клуб не выходил на связь')).toBeInTheDocument());
    expect(screen.getByText('Нет данных о связи')).toBeInTheDocument();
    expect(screen.getByText(/дня без связи/)).toBeInTheDocument();
    expect(screen.getByText(/дня без наблюдения/)).toBeInTheDocument();
  });

  it('на ошибке показывает сообщение и кнопку повтора, а не «нет данных»', async () => {
    const client = { getBranchDynamics: mock().mockRejectedValue(new Error('network down')) };
    render(<I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branch()]} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Не удалось загрузить историю клуба')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.queryByText('За последние 30 дней снимков по этому клубу нет')).not.toBeInTheDocument();
  });

  it('ни одна подпись на экране не содержит символа \'{\' (регрессия на ICU-шаблон, попавший в UI сырым)', async () => {
    const client = {
      getBranchDynamics: mock().mockResolvedValue(dynamics({
        daysWithoutAgent: 1,
        daysWithUnknownAgent: 1,
        missingDayCount: 1,
        days: [day({ agentAlive: false }), day({ date: '2026-08-02', agentAlive: null })]
      }))
    };
    const { container } = render(<I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branch()]} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Выручка по дням')).toBeInTheDocument());
    expect(container.textContent).not.toContain('{');
  });

  it('переключение клуба перезапрашивает данные и не даёт устаревшему ответу перетереть новый', async () => {
    const branchA = branch({ branchId: 'branch-a', name: 'Клуб А' });
    const branchB = branch({ branchId: 'branch-b', name: 'Клуб Б' });

    let resolveA: (value: BranchDynamics) => void = () => {};
    let resolveB: (value: BranchDynamics) => void = () => {};
    const getBranchDynamics = mock((_organizationId: string, branchId: string) => {
      if (branchId === 'branch-a') return new Promise<BranchDynamics>(resolve => { resolveA = resolve; });
      return new Promise<BranchDynamics>(resolve => { resolveB = resolve; });
    });
    const client = { getBranchDynamics };

    render(<I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branchA, branchB]} /></I18nProvider>);

    await waitFor(() => expect(getBranchDynamics).toHaveBeenCalledWith('org-1', 'branch-a', 30));

    // Переключаемся на клуб Б до того, как первый запрос успел ответить.
    fireEvent.change(screen.getByLabelText('Клуб'), { target: { value: 'branch-b' } });
    await waitFor(() => expect(getBranchDynamics).toHaveBeenCalledWith('org-1', 'branch-b', 30));

    // Устаревший ответ по клубу А приходит ПОСЛЕ переключения — он не должен попасть на экран.
    resolveB(dynamics({ branchId: 'branch-b', totalSessionCount: 7 }));
    resolveA(dynamics({ branchId: 'branch-a', totalSessionCount: 999 }));

    await waitFor(() => expect(screen.getByText('7')).toBeInTheDocument());
    expect(screen.queryByText('999')).not.toBeInTheDocument();
  });

  it('смена организации на смонтированном компоненте не оставляет чужой branchId', async () => {
    const branchA = branch({ branchId: 'branch-a', name: 'Клуб А' });
    const branchC = branch({ branchId: 'branch-c', name: 'Клуб В' });
    const getBranchDynamics = mock().mockResolvedValue(dynamics());
    const client = { getBranchDynamics };

    const { rerender } = render(
      <I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-1" branches={[branchA]} /></I18nProvider>
    );
    await waitFor(() => expect(getBranchDynamics).toHaveBeenCalledWith('org-1', 'branch-a', 30));

    // Та же смонтированная вкладка получает другую организацию с другим набором филиалов —
    // TabBoundary.resetKey не размонтирует детей, поэтому это обычный сценарий переключения.
    rerender(
      <I18nProvider><OrganizationDynamicsTab client={client} organizationId="org-2" branches={[branchC]} /></I18nProvider>
    );

    await waitFor(() => expect(getBranchDynamics).toHaveBeenCalledWith('org-2', 'branch-c', 30));
    expect(getBranchDynamics).not.toHaveBeenCalledWith('org-2', 'branch-a', 30);
  });
});
