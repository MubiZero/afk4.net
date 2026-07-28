import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

// bun's mock.module is not hoisted above static imports, so register it before
// importing the component under test.
const uploadMock = mock(async (_branchId: string, _purpose: string, file: File) => ({
  mediaId: 'media-1',
  url: 'https://cdn.test/media-1.png',
  contentType: file.type,
  sizeBytes: file.size
}));
const removeMock = mock(async (_branchId: string, _mediaId: string) => {});

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ media: { upload: uploadMock, remove: removeMock } })
}));

const { MediaUpload } = await import('./MediaUpload');

// Restore the real module once this file is done — bun keeps mock.module registrations for the
// rest of the run and mutates the shared namespace, which would break App.test.tsx and other
// suites that rely on the genuine operatorHelpers.
afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't' },
  branchId: 'b1'
} as unknown as import('../operatorTypes').OperatorBackendContext;

function fileInput(): HTMLInputElement {
  return screen.getByTestId('media-upload-input') as HTMLInputElement;
}

function renderUpload(overrides: Partial<import('./MediaUpload').MediaUploadProps> = {}) {
  const onChange = overrides.onChange ?? mock(() => {});
  render(
    <I18nProvider>
      <MediaUpload
        value={null}
        onChange={onChange}
        purpose="branch-logo"
        branchId="b1"
        backend={backend}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onChange };
}

describe('MediaUpload', () => {
  afterEach(() => {
    cleanup();
    uploadMock.mockClear();
    removeMock.mockClear();
  });

  it('shows the upload CTA when there is no value', () => {
    renderUpload();
    expect(screen.getByText('Загрузить изображение')).toBeInTheDocument();
  });

  it('rejects an over-sized file with errorSize and never calls upload', async () => {
    renderUpload();
    const bigFile = new File([new Uint8Array(11 * 1024 * 1024)], 'big.png', { type: 'image/png' });

    fireEvent.change(fileInput(), { target: { files: [bigFile] } });

    await waitFor(() => {
      expect(screen.getByText('Файл больше 10 МБ.')).toBeInTheDocument();
    });
    expect(uploadMock).not.toHaveBeenCalled();
  });

  it('uploads a valid png within the limit and reports {mediaId,url} via onChange', async () => {
    const { onChange } = renderUpload();
    const file = new File(['x'], 'logo.png', { type: 'image/png' });

    fireEvent.change(fileInput(), { target: { files: [file] } });

    await waitFor(() => expect(uploadMock).toHaveBeenCalledTimes(1));
    expect(uploadMock).toHaveBeenCalledWith('b1', 'branch-logo', file);
    await waitFor(() => {
      expect(onChange).toHaveBeenCalledWith({ mediaId: 'media-1', url: 'https://cdn.test/media-1.png' });
    });
  });

  it('shows preview + Replace/Remove when a value is set, and removes on click', async () => {
    const { onChange } = renderUpload({ value: 'https://cdn.test/existing.png' });

    const img = screen.getByTestId('media-upload-preview-img') as HTMLImageElement;
    expect(img.src).toBe('https://cdn.test/existing.png');
    expect(screen.getByText('Заменить')).toBeInTheDocument();

    fireEvent.click(screen.getByText('Удалить'));

    await waitFor(() => expect(removeMock).toHaveBeenCalledTimes(1));
    expect(removeMock).toHaveBeenCalledWith('b1', 'existing');
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('removes using the explicit mediaId prop, not the id parsed from the URL', async () => {
    const { onChange } = renderUpload({
      value: 'https://cdn.test/existing.png',
      mediaId: 'db-media-id-42'
    });

    fireEvent.click(screen.getByText('Удалить'));

    await waitFor(() => expect(removeMock).toHaveBeenCalledTimes(1));
    expect(removeMock).toHaveBeenCalledWith('b1', 'db-media-id-42');
    expect(onChange).toHaveBeenCalledWith(null);
  });
});
