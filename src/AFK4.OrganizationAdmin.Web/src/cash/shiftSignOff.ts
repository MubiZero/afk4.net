import type { StaffUserDto } from '../operatorApiClients';

/**
 * Кого предложить в подписанты расхождения кассы.
 *
 * Правило «не тот, кто открыл смену, и не тот, кто её закрывает» клиенту известно точно — оно про
 * людей, а не про права, и повторить его здесь безопасно. Смысл требования в том, что «закрыть
 * поверх недостачи» в одиночку нельзя (анти-фрод §5.7), значит и выбирать надо из других.
 *
 * А вот право подписывать остаётся за сервером: соответствие ролей правам живёт в его каталоге, и
 * второй такой каталог в клиенте разошёлся бы с первым — ровно та беда, ради которой контракты
 * теперь генерируются. Поэтому список шире, чем разрешит сервер, и на неподходящем человеке он
 * ответит отказом с объяснением. Показать лишнего лучше, чем не дать закрыть смену вовсе.
 */
export function signOffCandidates(
  staff: readonly StaffUserDto[],
  openedByStaffUserId: string | null,
  closingStaffUserId: string | null
): { staffUserId: string; displayName: string }[] {
  return staff
    .filter((user) => user.isActive)
    .filter((user) => user.staffUserId !== openedByStaffUserId && user.staffUserId !== closingStaffUserId)
    .map((user) => ({ staffUserId: user.staffUserId, displayName: user.displayName }))
    .sort((left, right) => left.displayName.localeCompare(right.displayName));
}
