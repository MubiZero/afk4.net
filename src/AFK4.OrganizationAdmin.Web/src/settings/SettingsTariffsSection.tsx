import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import type { PackageOptionDto, TariffOptionDto } from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames } from '../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  formatMinorUnits,
  formatMoneyInputMinorUnits,
  isGuid,
  parseMoneyInputMinorUnits,
  readBoolean,
  readNumber,
  readString,
  requireBackend,
  triggerFeedback
} from '../operatorHelpers';

// Раздел «Тарифы и пакеты»: владеет своими формами/выбором и своими мутациями. Родитель отдаёт
// загруженные tariffs/packageOptions (server-data, нужны и readiness-панели) + onReload/onFeedback.
export function SettingsTariffsSection({
  tariffs,
  packageOptions,
  currencyCode,
  backend,
  canManageTariffs,
  canManagePackages,
  onReload,
  onFeedback
}: {
  tariffs: TariffOptionDto[];
  packageOptions: PackageOptionDto[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  canManageTariffs: boolean;
  canManagePackages: boolean;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();

  const createTariffActionKey = t('op.settings.action.createTariff');
  const updateTariffActionKey = t('op.settings.action.updateTariff');
  const retireTariffActionKey = t('op.settings.action.retireTariff');
  const createPackageActionKey = t('op.settings.action.createPackage');
  const updatePackageActionKey = t('op.settings.action.updatePackage');
  const retirePackageActionKey = t('op.settings.action.retirePackage');

  const [tariffName, setTariffName] = useState(t('op.settings.prefill.tariffName'));
  const [tariffPricePerHour, setTariffPricePerHour] = useState('90.00');
  const [tariffMinimumMinutes, setTariffMinimumMinutes] = useState('15');
  const [tariffRoundingMinutes, setTariffRoundingMinutes] = useState('5');
  const [tariffEffectiveFromUtc, setTariffEffectiveFromUtc] = useState(() => new Date().toISOString());
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [packageName, setPackageName] = useState(t('op.settings.prefill.packageName'));
  const [packagePrice, setPackagePrice] = useState('250.00');
  const [packageMinutes, setPackageMinutes] = useState('300');
  const [packageBonusMinutes, setPackageBonusMinutes] = useState('30');
  const [packageExpiresDays, setPackageExpiresDays] = useState('30');
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');

  // Засев выбора из загруженных данных: сохранить текущий, если он ещё есть, иначе сбросить
  // (раньше это делал loadSettings в родителе).
  useEffect(() => {
    setSelectedTariffVersionId((current) => tariffs.some((tariff) => readString(tariff, 'tariffVersionId') === current) ? current : '');
  }, [tariffs]);
  useEffect(() => {
    setSelectedPackageDefinitionId((current) => packageOptions.some((option) => readString(option, 'packageDefinitionId') === current) ? current : '');
  }, [packageOptions]);

  const selectTariffOption = (option: TariffOptionDto) => {
    setSelectedTariffVersionId(readString(option, 'tariffVersionId'));
    setTariffName(readString(option, 'name', tariffName));
    setTariffPricePerHour(formatMoneyInputMinorUnits(readNumber(option, 'pricePerMinuteMinorUnits', 0) * 60));
    setTariffMinimumMinutes(String(readNumber(option, 'minimumBillableMinutes', Number(tariffMinimumMinutes))));
    setTariffRoundingMinutes(String(readNumber(option, 'roundingIncrementMinutes', Number(tariffRoundingMinutes))));
    setTariffEffectiveFromUtc(readString(option, 'effectiveFromUtc', new Date().toISOString()));
    triggerFeedback(onFeedback, readString(option, 'name', t('op.settings.tariffs.tariffFallback')), 'confirmed');
  };
  const selectPackageOption = (option: PackageOptionDto) => {
    setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'));
    setPackageName(readString(option, 'name', packageName));
    setPackagePrice(formatMoneyInputMinorUnits(readNumber(option, 'priceMinorUnits', 0)));
    setPackageMinutes(String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)));
    setPackageBonusMinutes(String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)));
    setPackageExpiresDays(String(readNumber(option, 'expiresAfterDays', 30)));
    triggerFeedback(onFeedback, readString(option, 'name', t('op.settings.packages.packageFallback')), 'confirmed');
  };

  const runAction = async (label: string) => {
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === createTariffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error(t('op.settings.tariffs.error.noPerm'));
        }

        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error(t('op.settings.tariffs.error.fillCreate'));
        }

        const tariff = await apiClients.settings.createTariff(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          idempotencyKey: createIdempotencyKey('tariff-create')
        });
        const tariffId = readString(tariff, 'tariffId');
        if (tariffId) {
          await apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
            organizationId: nextBackend.session.organizationId,
            tariffId,
            currencyCode,
            pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
            minimumBillableMinutes,
            roundingIncrementMinutes,
            effectiveFromUtc: new Date().toISOString(),
            idempotencyKey: createIdempotencyKey('tariff-version-create')
          });
        }
        setTariffName(t('op.settings.prefill.tariffNameIndexed', { n: tariffs.length + 2 })); // editable prefill
        setTariffEffectiveFromUtc(new Date().toISOString());
        await onReload(nextBackend);
      } else if (label === updateTariffActionKey || label === retireTariffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error(t('op.settings.tariffs.error.noPerm'));
        }

        const tariffOption = tariffs.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId);
        const tariffId = readString(tariffOption, 'tariffId');
        const tariffVersionId = readString(tariffOption, 'tariffVersionId');
        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!isGuid(tariffId) || !isGuid(tariffVersionId) || !name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error(t('op.settings.tariffs.error.fillUpdate'));
        }

        const isActive = label !== retireTariffActionKey;
        await apiClients.settings.updateTariff(nextBackend.branchId, tariffId, {
          organizationId: nextBackend.session.organizationId,
          name,
          isActive
        });
        await apiClients.settings.updateTariffVersion(nextBackend.branchId, tariffId, tariffVersionId, {
          organizationId: nextBackend.session.organizationId,
          currencyCode,
          pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
          minimumBillableMinutes,
          roundingIncrementMinutes,
          effectiveFromUtc: tariffEffectiveFromUtc,
          isActive
        });
        if (!isActive) {
          setSelectedTariffVersionId('');
        }
        await onReload(nextBackend);
      } else if (label === createPackageActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error(t('op.settings.packages.error.noPerm'));
        }

        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error(t('op.settings.packages.error.fillCreate'));
        }

        await apiClients.settings.createPackageDefinition(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          idempotencyKey: createIdempotencyKey('package-definition-create')
        });
        await onReload(nextBackend);
      } else if (label === updatePackageActionKey || label === retirePackageActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error(t('op.settings.packages.error.noPerm'));
        }

        const packageDefinitionId = selectedPackageDefinitionId.trim();
        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!isGuid(packageDefinitionId) || !name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error(t('op.settings.packages.error.fillUpdate'));
        }

        await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          isActive: label !== retirePackageActionKey
        });
        if (label === retirePackageActionKey) {
          setSelectedPackageDefinitionId('');
        }
        await onReload(nextBackend);
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.tariffs.title')}</span>
        <div className="settings-section-actions">
          <button type="button" disabled={!canManageTariffs} onClick={() => runAction(createTariffActionKey)}>{createTariffActionKey}</button>
          <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runAction(updateTariffActionKey)}>{updateTariffActionKey}</button>
          <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runAction(retireTariffActionKey)}>{retireTariffActionKey}</button>
        </div>
      </div>
      <div className="settings-form-grid settings-tariff-form">
        <label>{t('op.settings.tariffs.name')}<input value={tariffName} disabled={!canManageTariffs} onChange={(event) => setTariffName(event.currentTarget.value)} /></label>
        <label>{t('op.settings.tariffs.pricePerHour')}<input inputMode="decimal" value={tariffPricePerHour} disabled={!canManageTariffs} onChange={(event) => setTariffPricePerHour(event.currentTarget.value)} /></label>
        <label>{t('op.settings.tariffs.minMinutes')}<input inputMode="numeric" value={tariffMinimumMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffMinimumMinutes(event.currentTarget.value)} /></label>
        <label>{t('op.settings.tariffs.rounding')}<input inputMode="numeric" value={tariffRoundingMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffRoundingMinutes(event.currentTarget.value)} /></label>
      </div>
      <div className="settings-tariff-list">
        {tariffs.map((tariff) => (
          <button key={readString(tariff, 'tariffVersionId')} type="button" className={`settings-tariff-row ${readString(tariff, 'tariffVersionId') === selectedTariffVersionId ? 'active' : ''}`} onClick={() => selectTariffOption(tariff)}>
            <strong>{readString(tariff, 'name', t('op.settings.tariffs.tariffFallback'))}</strong>
            <b>{formatMinorUnits(readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60, readString(tariff, 'currencyCode', currencyCode))} {t('op.settings.tariffs.perHour')}</b>
            <span>{t('op.settings.tariffs.minLabel', { min: readNumber(tariff, 'minimumBillableMinutes', 0), step: readNumber(tariff, 'roundingIncrementMinutes', 0), state: readBoolean(tariff, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive') })}</span>
          </button>
        ))}
      </div>
      <div className="settings-section-title">
        <span>{t('op.settings.packages.title')}</span>
        <div className="settings-section-actions">
          <button type="button" disabled={!canManagePackages} onClick={() => runAction(createPackageActionKey)}>{createPackageActionKey}</button>
          <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runAction(updatePackageActionKey)}>{updatePackageActionKey}</button>
          <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runAction(retirePackageActionKey)}>{retirePackageActionKey}</button>
        </div>
      </div>
      <div className="settings-tariff-list">
        {packageOptions.map((option) => (
          <button key={readString(option, 'packageDefinitionId')} type="button" className={`settings-tariff-row ${readString(option, 'packageDefinitionId') === selectedPackageDefinitionId ? 'active' : ''}`} onClick={() => selectPackageOption(option)}>
            <strong>{readString(option, 'name', t('op.settings.packages.packageFallback'))}</strong>
            <b>{formatMinorUnits(readNumber(option, 'priceMinorUnits', 0), readString(option, 'currencyCode', currencyCode))}</b>
            <span>{t('op.settings.packages.minutesDetail', { minutes: Math.round(readNumber(option, 'includedSeconds', 0) / 60), bonus: Math.round(readNumber(option, 'bonusSeconds', 0) / 60), days: readNumber(option, 'expiresAfterDays', 0) })}</span>
          </button>
        ))}
      </div>
      <div className="settings-form-grid settings-package-form">
        <label>{t('op.settings.packages.name')}<input value={packageName} disabled={!canManagePackages} onChange={(event) => setPackageName(event.currentTarget.value)} /></label>
        <label>{t('op.settings.packages.price')}<input inputMode="decimal" value={packagePrice} disabled={!canManagePackages} onChange={(event) => setPackagePrice(event.currentTarget.value)} /></label>
        <label>{t('op.settings.packages.minutes')}<input inputMode="numeric" value={packageMinutes} disabled={!canManagePackages} onChange={(event) => setPackageMinutes(event.currentTarget.value)} /></label>
        <label>{t('op.settings.packages.bonusMinutes')}<input inputMode="numeric" value={packageBonusMinutes} disabled={!canManagePackages} onChange={(event) => setPackageBonusMinutes(event.currentTarget.value)} /></label>
        <label>{t('op.settings.packages.expiresDays')}<input inputMode="numeric" value={packageExpiresDays} disabled={!canManagePackages} onChange={(event) => setPackageExpiresDays(event.currentTarget.value)} /></label>
      </div>
    </>
  );
}
