import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Archive, Package as PackageIcon, Pencil } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import type { RowAction } from '../../kit/types';
import { PanelModal } from '../../../PanelModal';
import { CriticalActionConfirmation, Money } from '../../../operatorPrimitives';
import { projectOperatorError } from '../../../apiErrors';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  formatMoneyInputMinorUnits,
  isGuid,
  parseMoneyInputMinorUnits,
  readBoolean,
  readNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { PackageOptionDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

type PackageOption = Record<string, unknown>;

interface RetirePackageAction {
  packageDefinitionId: string;
  name: string;
}

// Вкладка «Пакеты» раздела «Тарифы и пакеты»: список пакетов времени + drawer-редактор выбранного
// пакета. Портировано из прежней формы настроек, удалённой вместе с разделом Settings:
// createPackageDefinition/
// updatePackageDefinition 1:1 (те же вызовы/аргументы/идемпотентные ключи, гейт managePackages) —
// см. TariffsTab.tsx для парного домена «Тарифы». Минуты хранятся в СЕКУНДАХ (×60/÷60 на границе
// UI). Создание — PanelModal, редактирование — прямо в drawer, «Снять с продажи» — через
// CriticalActionConfirmation (в оригинале подтверждения не было).
export function PackagesTab({
  packageOptions,
  currencyCode,
  backend,
  canManagePackages,
  onReload,
  onFeedback
}: {
  packageOptions: PackageOptionDto[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  canManagePackages: boolean;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [retireAction, setRetireAction] = useState<RetirePackageAction | null>(null);
  const [name, setName] = useState('');
  const [price, setPrice] = useState('250.00');
  const [minutes, setMinutes] = useState('300');
  const [bonusMinutes, setBonusMinutes] = useState('30');
  const [expiresDays, setExpiresDays] = useState('30');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setSelectedPackageDefinitionId((current) => (current && packageOptions.some((option) => readString(option, 'packageDefinitionId') === current) ? current : null));
  }, [packageOptions]);

  const selectedPackage = packageOptions.find((option) => readString(option, 'packageDefinitionId') === selectedPackageDefinitionId) ?? null;

  useEffect(() => {
    if (!selectedPackage) return;
    setName(readString(selectedPackage, 'name'));
    setPrice(formatMoneyInputMinorUnits(readNumber(selectedPackage, 'priceMinorUnits', 0)));
    setMinutes(String(Math.round(readNumber(selectedPackage, 'includedSeconds', 0) / 60)));
    setBonusMinutes(String(Math.round(readNumber(selectedPackage, 'bonusSeconds', 0) / 60)));
    setExpiresDays(String(readNumber(selectedPackage, 'expiresAfterDays', 30)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedPackageDefinitionId]);

  const openCreate = () => {
    setName(t('op.settings.prefill.packageName'));
    setPrice('250.00');
    setMinutes('300');
    setBonusMinutes('30');
    setExpiresDays('30');
    setCreateOpen(true);
  };

  const submitCreate = async () => {
    const label = t('op.settings.action.createPackage');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
        throw new Error(t('op.settings.packages.error.noPerm'));
      }

      const trimmedName = name.trim();
      const priceMinorUnits = parseMoneyInputMinorUnits(price);
      const includedMinutes = Number(minutes);
      const bonusMinutesValue = Number(bonusMinutes);
      const expiresDaysValue = Number(expiresDays);
      if (!trimmedName || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
        || !Number.isInteger(bonusMinutesValue) || bonusMinutesValue < 0
        || !Number.isInteger(expiresDaysValue) || expiresDaysValue <= 0) {
        throw new Error(t('op.settings.packages.error.fillCreate'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.createPackageDefinition(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: trimmedName,
        price: { currencyCode, minorUnits: priceMinorUnits },
        includedSeconds: includedMinutes * 60,
        bonusSeconds: bonusMinutesValue * 60,
        expiresAfterDays: expiresDaysValue,
        idempotencyKey: createIdempotencyKey('package-definition-create')
      });
      setCreateOpen(false);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitEdit = async () => {
    if (!selectedPackage) return;
    const label = t('op.settings.action.updatePackage');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
        throw new Error(t('op.settings.packages.error.noPerm'));
      }

      const packageDefinitionId = readString(selectedPackage, 'packageDefinitionId');
      const trimmedName = name.trim();
      const priceMinorUnits = parseMoneyInputMinorUnits(price);
      const includedMinutes = Number(minutes);
      const bonusMinutesValue = Number(bonusMinutes);
      const expiresDaysValue = Number(expiresDays);
      if (!isGuid(packageDefinitionId) || !trimmedName || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
        || !Number.isInteger(bonusMinutesValue) || bonusMinutesValue < 0
        || !Number.isInteger(expiresDaysValue) || expiresDaysValue <= 0) {
        throw new Error(t('op.settings.packages.error.fillUpdate'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
        organizationId: nextBackend.session.organizationId,
        name: trimmedName,
        price: { currencyCode, minorUnits: priceMinorUnits },
        includedSeconds: includedMinutes * 60,
        bonusSeconds: bonusMinutesValue * 60,
        expiresAfterDays: expiresDaysValue,
        isActive: true
      });
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmRetire = async () => {
    if (!retireAction) return;
    const label = t('op.settings.action.retirePackage');
    const { packageDefinitionId } = retireAction;
    setRetireAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
        throw new Error(t('op.settings.packages.error.noPerm'));
      }

      const packageOption = packageOptions.find((option) => readString(option, 'packageDefinitionId') === packageDefinitionId);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
        organizationId: nextBackend.session.organizationId,
        name: readString(packageOption, 'name', name),
        price: { currencyCode: readString(packageOption, 'currencyCode', currencyCode), minorUnits: readNumber(packageOption, 'priceMinorUnits', 0) },
        includedSeconds: readNumber(packageOption, 'includedSeconds', 0),
        bonusSeconds: readNumber(packageOption, 'bonusSeconds', 0),
        expiresAfterDays: readNumber(packageOption, 'expiresAfterDays', 1),
        isActive: false
      });
      setSelectedPackageDefinitionId((current) => (current === packageDefinitionId ? null : current));
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const rowActions = canManagePackages
    ? (option: PackageOption): RowAction[] => [
      {
        id: 'edit',
        label: t('op.settings.action.updatePackage'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'))
      },
      {
        id: 'retire',
        label: t('op.settings.action.retirePackage'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        disabled: !readBoolean(option, 'isActive', true),
        onSelect: () => setRetireAction({
          packageDefinitionId: readString(option, 'packageDefinitionId'),
          name: readString(option, 'name', t('op.settings.packages.packageFallback'))
        })
      }
    ]
    : undefined;

  const drawerActions: RowAction[] = selectedPackage && canManagePackages && readBoolean(selectedPackage, 'isActive', true)
    ? [
      {
        id: 'retire',
        label: t('op.settings.action.retirePackage'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setRetireAction({
          packageDefinitionId: readString(selectedPackage, 'packageDefinitionId'),
          name: readString(selectedPackage, 'name', t('op.settings.packages.packageFallback'))
        })
      }
    ]
    : [];

  return (
    <>
      <div className="mgmt-master-detail">
        <MgmtTable<PackageOption>
          columns={[
            { key: 'name', header: t('op.management.tariffs.col.name'), render: (option) => readString(option, 'name', t('op.settings.packages.packageFallback')) },
            {
              key: 'price',
              header: t('op.management.tariffs.col.price'),
              align: 'end',
              render: (option) => <Money minorUnits={readNumber(option, 'priceMinorUnits', 0)} currencyCode={readString(option, 'currencyCode', currencyCode)} />
            },
            { key: 'minutes', header: t('op.management.tariffs.col.minutes'), align: 'end', render: (option) => String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)) },
            { key: 'bonus', header: t('op.management.tariffs.col.bonus'), align: 'end', render: (option) => String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)) },
            { key: 'expires', header: t('op.management.tariffs.col.expiresDays'), align: 'end', render: (option) => String(readNumber(option, 'expiresAfterDays', 0)) },
            {
              key: 'status',
              header: t('op.management.tariffs.col.status'),
              render: (option) => readBoolean(option, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive')
            }
          ]}
          rows={packageOptions}
          rowKey={(option) => readString(option, 'packageDefinitionId')}
          gridTemplate="1.4fr 1fr 0.7fr 0.7fr 0.8fr 0.8fr"
          selectedKey={selectedPackageDefinitionId}
          onSelectRow={(option) => setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'))}
          rowActions={rowActions}
          toolbar={{
            title: t('op.settings.packages.title'),
            primary: canManagePackages ? { label: t('op.management.tariffs.addPackageCta'), onClick: openCreate } : undefined
          }}
          empty={{
            icon: <PackageIcon size={22} aria-hidden="true" />,
            title: t('op.management.tariffs.packagesEmpty.title'),
            description: t('op.management.tariffs.packagesEmpty.description'),
            action: canManagePackages ? { label: t('op.management.tariffs.addPackageCta'), onClick: openCreate } : undefined
          }}
        />

        {selectedPackage && (
          <MgmtDrawer
            title={readString(selectedPackage, 'name', t('op.settings.packages.packageFallback'))}
            subtitle={readBoolean(selectedPackage, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive')}
            actions={drawerActions}
            onClose={() => setSelectedPackageDefinitionId(null)}
            footer={
              canManagePackages ? (
                <div className="mgmt-form-actions">
                  <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void submitEdit()}>
                    {t('common.save')}
                  </button>
                </div>
              ) : undefined
            }
          >
            <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitEdit(); }}>
              <div className="mgmt-form-grid">
                <label>{t('op.settings.packages.name')}
                  <input value={name} disabled={!canManagePackages || busy} onChange={(event) => setName(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.packages.price')}
                  <input inputMode="decimal" value={price} disabled={!canManagePackages || busy} onChange={(event) => setPrice(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.packages.minutes')}
                  <input inputMode="numeric" value={minutes} disabled={!canManagePackages || busy} onChange={(event) => setMinutes(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.packages.bonusMinutes')}
                  <input inputMode="numeric" value={bonusMinutes} disabled={!canManagePackages || busy} onChange={(event) => setBonusMinutes(event.currentTarget.value)} />
                </label>
                <label>{t('op.settings.packages.expiresDays')}
                  <input inputMode="numeric" value={expiresDays} disabled={!canManagePackages || busy} onChange={(event) => setExpiresDays(event.currentTarget.value)} />
                </label>
              </div>
            </form>
          </MgmtDrawer>
        )}
      </div>

      {createOpen && (
        <PanelModal title={t('op.management.tariffs.packageModal.createTitle')} onClose={() => setCreateOpen(false)} closeDisabled={busy}>
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitCreate(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.packages.name')}
                <input value={name} onChange={(event) => setName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.packages.price')}
                <input inputMode="decimal" value={price} onChange={(event) => setPrice(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.packages.minutes')}
                <input inputMode="numeric" value={minutes} onChange={(event) => setMinutes(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.packages.bonusMinutes')}
                <input inputMode="numeric" value={bonusMinutes} onChange={(event) => setBonusMinutes(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.packages.expiresDays')}
                <input inputMode="numeric" value={expiresDays} onChange={(event) => setExpiresDays(event.currentTarget.value)} />
              </label>
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setCreateOpen(false)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>{t('op.settings.action.createPackage')}</button>
            </div>
          </form>
        </PanelModal>
      )}

      {retireAction && (
        <CriticalActionConfirmation
          title={t('op.management.tariffs.confirmRetirePackage.title')}
          detail={retireAction.name}
          impact={t('op.management.tariffs.confirmRetirePackage.impact')}
          confirmLabel={t('op.settings.action.retirePackage')}
          onCancel={() => setRetireAction(null)}
          onConfirm={() => void confirmRetire()}
        />
      )}
    </>
  );
}
