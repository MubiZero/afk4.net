import { useI18n } from '@afk4/i18n';
import { Package } from 'lucide-react';
import type { PlayerPackageDto } from '../operatorApiClients';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import { projectPlayerPackage } from './playersModel';

// Человекочитаемые активные пакеты клиента (read-only показ). Продажа пакетов — в Кассе.
export function PackagesSection({
  packages,
  loading
}: {
  packages: PlayerPackageDto[];
  loading: boolean;
}) {
  const { t, locale } = useI18n();

  return (
    <div className="clients-packages-section">
      {loading ? (
        <div className="client-package-list" aria-busy="true">
          <Skeleton className="client-package-skeleton" />
          <Skeleton className="client-package-skeleton" />
        </div>
      ) : packages.length === 0 ? (
        <EmptyState
          icon={<Package size={20} aria-hidden="true" />}
          title={t('op.players.packages.emptyTitle')}
          description={t('op.players.packages.emptyDescription')}
        />
      ) : (
        <div className="client-package-list" aria-label={t('op.players.profile.packagesLabel')}>
          {packages.map((raw) => {
            const view = projectPlayerPackage(raw, t, locale);
            const expiry = view.isExpired
              ? t('op.players.packages.expired')
              : view.expiryLabel
                ? t('op.players.packages.expiresOn', { date: view.expiryLabel })
                : t('op.players.packages.perpetual');
            return (
              <article key={view.id} className={`ui-card client-package-row${view.isExpired ? ' is-expired' : ''}`}>
                <strong>{view.name}</strong>
                <span>{t('op.players.packages.includedMinutes', { minutes: view.remainingIncludedMinutes })}</span>
                {view.remainingBonusMinutes > 0 && (
                  <span className="client-package-bonus">
                    {t('op.players.packages.bonusMinutes', { minutes: view.remainingBonusMinutes })}
                  </span>
                )}
                <b>{expiry}</b>
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}
