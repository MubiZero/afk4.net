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

// The public media URL Platform.Api hands back is `{base}/{organizationId}/{branchId}/{mediaId}.{ext}`
// (see Media/EfMediaService.UploadAsync) — the mediaId is the URL's filename stem. Callers that
// persist only the URL (no `mediaId` prop) still need a way to resolve the delete-endpoint id, so
// this recovers it as a fallback when the caller doesn't pass `mediaId` explicitly.
function mediaIdFromUrl(url: string): string | null {
  const path = url.split('?')[0].split('#')[0];
  const fileName = path.slice(path.lastIndexOf('/') + 1);
  if (fileName === '') return null;
  const dotIndex = fileName.lastIndexOf('.');
  return dotIndex > 0 ? fileName.slice(0, dotIndex) : fileName;
}

export interface MediaUploadProps {
  value: string | null;
  mediaId?: string | null;
  onChange: (media: { mediaId: string; url: string } | null) => void;
  purpose: string;
  branchId: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
}

export function MediaUpload({ value, mediaId: explicitMediaId, onChange, purpose, branchId, backend, disabled }: MediaUploadProps) {
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
      // Uploading a new file for the same (branch, purpose) supersedes the previous one
      // server-side (EfMediaService deletes the old object) — no explicit remove() needed here.
      const uploaded = await client.upload(branchId, purpose, file);
      onChange({ mediaId: uploaded.mediaId, url: uploaded.url });
    } catch (err) {
      setError(projectOperatorError(err, t).detail);
    } finally {
      setUploading(false);
    }
  };

  const handleRemove = async () => {
    if (value === null) return;
    const mediaId = explicitMediaId != null && explicitMediaId !== '' ? explicitMediaId : mediaIdFromUrl(value);
    if (mediaId === null) {
      onChange(null);
      return;
    }

    setError(null);
    setUploading(true);
    try {
      await client.remove(branchId, mediaId);
      onChange(null);
    } catch (err) {
      setError(projectOperatorError(err, t).detail);
    } finally {
      setUploading(false);
    }
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
              className="ui-btn ui-btn--sm ui-btn--danger"
              onClick={() => { void handleRemove(); }}
              disabled={disabled}
            >
              {t('op.media.upload.remove')}
            </button>
          </>
        ) : (
          <button type="button" className="ui-btn ui-btn--sm ui-btn--primary" onClick={openPicker} disabled={disabled}>
            {t('op.media.upload.cta')}
          </button>
        )}
      </div>

      <p className="media-upload-hint">{t('op.media.upload.hint')}</p>
      {error !== null && <p className="ui-field-error" role="alert">{error}</p>}
    </div>
  );
}
