import { useI18n } from '@afk4/i18n';
import { Loader2, Package, ShoppingBag, TimerReset } from 'lucide-react';
import type { PlayerPackageDto, PackageOptionDto } from '../operatorApiClients';
import { formatMinorUnits, packageOptionLabel, readNumber, readString } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import { projectPlayerPackage } from './playersModel';

// Человекочитаемые активные пакеты + инлайн-покупка. Заменяет хардкод <b>active</b>.
export function PackagesSection({
  packages,
  options,
  selectedPackageDefinitionId,
  balanceMinorUnits,
  currencyCode,
  canPurchase,
  busy,
  loading,
  onSelectOption,
  onBuy
}: {
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  selectedPackageDefinitionId: string;
  balanceMinorUnits: number;
  currencyCode: string;
  canPurchase: boolean;
  busy: boolean;
  loading: boolean;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
}) {
  const { t, locale } = useI18n();

  const selectedOption = options.find((o) => readString(o, 'packageDefinitionId') === selectedPackageDefinitionId)
    ?? options[0]
    ?? null;
  const priceMinorUnits = selectedOption === null ? 0 : readNumber(selectedOption, 'priceMinorUnits', 0);
  const optionCurrency = selectedOption === null ? currencyCode : readString(selectedOption, 'currencyCode', currencyCode);
  const includedMinutes = selectedOption === null ? 0 : Math.floor(readNumber(selectedOption, 'includedSeconds', 0) / 60);
  const bonusMinutes = selectedOption === null ? 0 : Math.floor(readNumber(selectedOption, 'bonusSeconds', 0) / 60);
  const totalMinutes = includedMinutes + bonusMinutes;
  const expiresDays = selectedOption === null ? 0 : readNumber(selectedOption, 'expiresAfterDays', 0);
  const canAfford = selectedOption !== null && balanceMinorUnits >= priceMinorUnits;
  const shortfallMinorUnits = Math.max(0, priceMinorUnits - balanceMinorUnits);
  const hasOptions = options.length > 0;

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
              <article key={view.id} className={`client-package-row${view.isExpired ? ' is-expired' : ''}`}>
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

      <div className="clients-package-buy">
        <strong className="clients-section-title">{t('op.players.packages.buyTitle')}</strong>
        {!hasOptions ? (
          <EmptyState
            icon={<ShoppingBag size={20} aria-hidden="true" />}
            title={t('op.players.packages.noOptionsTitle')}
            description={t('op.players.packages.noOptionsDescription')}
          />
        ) : (
          <>
            <label className="clients-package-select">
              {t('op.players.actions.packageSelectLabel')}
              <select
                value={selectedOption === null ? '' : readString(selectedOption, 'packageDefinitionId')}
                disabled={!canPurchase || busy}
                onChange={(e) => onSelectOption(e.currentTarget.value)}
              >
                {options.map((o) => (
                  <option key={readString(o, 'packageDefinitionId')} value={readString(o, 'packageDefinitionId')}>
                    {packageOptionLabel(o, currencyCode, t)}
                  </option>
                ))}
              </select>
            </label>
            <div className="clients-package-preview" aria-label={t('op.players.actions.packagePreviewLabel')}>
              <span>
                <strong>{t('op.players.actions.packagePrice')}</strong>
                <b>{formatMinorUnits(priceMinorUnits, optionCurrency)}</b>
              </span>
              <span>
                <strong>{t('op.players.actions.packageIncluded')}</strong>
                <b>{t('op.players.actions.packageMinShort', { minutes: includedMinutes })}</b>
              </span>
              <span>
                <strong>{t('op.players.actions.packageBonus')}</strong>
                <b>{bonusMinutes > 0 ? `+${t('op.players.actions.packageMinShort', { minutes: bonusMinutes })}` : '—'}</b>
              </span>
              <span>
                <strong>{t('op.players.actions.packageTotal')}</strong>
                <b>{t('op.players.actions.packageMinShort', { minutes: totalMinutes })}</b>
              </span>
              <span>
                <strong>{t('op.players.actions.packageExpiry')}</strong>
                <b>{expiresDays > 0 ? t('op.players.actions.packageExpiryDays', { count: expiresDays }) : t('op.players.actions.packageNoExpiry')}</b>
              </span>
            </div>
            <div className={`clients-package-deposit${canAfford ? '' : ' attention'}`}>
              <strong>{t('op.pos.payment.methodDeposit')}</strong>
              <b>{canAfford ? t('op.players.actions.depositOk') : t('op.players.actions.depositLow', { amount: formatMinorUnits(shortfallMinorUnits, optionCurrency) })}</b>
            </div>
            <button
              type="button"
              className="clients-primary-action"
              disabled={!canPurchase || busy || !canAfford}
              onClick={onBuy}
            >
              {busy
                ? <><Loader2 size={15} className="spin" aria-hidden="true" />{t('op.players.actions.buyPackagePending')}</>
                : <><TimerReset size={15} aria-hidden="true" />{t('op.players.actions.buyPackageBtn')}</>}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
