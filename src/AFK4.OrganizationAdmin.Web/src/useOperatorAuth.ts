import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import {
  loadOperatorSession,
  refreshOperatorSession,
  signInByLoginOperator,
  signInToClubOperator,
  signOutOperator,
  ChooseClubError,
  isUnauthorizedStaffAuthError,
  type OperatorAuthSession,
  type ClubChoice
} from './authClient';
import { isAccessTokenExpired } from './auth/staffSessionStore';
import type { AuthStatus, OperatorConfig } from './operatorTypes';
import { projectAuthSignInError, clearStoredOperatorSession } from './operatorHelpers';

export interface ChooseClubState {
  clubs: ClubChoice[];
  login: string;
  password: string;
}

// Бутстрап и жизненный цикл сессии оператора: при монтировании пробуем восстановить сохранённую
// сессию из sessionStorage (с тихим refresh, только если access-токен истёк), плюс вход/выход.
// Аутентификация идёт напрямую по HTTP (StaffAuthApi) — нативный мост тут больше не участвует.
// Сеттеры отдаём наружу — их разделяет useFloorMap (сбрасывает сессию при 401). Очистку
// навигационного feedback не знаем — её вешает вызывающий через onSignedIn/onSignedOut, чтобы
// хук не зависел от состояния навигации.
export function useOperatorAuth(
  _config: OperatorConfig,
  options: { onSignedIn?: () => void; onSignedOut?: () => void } = {}
) {
  const { t } = useI18n();
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [authView, setAuthView] = useState<'signIn' | 'forgot' | 'invite'>('signIn');
  const [chooseClub, setChooseClub] = useState<ChooseClubState | null>(null);

  useEffect(() => {
    let disposed = false;

    loadOperatorSession()
      .then(async (session) => {
        if (session === null) {
          return null;
        }

        if (!isAccessTokenExpired(session, Date.now())) {
          return session;
        }

        try {
          return await refreshOperatorSession();
        } catch (error) {
          if (!isUnauthorizedStaffAuthError(error)) {
            // Транзиентный сбой (сеть/5xx) — сохранённая сессия ещё может быть годной, не
            // стираем её; пробрасываем наверх, чтобы показать ошибку и уйти в signed-out
            // (пользователь может повторить попытку, вместо форс-релогина на ровном месте).
            throw error;
          }

          // Refresh-токен реально отозван/просрочен (401) — сохранённая сессия больше не
          // годится, тихо возвращаемся к экрану входа без тревожного алерта поверх формы.
          await clearStoredOperatorSession();
          return null;
        }
      })
      .then((session) => {
        if (disposed) {
          return;
        }

        setAuthSession(session);
        setAuthStatus(session ? 'signed-in' : 'signed-out');
        setAuthError(null);
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setAuthSession(null);
        setAuthStatus('signed-out');
        setAuthError(projectAuthSignInError(error, t));
      });

    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSignIn = async (login: string, password: string) => {
    try {
      const session = await signInByLoginOperator(login, password);
      setChooseClub(null);
      setAuthSession(session);
      setAuthStatus('signed-in');
      setAuthError(null);
      options.onSignedIn?.();
    } catch (error) {
      if (error instanceof ChooseClubError) {
        // Логин совпал в нескольких клубах — просим выбрать; логин/пароль держим в памяти
        // хука ровно до выбора клуба, дальше не нужны.
        setChooseClub({ clubs: error.clubs, login, password });
        return;
      }

      throw error;
    }
  };

  const handleChooseClub = async (organizationId: string) => {
    if (chooseClub === null) {
      return;
    }

    const session = await signInToClubOperator(organizationId, chooseClub.login, chooseClub.password);
    setChooseClub(null);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
    options.onSignedIn?.();
  };

  const cancelChooseClub = () => {
    setChooseClub(null);
  };

  const handleSignOut = async () => {
    try {
      await signOutOperator();
      setAuthError(null);
      options.onSignedOut?.();
    } catch (error) {
      setAuthError(projectAuthSignInError(error, t));
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
      setChooseClub(null);
    }
  };

  return {
    authStatus,
    authSession,
    authError,
    authView,
    setAuthView,
    setAuthSession,
    setAuthStatus,
    setAuthError,
    chooseClub,
    handleSignIn,
    handleChooseClub,
    cancelChooseClub,
    handleSignOut
  };
}
