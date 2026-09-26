import { useState } from 'react';
import { fieldErrorId } from '@/components/ui/field';
import { useI18n } from '@/i18n/I18nProvider';
import { hasErrors, type FieldError } from './adsModel';

/**
 * Ошибки полей формы — то же поведение, что у формы игры каталога. Ошибку поля показываем, когда
 * человек из него ушёл или попробовал сохранить: красное «укажите название» на только что
 * открытой пустой форме — упрёк за то, чего он ещё не делал.
 *
 * Порядок `fieldIds` — порядок полей в форме: в нём фокус уходит к первой ошибке.
 */
export function useFieldErrors<Field extends string>(
  errors: Partial<Record<Field, FieldError>>,
  fieldIds: Record<Field, string>
) {
  const { t } = useI18n();
  const [touched, setTouched] = useState<ReadonlySet<Field>>(new Set());
  const [attempted, setAttempted] = useState(false);

  const errorOf = (field: Field): string | undefined => {
    const found = errors[field];
    return found !== undefined && (attempted || touched.has(field)) ? t(found.key, found.values) : undefined;
  };

  const controlProps = (field: Field) => {
    const message = errorOf(field);
    return {
      id: fieldIds[field],
      'aria-invalid': message !== undefined ? true : undefined,
      'aria-describedby': message !== undefined ? fieldErrorId(fieldIds[field]) : undefined,
      onBlur: () => setTouched(previous => new Set(previous).add(field))
    };
  };

  /** Можно ли отправлять. Если нет — показывает все ошибки и ставит фокус на первое такое поле. */
  const readyToSubmit = (): boolean => {
    if (!hasErrors(errors)) return true;
    setAttempted(true);
    const first = (Object.keys(fieldIds) as Field[]).find(field => errors[field] !== undefined);
    if (first !== undefined) document.getElementById(fieldIds[first])?.focus();
    return false;
  };

  return { errorOf, controlProps, readyToSubmit };
}
