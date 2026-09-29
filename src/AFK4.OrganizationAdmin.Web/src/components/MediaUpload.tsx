import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';

const ALLOWED_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);
const MAX_SIZE_BYTES = 10 * 1024 * 1024;

type FileProblem = 'type' | 'size' | null;

function validateFile(file: File): FileProblem {
  if (!ALLOWED_TYPES.has(file.type)) return 'type';
  if (file.size > MAX_SIZE_BYTES) return 'size';
  return null;
}

export interface MediaUploadProps {
  value: string | null;
  onChange: (media: { mediaId: string; url: string } | null) => void;
  purpose: string;
  branchId: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
}

export function MediaUpload({ value, onChange, purpose, branchId, backend, disabled }: MediaUploadProps) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const client = useMemo(
    () => createAuthenticatedOperatorClients(backend.config, backend.session).media,
    [backend.config, backend.session]
  );

  const openPicker = () => inputRef.current?.click();

  const handleFileSelected = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    event.target.value = ''; // allow re-selecting the same file next time
    if (file === null) return;

    const problem = validateFile(file);
    if (problem === 'type') {
      setError(t('op.media.upload.errorType'));
      return;
    }
    if (problem === 'size') {
      setError(t('op.media.upload.errorSize'));
      return;
    }

    setError(null);
    setUploading(true);
    try {
      // Прежний файл убирает сервер, remove() здесь не нужен: логотип и обложку — сразу при новой
      // загрузке (EfMediaService), фото новости и товара — когда на него перестанет ссылаться
      // сохранённое (OrphanMediaSweeper).
      const uploaded = await client.upload(branchId, purpose, file);
      onChange({ mediaId: uploaded.mediaId, url: uploaded.url });
    } catch (err) {
      setError(projectOperatorError(err, t).detail);
    } finally {
      setUploading(false);
    }
  };

  // «Удалить» только убирает картинку из формы. Стереть файл сразу значило бы оставить битую
  // картинку, если человек передумает и не сохранит: запись всё ещё на него ссылается. Лишний
  // логотип или обложку сервер уберёт сам при следующей загрузке (EfMediaService.UploadAsync).
  const handleRemove = () => {
    setError(null);
    onChange(null);
  };

  return (
    <div className="media-upload">
      <input
        ref={inputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        data-testid="media-upload-input"
        className="media-upload-input"
        disabled={disabled || uploading}
        onChange={(event) => { void handleFileSelected(event); }}
      />

      {value !== null && (
        <div className="media-upload-preview">
          <img
            src={value}
            alt=""
            data-testid="media-upload-preview-img"
            className="media-upload-preview-img"
          />
        </div>
      )}

      {/* Все кнопки второстепенные: главная у формы с картинкой — «Сохранить», а заливка на
          «Загрузить изображение» давала экрану клуба две главные. «Удалить» убирает картинку из
          формы до сохранения — это отменяемо, поэтому не красным. */}
      <div className="media-upload-actions">
        {uploading ? (
          <button type="button" className="ui-btn ui-btn--sm" disabled>
            {t('op.media.upload.uploading')}
          </button>
        ) : value !== null ? (
          <>
            <button type="button" className="ui-btn ui-btn--sm" onClick={openPicker} disabled={disabled}>
              {t('op.media.upload.replace')}
            </button>
            <button
              type="button"
              className="ui-btn ui-btn--sm"
              onClick={handleRemove}
              disabled={disabled}
            >
              {t('op.media.upload.remove')}
            </button>
          </>
        ) : (
          <button type="button" className="ui-btn ui-btn--sm" onClick={openPicker} disabled={disabled}>
            {t('op.media.upload.cta')}
          </button>
        )}
      </div>

      <p className="media-upload-hint">{t('op.media.upload.hint')}</p>
      {error !== null && <p className="ui-field-error" role="alert">{error}</p>}
    </div>
  );
}
