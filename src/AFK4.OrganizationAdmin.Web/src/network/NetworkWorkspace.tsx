import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import { allowedNetworkDestinations, type NetworkDestinationId } from './networkNav';
import { BranchesDestination } from './branches/BranchesDestination';
import { BillingDestination } from './billing/BillingDestination';
import { InstallDestination } from './install/InstallDestination';
import { JournalDestination } from './journal/JournalDestination';

// Каркас раздела «Сеть»: левый рейл разделов, доступных сессии (Филиалы/Подписка/Установка/
// Журнал), + активный экран раздела справа — тот же паттерн, что ManagementWorkspace. В отличие
// от Управления здесь нет общей settings-domain загрузки на уровне хоста: каждый экран сам себе
// хозяин (наполняется в Task 5-8), поэтому lift-состояние (dirty/settingsLoadStatus) сюда не
// перенесено — эти данные никак не пересекаются между Филиалами/Подпиской/Установкой/Журналом.
export function NetworkWorkspace({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const destinations = allowedNetworkDestinations(session);
  const [active, setActive] = useState<NetworkDestinationId | null>(destinations[0]?.id ?? null);

  if (destinations.length === 0) {
    return (
      <main className="workspace-screen">
        <EmptyState title={t('op.network.noAccess')} />
      </main>
    );
  }

  // Сессия могла лишиться прав между рендерами — активный раздел мог перестать быть доступным.
  // Падаем на первый разрешённый вместо пустого экрана (см. тот же приём в ManagementWorkspace).
  const currentId: NetworkDestinationId = destinations.some((destination) => destination.id === active)
    ? (active as NetworkDestinationId)
    : destinations[0].id;

  function renderActive(): JSX.Element {
    switch (currentId) {
      case 'branches':
        return <BranchesDestination backend={backend} />;
      case 'billing':
        return <BillingDestination backend={backend} />;
      case 'install':
        return <InstallDestination backend={backend} />;
      case 'journal':
        return <JournalDestination backend={backend} />;
    }
  }

  return (
    <div className="management-layout">
      <nav className="management-nav">
        {destinations.map((destination) => {
          const Icon = destination.Icon;
          return (
            <button
              key={destination.id}
              type="button"
              className={destination.id === currentId ? 'active' : undefined}
              onClick={() => setActive(destination.id)}
            >
              <Icon size={16} aria-hidden="true" />
              <span>{t(destination.labelKey)}</span>
            </button>
          );
        })}
      </nav>

      <div className="management-active-pane">{renderActive()}</div>
    </div>
  );
}
