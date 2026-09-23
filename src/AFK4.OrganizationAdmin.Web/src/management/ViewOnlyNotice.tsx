import type { JSX } from 'react';
import { Eye } from 'lucide-react';

/**
 * «Только просмотр» — одной строкой над экраном, а не подписью под каждой погашенной кнопкой.
 *
 * Сотрудник без права менять открывал «Товары» или «Тарифы» и видел форму, где все поля и кнопки
 * серые, и ни слова почему. Здесь сказано один раз: смотреть можно, менять — нет, и кто может.
 */
export function ViewOnlyNotice({ reason }: { reason: string | null | undefined }): JSX.Element | null {
  if (!reason) return null;
  return (
    <p className="management-view-only" role="note">
      <Eye size={14} aria-hidden="true" />
      <span>{reason}</span>
    </p>
  );
}
