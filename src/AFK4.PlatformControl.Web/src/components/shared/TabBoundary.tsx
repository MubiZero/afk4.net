import { Component, type ReactNode } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

interface Props {
  message: string;
  retryLabel: string;
  /**
   * Identity that means "this is meaningfully different content, worth a
   * fresh render attempt" — e.g. `${organizationId}:${activeTab}`. Deliberately
   * NOT `children`: JSX creates a new element on almost every parent re-render,
   * so keying off `children` identity would clear the failed state on the very
   * next render (before the user ever sees the fallback) or, if the same crash
   * keeps happening, spin in a fail/reset loop. Omit this prop to disable
   * automatic resets and rely on the manual retry button only.
   */
  resetKey?: unknown;
  children: ReactNode;
}

interface State {
  failed: boolean;
}

/**
 * Isolates a render crash inside one workspace tab so it does not take down
 * the sibling tabs or the always-visible client passport next to it.
 */
export class TabBoundary extends Component<Props, State> {
  public state: State = { failed: false };

  public static getDerivedStateFromError(): State {
    return { failed: true };
  }

  public componentDidUpdate(prevProps: Props): void {
    if (this.state.failed && prevProps.resetKey !== this.props.resetKey) {
      this.setState({ failed: false });
    }
  }

  public render(): ReactNode {
    if (this.state.failed) {
      return (
        <Card role="alert">
          <CardContent className="flex flex-col items-start gap-3 py-10">
            <p className="text-muted-foreground">{this.props.message}</p>
            <Button onClick={() => this.setState({ failed: false })}>{this.props.retryLabel}</Button>
          </CardContent>
        </Card>
      );
    }
    return this.props.children;
  }
}
