import { useState } from 'react';
import { useI18n } from '@/i18n/I18nProvider';
import { isHttpsUrl } from './adsModel';

// Миниатюра стоит и там, где картинки нет или она не открылась: пустая плашка держит заголовки
// креативов в одну колонку, а не скачет по строкам.
export function AdThumb({ url }: { url: string | null }) {
  const [broken, setBroken] = useState(false);
  if (url === null || broken) return <span className="pc-ad-thumb is-empty" aria-hidden="true" />;
  return <img className="pc-ad-thumb" src={url} alt="" loading="lazy" onError={() => setBroken(true)} />;
}

/**
 * Картинка креатива целиком, как её откроет ПК. Показывается только https-адрес — другой ПК не
 * скачает. Не открылась — говорим об этом словами: вместо картинки на экране будет один текст.
 */
export function AdImagePreview({ url }: { url: string }) {
  const { t } = useI18n();
  const [broken, setBroken] = useState<string | null>(null);
  const trimmed = url.trim();
  if (!isHttpsUrl(trimmed)) return null;
  if (broken === trimmed) return <p className="pc-error-text">{t('platform.ads.creative.imageBroken')}</p>;
  return <img className="pc-ad-preview" src={trimmed} alt={t('platform.ads.creative.imagePreview')} onError={() => setBroken(trimmed)} />;
}
