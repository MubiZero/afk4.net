import { useI18n } from '@afk4/i18n';
import type { PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { projectPlayerPackage } from './playersModel';
import { DeferredSkeleton, SkeletonLine } from '../LoadingSkeleton';

export function PackagesSection({ packages, loading, errorDetail, canSellPackage = false, onSellPackage }: {
  packages: PlayerPackageDto[];
  loading: boolean;
  errorDetail?: string;
  canSellPackage?: boolean;
  onSellPackage?: () => void;
}) {
  const { t, locale } = useI18n();
  // Заголовок от ответа не зависит и стоит сразу; на месте пакетов — пакет той же разметки. Название,
  // минуты и срок идут одной строкой текста, и в ширину карточки она переносится на вторую.
  if (loading) {
    return (
      <section className="clients-packages-section">
        <strong>{t('op.players.profile.packagesLabel')}</strong>
        <DeferredSkeleton><article data-skeleton="list" aria-hidden="true"><SkeletonLine width="90%" /><SkeletonLine width="45%" /></article></DeferredSkeleton>
      </section>
    );
  }
  if (errorDetail) return <section><strong>{t('op.players.profile.packagesLabel')}</strong><p role="alert">{errorDetail}</p></section>;
  if (packages.length === 0) {
    return (
      <section>
        <strong>{t('op.players.profile.packagesLabel')}</strong>
        <EmptyState
          inline
          title={t('op.players.packages.emptyTitle')}
          next={canSellPackage && onSellPackage
            ? { kind: 'action', label: t('op.players.packages.sellBtn'), onClick: onSellPackage }
            : { kind: 'calm', hint: t('op.players.packages.emptyHint') }}
        />
      </section>
    );
  }
  return (
    <section className="clients-packages-section">
      <strong>{t('op.players.profile.packagesLabel')}</strong>
      {packages.map((pkg) => {
        const view = projectPlayerPackage(pkg, t, locale);
        return <article key={view.id}>
          <b>{view.name}</b>
          <span>{t('op.players.packages.includedMinutes', { minutes: view.remainingIncludedMinutes })}</span>
          <span>{t('op.players.packages.bonusMinutes', { minutes: view.remainingBonusMinutes })}</span>
          <span>{view.expiryLabel ? t('op.players.packages.expiresOn', { date: view.expiryLabel }) : t('op.players.packages.perpetual')}</span>
          <em>{view.isExpired ? t('op.players.packages.expired') : t('op.players.status.active')}</em>
        </article>;
      })}
    </section>
  );
}
