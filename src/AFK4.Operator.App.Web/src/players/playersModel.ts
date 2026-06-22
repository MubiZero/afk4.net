// Players-feature model surface (зеркало паттерна src/booking/). Общие мапперы
// (projectPlayerClient/playerPackageLabel/PlayerClientItem) остаются в operatorHelpers,
// т.к. их используют POS/Брони/Карта — здесь только ре-экспорт, чтобы у фичи был
// единый импорт. Players-эксклюзивные чистые функции живут здесь.
import { formatMinorUnits, type PlayerClientItem, type TFunc } from '../operatorHelpers';

export { projectPlayerClient, playerPackageLabel, type PlayerClientItem } from '../operatorHelpers';

export function fixturePlayers(currencyCode: string, t: TFunc): PlayerClientItem[] {
  const example = t('op.helper.player.fixture.example');
  return [
    { name: 'Madina S.', status: 'vip', balanceMinorUnits: 46000, debtMinorUnits: 0, last: example, tone: 'vip', detail: t('op.helper.player.fixture.localCard'), phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: formatMinorUnits(12000, currencyCode), phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'debt', balanceMinorUnits: 0, debtMinorUnits: 3500, last: example, tone: 'debt', detail: t('op.helper.player.fixture.debtDetail'), phoneNumber: '', source: 'fixture' }
  ];
}

// Maps the stable status key from projectPlayerClient/fixturePlayers to a localized label.
export function playerStatusLabel(status: string, t: TFunc): string {
  switch (status) {
    case 'vip':
      return t('op.players.status.vip');
    case 'active':
      return t('op.players.status.active');
    case 'debt':
      return t('op.players.status.debt');
    case 'package':
      return t('op.players.status.package');
    case 'inactive':
      return t('op.players.status.inactive');
    default:
      return status;
  }
}
