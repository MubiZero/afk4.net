import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';
import type { PlayerReputationDto } from '../operatorApiClients';
import { reputationErrorKey, reputationLookupPhone } from './reputationModel';

export type ReputationState =
  | { status: 'noPhone' }
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; reputation: PlayerReputationDto }
  | { status: 'failed'; detail: string };

export interface ReputationController {
  state: ReputationState;
  ask: () => void;
}

// Ответы сети за сессию. Сеть считает числа раз в сутки, поэтому второй запрос про того же
// человека не узнал бы ничего нового — зато оставил бы вторую запись в аудите клуба.
// Ключ — то, чем спрашивали: номер или личность платформы.
const answers = new Map<string, PlayerReputationDto>();

/** Только для тестов: сбросить накопленные за сессию ответы. */
export function clearReputationAnswers(): void {
  answers.clear();
}

/**
 * Репутация одного человека в карточке.
 *
 * Сеть спрашивается по нажатию, а не при открытии карточки: каждый запрос пишется в аудит на
 * сам факт чтения, и «пролистал двадцать клиентов» превратилось бы в двадцать записей о том,
 * что клуб интересовался чужими гостями. Один осознанный клик — одна запись.
 */
export function useReputation(
  backend: OperatorBackendContext | null,
  rawPhoneNumber: string,
  platformPersonId: string | null = null
): ReputationController {
  const { t } = useI18n();
  const phone = reputationLookupPhone(rawPhoneNumber);
  // Чем спрашиваем. Номер вперёд личности: он же и единственный способ узнать про человека,
  // чью карточку стойка завела до общего котла и к платформе не привязала. Номера нет —
  // спрашиваем по личности, если карточка её называет.
  const asked: AskedBy | null = phone !== null
    ? { kind: 'phone', value: phone }
    : platformPersonId !== null ? { kind: 'person', value: platformPersonId } : null;
  const key = asked === null ? null : `${asked.kind}:${asked.value}`;

  const initial = (): ReputationState => {
    if (key === null) return { status: 'noPhone' };
    const known = answers.get(key);
    return known ? { status: 'ready', reputation: known } : { status: 'idle' };
  };
  const [state, setState] = useState<ReputationState>(initial);
  // Про кого сейчас карточка. Ответ сети приходит асинхронно, а она за это время могла
  // переехать на другого человека — показать ему чужие числа хуже, чем не показать ничего.
  const shownKey = useRef(key);
  shownKey.current = key;

  // Карточка переехала на другого человека — показываем его ответ (или предложение спросить),
  // а не оставшийся на экране чужой.
  useEffect(() => { setState(initial()); }, [key]);

  const ask = useCallback(() => {
    if (backend === null || asked === null || key === null) return;
    setState({ status: 'loading' });
    const players = createAuthenticatedOperatorClients(backend.config, backend.session).players;
    const answer = asked.kind === 'phone'
      ? players.lookupReputation(backend.branchId, asked.value)
      : players.reputationForPerson(backend.branchId, asked.value);
    answer
      .then((reputation) => {
        // Ответ кладём в память всегда — он верен для того, про кого спрашивали; на экран
        // только если карточка всё ещё про него.
        answers.set(key, reputation);
        if (shownKey.current === key) setState({ status: 'ready', reputation });
      })
      .catch((error) => {
        if (shownKey.current !== key) return;
        const failure = reputationErrorKey(error);
        setState({
          status: 'failed',
          detail: failure ? t(failure) : projectOperatorError(error, t).detail
        });
      });
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, key, t]);

  return { state, ask };
}

type AskedBy = { kind: 'phone' | 'person'; value: string };
