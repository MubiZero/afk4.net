import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  message: string;
}

interface State {
  hasError: boolean;
}

// Локализует падение одного рабочего экрана: вместо чёрного экрана всего приложения показываем
// сообщение в области контента, а рельс/шапка/футер остаются рабочими — оператор уходит в другой
// раздел. Сбрасывается через key={workspace} в App: смена раздела перемонтирует границу и чистит ошибку.
export class WorkspaceErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: unknown) {
    // eslint-disable-next-line no-console
    console.error('Workspace render failed', error);
  }

  render() {
    if (this.state.hasError) {
      return <div className="workspace-crash" role="alert">{this.props.message}</div>;
    }
    return this.props.children;
  }
}
