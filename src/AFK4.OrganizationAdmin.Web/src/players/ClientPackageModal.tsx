import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { PartialLoadFailure } from '../operatorPrimitives';
import { PackagePurchasePanel } from '../PackagePurchasePanel';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import type { PlayerClientItem } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';

/**
 * Продажа пакета прямо из карточки клиента.
 *
 * Пакеты в карточке были только для просмотра, а продавались в Кассе — где того же человека
 * приходилось искать заново. Один визит гостя превращался в два поиска одной карточки на двух
 * экранах, и на втором легко выбрать тёзку.
 */
export function ClientPackageModal({ backend, player, onClose, onPurchased }: {
  backend: OperatorBackendContext;
  player: PlayerClientItem & { playerAccountId: string };
  onClose: () => void;
  onPurchased: (result: PlayerPackageDto) => void | Promise<void>;
}) {
  const { t } = useI18n();
  const [options, setOptions] = useState<PackageOptionDto[] | null>(null);
  const [shiftOpen, setShiftOpen] = useState<boolean | null>(null);
  const [shiftError, setShiftError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    // Смена нужна так же, как в Кассе: пакет — это продажа, она идёт в открытую смену. Её отказ
    // раньше молча читался как «смены нет», и при открытой смене кассир видел «откройте смену».
    // Теперь пакеты остаются, продажа ждёт, а причина называется рядом.
    Promise.allSettled([
      clients.settings.getPackageOptions(backend.branchId),
      clients.shifts.getCurrentShift(backend.branchId)
    ])
      .then(([packageOptions, shift]) => {
        if (!active) return;
        if (packageOptions.status === 'fulfilled') {
          setOptions(packageOptions.value);
        } else {
          setOptions([]);
          setError(projectOperatorError(packageOptions.reason, t).detail);
        }
        if (shift.status === 'fulfilled') setShiftOpen(shift.value !== null);
        else setShiftError(projectOperatorError(shift.reason, t).detail);
      });
    return () => { active = false; };
  }, [backend.branchId, backend.config, backend.session, t]);

  const retryShift = () => {
    setShiftError(null);
    createAuthenticatedOperatorClients(backend.config, backend.session).shifts.getCurrentShift(backend.branchId)
      .then((shift) => setShiftOpen(shift !== null))
      .catch((reason) => setShiftError(projectOperatorError(reason, t).detail));
  };

  return (
    <PanelModal
      title={t('op.players.packages.sellTitle')}
      subtitle={player.name}
      onClose={onClose}
    >
      {error !== null && <p role="alert">{error}</p>}
      {shiftError !== null && (
        <PartialLoadFailure text={t('op.players.packages.shiftFailed', { reason: shiftError })} onRetry={retryShift} />
      )}
      {options === null ? (
        <p>{t('state.loading')}</p>
      ) : options.length === 0 && error === null ? (
        <p>{t('op.players.packages.noOptions')}</p>
      ) : (
        <PackagePurchasePanel
          backend={backend}
          player={player}
          options={options}
          shiftOpen={shiftOpen}
          onPurchased={onPurchased}
        />
      )}
    </PanelModal>
  );
}
