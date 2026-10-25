import { Component, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';

const reloadAfterMs = 3_000;

interface Props {
  children: ReactNode;
  // Подменяется в тестах: настоящая перезагрузка страницы в них ничего не докажет.
  reload?: () => void;
}

interface State {
  hasError: boolean;
}

/**
 * Корневая граница экрана игрока. Без неё ошибка рендера оставляла на киоске белый экран: гость
 * не мог ни войти, ни позвать администратора, а перезапустить страницу в киоске нечем. Теперь
 * экран говорит, что перезапускается, и через три секунды перезагружает страницу сам.
 */
export class ShellErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  private reloadTimer: ReturnType<typeof setTimeout> | undefined;

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: unknown) {
    // eslint-disable-next-line no-console
    console.error('Player shell render failed', error);
    this.reloadTimer = setTimeout(this.props.reload ?? (() => window.location.reload()), reloadAfterMs);
  }

  componentWillUnmount() {
    clearTimeout(this.reloadTimer);
  }

  render() {
    return this.state.hasError ? <ShellCrashScreen /> : this.props.children;
  }
}

function ShellCrashScreen() {
  const { t } = useI18n();
  return (
    <div className="shell-crash" role="alert">
      {t('playerShell.crash.reloading')}
    </div>
  );
}
