import type { MessageKey } from '@afk4/i18n';
import { HostBridgeRequestError, isHostBridgeUnavailableError } from './hostBridge';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

/**
 * Отказы мастера человеческим языком.
 *
 * Текст отказа с сервера всегда английский («Tariff name already exists.»), а мастер работает и
 * по-русски, и по-таджикски: показать этот текст нельзя. Зато рядом с ним едет код — по нему и
 * называем причину. Незнакомый код попадает в общие слова экрана: выдумывать объяснение коду,
 * которого мы не знаем, хуже, чем сказать «не получилось».
 *
 * Раньше этот разбор был только на экране устройства, а шаги настройки клуба ловили любую
 * причину как одну строку «не удалось» — человек не мог понять, чинить ли ему сеть, имя тарифа
 * или номер телефона.
 */
const CODE_KEYS = {
  // Коды моста: что именно не доехало до сервера.
  host_timeout: 'setup.wizard.error.timedOut',
  wizard_create_seat_failed: 'setup.wizard.device.error.createSeat',
  wizard_enroll_failed: 'setup.wizard.device.error.enroll',
  // Регистрация прошла, а настройка на эту машину не легла — почти всегда права. Под общим
  // «не удалось зарегистрировать» причину искали в сети и в платформе, где её нет.
  wizard_local_config_write_failed: 'setup.wizard.device.error.localConfig',

  // Коды сервера: почему он отказал. Эти причины человек за мастером правит сам.
  too_many_password_attempts: 'setup.wizard.error.tooManyPasswordAttempts',
  tariff_name_taken: 'setup.wizard.error.tariffNameTaken',
  seat_occupied: 'setup.wizard.error.seatOccupied',
  invalid_phone: 'setup.wizard.error.invalidPhone',
  staff_phone_taken: 'setup.wizard.error.staffPhoneTaken',
  staff_username_taken: 'setup.wizard.error.staffUserNameTaken',
  plan_limit_reached: 'setup.wizard.error.planLimitReached'
} as const satisfies Record<string, MessageKey>;

/**
 * Что сказать человеку о неудаче.
 *
 * @param fallbackKey Слова экрана на случай, когда причина неизвестна («не удалось создать
 * тариф»): они хотя бы называют шаг, на котором всё встало.
 */
export function wizardErrorMessage(error: unknown, t: TFn, fallbackKey: MessageKey): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.error.bridgeMissing');
  }

  if (error instanceof HostBridgeRequestError) {
    const key = CODE_KEYS[error.code as keyof typeof CODE_KEYS];
    if (key) {
      return t(key);
    }
  }

  return t(fallbackKey);
}
