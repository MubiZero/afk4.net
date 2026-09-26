import type { MessageKey } from '@afk4/i18n';
import { PlatformMediaErrorCodeNames } from '@afk4/contracts';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

const MEDIA_ERROR_KEY: Record<string, MessageKey> = {
  [PlatformMediaErrorCodeNames.SteamCoverNotFound]: 'platform.games.cover.error.steamNotFound',
  [PlatformMediaErrorCodeNames.StorageNotConfigured]: 'platform.media.error.storageOff',
  [PlatformMediaErrorCodeNames.TooLarge]: 'platform.media.error.tooLarge',
  [PlatformMediaErrorCodeNames.NotAnImage]: 'platform.media.error.notImage'
};

/** Почему картинку не подтянули или не загрузили — фразой, с которой ясно, что делать дальше. */
export function describeMediaError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = MEDIA_ERROR_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}

/** Итог загрузки картинки: адрес или уже переведённая причина отказа — форме нечего разбирать самой. */
export type ImageResult = { url: string; error?: undefined } | { url?: undefined; error: string };

export async function loadImage(load: () => Promise<{ url: string }>, t: Translate): Promise<ImageResult> {
  try {
    return { url: (await load()).url };
  } catch (cause) {
    return { error: describeMediaError(cause, t) };
  }
}
