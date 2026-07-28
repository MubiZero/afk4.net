import { Maximize2, Minus, X } from 'lucide-react';
import { type MouseEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { postHostWindowCommand, postHostWindowResize, type HostWindowResizeEdge } from './hostBridge';

export function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (event.detail > 1 || target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('drag');
}

export function handleWindowTitleDoubleClick(event: MouseEvent<HTMLElement>) {
  const target = event.target as HTMLElement;
  if (target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('maximize');
}

export function WindowControls() {
  const { t } = useI18n();
  return (
    <div className="window-controls" aria-label={t('op.shell.window')}>
      <button type="button" title={t('op.shell.minimize')} aria-label={t('op.shell.minimize')} onClick={() => postHostWindowCommand('minimize')}>
        <Minus size={15} />
      </button>
      <button type="button" title={t('op.shell.maximize')} aria-label={t('op.shell.maximize')} onClick={() => postHostWindowCommand('maximize')}>
        <Maximize2 size={13} />
      </button>
      <button type="button" title={t('op.shell.close')} aria-label={t('op.shell.close')} onClick={() => postHostWindowCommand('close')}>
        <X size={15} />
      </button>
    </div>
  );
}

export function WindowResizeHandles() {
  const edges: HostWindowResizeEdge[] = ['top', 'right', 'bottom', 'left', 'top-left', 'top-right', 'bottom-left', 'bottom-right'];

  return (
    <div className="window-resize-handles" aria-hidden="true">
      {edges.map((edge) => (
        <div
          key={edge}
          className={`window-resize-handle ${edge}`}
          onMouseDown={(event) => {
            if (event.button !== 0) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            postHostWindowResize(edge);
          }}
        />
      ))}
    </div>
  );
}
