import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
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
  const [shiftOpen, setShiftOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    // Смена нужна так же, как в Кассе: пакет — это продажа, она идёт в открытую смену.
    Promise.all([
      clients.settings.getPackageOptions(backend.branchId),
      clients.shifts.getCurrentShift(backend.branchId).catch(() => null)
    ])
      .then(([packageOptions, shift]) => {
        if (!active) return;
        setOptions(packageOptions);
        setShiftOpen(shift !== null);
      })
      .catch((reason) => {
        if (!active) return;
        setOptions([]);
        setError(projectOperatorError(reason, t).detail);
      });
    return () => { active = false; };
  }, [backend.branchId, backend.config, backend.session, t]);

  return (
    <PanelModal
      title={t('op.players.packages.sellTitle')}
      subtitle={player.name}
      onClose={onClose}
    >
      {error !== null && <p role="alert">{error}</p>}
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
