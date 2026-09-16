import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  /// Что сказать человеку у ПК. Строка приходит готовой: граница — класс, а хук локали здесь
  /// не вызвать.
  message: string;
  retryLabel: string;
}

interface State {
  hasError: boolean;
}

/**
 * Последняя защита мастера от белого окна.
 *
 * Мастер живёт в WebView2 без адресной строки и без кнопки «обновить», а во время установки ещё
 * и придерживает закрытие окна. Любое исключение в отрисовке до этого оставляло человека у ПК
 * наедине с пустым белым прямоугольником, из которого нет выхода. Теперь он видит, что произошло,
 * и может попробовать ещё раз.
 */
export class WizardErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: unknown) {
    // Подробности — в консоль WebView2: их снимают с машины при разборе, на экране они человеку
    // у ПК ничего не объясняют.
    // eslint-disable-next-line no-console
    console.error('Setup wizard render failed', error);
  }

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <div className="wizard-crash" role="alert">
        <p>{this.props.message}</p>
        <button type="button" className="ui-btn" onClick={() => this.setState({ hasError: false })}>
          {this.props.retryLabel}
        </button>
      </div>
    );
  }
}
