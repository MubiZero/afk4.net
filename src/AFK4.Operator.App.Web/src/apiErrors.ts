import type { MessageKey } from '@afk4/i18n';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

export interface OperatorErrorProjection {
  title: string;
  detail: string;
}

export function projectOperatorError(error: unknown, t: TFn): OperatorErrorProjection {
  const title = t('op.error.actionFailed.title');

  if (error instanceof Error && error.message.trim().length > 0) {
    return { title, detail: error.message };
  }

  if (typeof error === 'string' && error.trim().length > 0) {
    return { title, detail: error };
  }

  return { title, detail: t('op.error.actionFailed.noDetail') };
}
