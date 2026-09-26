import { useI18n } from '@/i18n/I18nProvider';
import type { AdCreativeDto } from '@/api/types';
import { AdImagePreview } from './AdImages';

type CreativeText = Pick<AdCreativeDto, 'title' | 'body' | 'titleRu' | 'bodyRu'>;

/**
 * Текст креатива в том порядке, в каком его покажет карточка: сначала таджикский —
 * государственный язык (ст. 5 закона «О рекламе»), под ним русский, мельче. `lang` у каждой части —
 * чтобы экранный диктор читал таджикский и русский своим произношением.
 */
export function AdCreativeText({ creative }: { creative: CreativeText }) {
  const titleRu = creative.titleRu ?? null;
  const bodyRu = creative.bodyRu ?? null;
  return (
    <>
      <strong lang="tg">{creative.title}</strong>
      {creative.body !== null ? <span className="pc-ad-body" lang="tg">{creative.body}</span> : null}
      {titleRu !== null || bodyRu !== null ? (
        <span className="pc-ad-ru" lang="ru">
          {titleRu !== null ? <span className="pc-ad-ru-title">{titleRu}</span> : null}
          {bodyRu !== null ? <span>{bodyRu}</span> : null}
        </span>
      ) : null}
    </>
  );
}

/** Креатив глазами игрока — с подписью рекламодателя: по ней и видно, не другой ли это клуб. */
export function AdCardPreview({ creative, advertiserName }: { creative: AdCreativeDto; advertiserName: string }) {
  const { t } = useI18n();
  return (
    <figure className="pc-ad-card">
      <figcaption className="pc-ad-card-label">{t('platform.ads.preview.label', { advertiser: advertiserName })}</figcaption>
      <AdCreativeText creative={creative} />
      {creative.imageUrl !== null ? <AdImagePreview url={creative.imageUrl} /> : null}
    </figure>
  );
}
