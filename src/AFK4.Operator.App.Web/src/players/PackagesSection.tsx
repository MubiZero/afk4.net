import { useI18n } from '@afk4/i18n';
import type { PlayerPackageDto } from '../operatorApiClients';
import { projectPlayerPackage } from './playersModel';

export function PackagesSection({ packages, loading, errorDetail }: { packages: PlayerPackageDto[]; loading: boolean; errorDetail?: string }) {
  const { t, locale } = useI18n();
  if (loading) return <section><strong>{t('op.players.profile.packagesLabel')}</strong><p>{t('state.loading')}</p></section>;
  if (errorDetail) return <section><strong>{t('op.players.profile.packagesLabel')}</strong><p role="alert">{errorDetail}</p></section>;
  if (packages.length === 0) return <section><strong>{t('op.players.profile.packagesLabel')}</strong><p>{t('op.players.packages.emptyTitle')}</p></section>;
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
