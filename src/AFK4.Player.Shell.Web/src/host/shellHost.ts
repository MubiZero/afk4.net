import { useEffect, useState } from 'react';
import { isHostBridgeAvailable, onHostMessage, postHostRequest } from '@afk4/host-bridge';
import {
  ShellBridgeEventTypeNames,
  ShellBridgeRequestTypeNames,
  type PlayerShellStateDto,
  type ShellAuthStateDto,
  type ShellBridgeRequestTypeName,
  type ShellGameForegroundDto,
  type ShellSnapshotDto,
  type ShellSystemStateDto
} from '@afk4/contracts';

/**
 * Мост к хосту оболочки (спека, §4.4). Запросы — имена и тела из контрактов, конверт — общий
 * `@afk4/host-bridge`, тот же, что у Панели и мастера.
 */
export const HOST_TIMEOUT_MS = 15_000;

export function requestHost<TPayload>(type: ShellBridgeRequestTypeName, payload?: unknown): Promise<TPayload> {
  return postHostRequest<TPayload>(type, payload ?? {}, HOST_TIMEOUT_MS);
}

const signedOut: ShellAuthStateDto = { signedIn: false, displayName: null, playerAccountId: null };

export interface HostView {
  /** Состояние ПК от агента; null — ещё не пришло. */
  state: PlayerShellStateDto | null;
  /** Когда по часам ПК пришло это состояние — для поправки часов. */
  stateReceivedAtMs: number | null;
  auth: ShellAuthStateDto;
  system: ShellSystemStateDto | null;
  /** Игра на переднем плане: странице незачем рисовать то, чего не видно. */
  gameForeground: boolean;
  /** Сколько раз тронули мышь или клавиатуру; растёт — значит, человек у ПК. */
  activity: number;
  /** Сколько раз хост сообщил о тишине; растёт — значит, от ПК отошли. */
  idle: number;
  /** Моста нет вовсе — страница открыта в браузере без хоста и без учебного хоста. */
  bridgeMissing: boolean;
}

/**
 * Всё, что хост знает и присылает сам. Подписка — до запроса снимка: иначе событие, пришедшее
 * между ответом и подпиской, потерялось бы.
 */
export function useShellHost(): HostView {
  const [view, setView] = useState<HostView>(() => ({
    state: null,
    stateReceivedAtMs: null,
    auth: signedOut,
    system: null,
    gameForeground: false,
    activity: 0,
    idle: 0,
    bridgeMissing: !isHostBridgeAvailable()
  }));

  useEffect(() => {
    const receiveState = (state: PlayerShellStateDto | null) =>
      setView((current) => ({ ...current, state, stateReceivedAtMs: state ? Date.now() : null }));

    const unsubscribers = [
      onHostMessage<PlayerShellStateDto>(ShellBridgeEventTypeNames.StateChanged, receiveState),
      onHostMessage<ShellAuthStateDto>(ShellBridgeEventTypeNames.AuthChanged, (auth) =>
        setView((current) => ({ ...current, auth: auth ?? signedOut }))),
      onHostMessage<ShellSystemStateDto>(ShellBridgeEventTypeNames.SystemChanged, (system) =>
        setView((current) => ({ ...current, system }))),
      onHostMessage<ShellGameForegroundDto>(ShellBridgeEventTypeNames.GameForeground, (game) =>
        setView((current) => ({ ...current, gameForeground: Boolean(game?.active) }))),
      onHostMessage(ShellBridgeEventTypeNames.InputActivity, () =>
        setView((current) => ({ ...current, activity: current.activity + 1 }))),
      onHostMessage(ShellBridgeEventTypeNames.InputIdle, () =>
        setView((current) => ({ ...current, idle: current.idle + 1 })))
    ];

    let cancelled = false;
    if (isHostBridgeAvailable()) {
      requestHost<ShellSnapshotDto>(ShellBridgeRequestTypeNames.ShellReady)
        .then((snapshot) => {
          if (cancelled || !snapshot) return;
          setView((current) => ({
            ...current,
            // Событие могло прийти раньше ответа — оно свежее снимка.
            state: current.state ?? snapshot.state ?? null,
            stateReceivedAtMs: current.state ? current.stateReceivedAtMs : snapshot.state ? Date.now() : null,
            auth: snapshot.auth ?? current.auth,
            system: current.system ?? snapshot.system ?? null
          }));
        })
        .catch(() => {
          // Хост не ответил на снимок — состояние придёт следующим пульсом агента.
        });
    }

    return () => {
      cancelled = true;
      for (const unsubscribe of unsubscribers) unsubscribe();
    };
  }, []);

  return view;
}
