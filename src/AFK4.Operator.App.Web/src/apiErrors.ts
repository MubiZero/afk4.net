export interface OperatorErrorProjection {
  title: string;
  detail: string;
}

export function projectOperatorError(error: unknown): OperatorErrorProjection {
  if (error instanceof Error && error.message.trim().length > 0) {
    return {
      title: 'Действие не выполнено',
      detail: error.message
    };
  }

  if (typeof error === 'string' && error.trim().length > 0) {
    return {
      title: 'Действие не выполнено',
      detail: error
    };
  }

  return {
    title: 'Действие не выполнено',
    detail: 'Платформа не вернула подробности. Повторите действие или проверьте связь.'
  };
}
