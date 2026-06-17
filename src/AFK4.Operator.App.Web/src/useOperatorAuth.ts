import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import {
  loadOperatorSession,
  refreshOperatorSession,
  signInOperator,
  signOutOperator,
  type OperatorAuthSession,
  type OperatorSignInRequest
} from './authClient';
import type { AuthStatus, OperatorConfig } from './operatorTypes';
import {
  clearStoredOperatorSession,
  isUnauthorizedAuthError,
  projectAuthHostError
} from './operatorHelpers';

// Бутстрап и жизненный цикл сессии оператора: при монтировании пробуем восстановить сохранённую
// сессию (с тихим refresh), плюс вход/выход. Сеттеры отдаём наружу — их разделяет useFloorMap
// (сбрасывает сессию при 401). Очистку навигационного feedback не знаем — её вешает вызывающий
// через onSignedIn/onSignedOut, чтобы хук не зависел от состояния навигации.
export function useOperatorAuth(
  config: OperatorConfig,
  options: { onSignedIn?: () => void; onSignedOut?: () => void } = {}
) {
  const { t } = useI18n();
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [authView, setAuthView] = useState<'signIn' | 'forgot'>('signIn');

  useEffect(() => {
    let disposed = false;

    loadOperatorSession()
      .then(async (session) => {
        if (session === null) {
          return null;
        }

        try {
          return await refreshOperatorSession();
        } catch (error) {
          if (isUnauthorizedAuthError(error)) {
            await clearStoredOperatorSession();
            return null;
          }

          throw error;
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
        setAuthError(projectAuthHostError(error, config, t));
      });

    return () => {
      disposed = true;
    };
  }, []);

  const handleSignIn = async (request: OperatorSignInRequest) => {
    const session = await signInOperator(request);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
    options.onSignedIn?.();
  };

  const handleSignOut = async () => {
    try {
      await signOutOperator();
      setAuthError(null);
      options.onSignedOut?.();
    } catch (error) {
      setAuthError(projectAuthHostError(error, config, t));
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
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
    handleSignIn,
    handleSignOut
  };
}
