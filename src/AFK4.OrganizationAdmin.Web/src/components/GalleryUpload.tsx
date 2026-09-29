import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { MediaPurposeNames } from '@afk4/contracts';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';

const ALLOWED_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);
const MAX_SIZE_BYTES = 10 * 1024 * 1024;

// Тот же потолок, что и на сервере (BranchPhotos.MaxPhotos): больше десятка фото зала никто
// не пролистает, а каждое — трафик игрока на экране выбора клуба.
const MAX_PHOTOS = 10;

export interface GalleryPhoto {
  url: string;
  mediaId: string | null;
}

export interface GalleryUploadProps {
  value: GalleryPhoto[];
  onChange: (photos: GalleryPhoto[]) => void;
  branchId: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
}

// Галерея зала: обложка отвечает игроку «как тут выглядит», эти фото — «а что ещё». Порядок
// задаёт владелец стрелками — в приложении фото листаются ровно так, как выстроены здесь.
export function GalleryUpload({ value, onChange, branchId, backend, disabled }: GalleryUploadProps) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const client = useMemo(
    () => createAuthenticatedOperatorClients(backend.config, backend.session).media,
    [backend.config, backend.session]
  );

  const full = value.length >= MAX_PHOTOS;

  const handleFilesSelected = async (event: ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files ?? []);
    event.target.value = ''; // чтобы тот же файл можно было выбрать снова
    if (files.length === 0) return;

    setError(null);
    setBusy(true);
    try {
      const added: GalleryPhoto[] = [];
      for (const file of files) {
        if (value.length + added.length >= MAX_PHOTOS) break;
        if (!ALLOWED_TYPES.has(file.type)) { setError(t('op.media.upload.errorType')); continue; }
        if (file.size > MAX_SIZE_BYTES) { setError(t('op.media.upload.errorSize')); continue; }
        const uploaded = await client.upload(branchId, MediaPurposeNames.BranchGallery, file);
        added.push({ url: uploaded.url, mediaId: uploaded.mediaId });
      }
      if (added.length > 0) onChange([...value, ...added]);
    } catch (err) {
      setError(projectOperatorError(err, t).detail);
    } finally {
      setBusy(false);
    }
  };

  // Файл в хранилище здесь не стираем: форму ещё закроют «Отменой», и сохранённый профиль
  // остался бы со ссылкой на удалённое фото — у игрока в приложении битая картинка. Когда на
  // файл перестанет ссылаться сохранённое, его уберёт сервер (OrphanMediaSweeper).
  const handleRemove = (index: number) => {
    setError(null);
    onChange(value.filter((_, position) => position !== index));
  };

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= value.length) return;
    const next = [...value];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  };

  return (
    <div className="gallery-upload">
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        multiple
        data-testid="gallery-upload-input"
        className="media-upload-input"
        disabled={disabled || busy || full}
        onChange={(event) => { void handleFilesSelected(event); }}
      />

      {value.length > 0 && (
        <ul className="gallery-upload-list">
          {value.map((photo, index) => (
            <li key={photo.url} className="gallery-upload-item">
              <img src={photo.url} alt="" className="gallery-upload-thumb" />
              <div className="gallery-upload-item-actions">
                <button
                  type="button" className="ui-btn ui-btn--sm"
                  aria-label={t('op.club.gallery.moveEarlier')}
                  disabled={disabled || busy || index === 0}
                  onClick={() => move(index, -1)}
                >‹</button>
                <button
                  type="button" className="ui-btn ui-btn--sm"
                  aria-label={t('op.club.gallery.moveLater')}
                  disabled={disabled || busy || index === value.length - 1}
                  onClick={() => move(index, 1)}
                >›</button>
                <button
                  type="button" className="ui-btn ui-btn--sm"
                  aria-label={t('op.media.upload.remove')}
                  disabled={disabled || busy}
                  onClick={() => handleRemove(index)}
                >✕</button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div className="media-upload-actions">
        <button
          type="button"
          className="ui-btn ui-btn--sm"
          disabled={disabled || busy || full}
          onClick={() => inputRef.current?.click()}
        >
          {busy ? t('op.media.upload.uploading') : t('op.club.gallery.add')}
        </button>
        <span className="club-field-hint">
          {full ? t('op.club.gallery.full') : t('op.club.hint.gallery')}
        </span>
      </div>

      {error !== null && <p className="ui-field-error">{error}</p>}
    </div>
  );
}
