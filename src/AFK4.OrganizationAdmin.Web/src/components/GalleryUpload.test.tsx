import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

// bun's mock.module is not hoisted above static imports, so register it before importing the
// component under test.
let uploadCount = 0;
const uploadMock = mock(async (_branchId: string, _purpose: string, file: File) => {
  uploadCount += 1;
  return {
    mediaId: `media-${uploadCount}`,
    url: `https://cdn.test/media-${uploadCount}.png`,
    contentType: file.type,
    sizeBytes: file.size
  };
});
const removeMock = mock(async (_branchId: string, _mediaId: string) => {});

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ media: { upload: uploadMock, remove: removeMock } })
}));

const { GalleryUpload } = await import('./GalleryUpload');

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

type Photo = { url: string; mediaId: string | null };

function renderGallery(value: Photo[] = []) {
  const onChange = mock((_photos: Photo[]) => {});
  render(
    <I18nProvider>
      <GalleryUpload value={value} onChange={onChange} branchId="b1" backend={backend} />
    </I18nProvider>
  );
  return { onChange };
}

const input = () => screen.getByTestId('gallery-upload-input') as HTMLInputElement;

describe('GalleryUpload', () => {
  afterEach(() => {
    cleanup();
    uploadCount = 0;
    uploadMock.mockClear();
    removeMock.mockClear();
  });

  // Галерея — не логотип: второе фото добавляется к первому, а не заменяет его.
  it('добавляет загруженные фото к уже имеющимся', async () => {
    const { onChange } = renderGallery([{ url: 'https://cdn.test/old.png', mediaId: 'old' }]);
    const file = new File(['x'], 'hall.png', { type: 'image/png' });

    fireEvent.change(input(), { target: { files: [file] } });

    await waitFor(() => expect(uploadMock).toHaveBeenCalledTimes(1));
    expect(uploadMock).toHaveBeenCalledWith('b1', 'branch-gallery', file);
    expect(onChange).toHaveBeenCalledWith([
      { url: 'https://cdn.test/old.png', mediaId: 'old' },
      { url: 'https://cdn.test/media-1.png', mediaId: 'media-1' }
    ]);
  });

  it('за один выбор грузит несколько файлов', async () => {
    const { onChange } = renderGallery();
    const files = [
      new File(['x'], 'a.png', { type: 'image/png' }),
      new File(['y'], 'b.png', { type: 'image/png' })
    ];

    fireEvent.change(input(), { target: { files } });

    await waitFor(() => expect(uploadMock).toHaveBeenCalledTimes(2));
    expect(onChange.mock.calls[0][0]).toHaveLength(2);
  });

  it('слишком большой файл отклоняется и не уходит на сервер', async () => {
    renderGallery();
    const big = new File([new Uint8Array(11 * 1024 * 1024)], 'big.png', { type: 'image/png' });

    fireEvent.change(input(), { target: { files: [big] } });

    await waitFor(() => expect(screen.getByText('Файл больше 10 МБ.')).toBeInTheDocument());
    expect(uploadMock).not.toHaveBeenCalled();
  });

  // Порядок фото — это порядок, в котором их листает игрок, поэтому им можно управлять.
  it('стрелка меняет порядок фото', () => {
    const { onChange } = renderGallery([
      { url: 'https://cdn.test/1.png', mediaId: '1' },
      { url: 'https://cdn.test/2.png', mediaId: '2' }
    ]);

    fireEvent.click(screen.getAllByLabelText('Переместить раньше')[1]);

    expect(onChange).toHaveBeenCalledWith([
      { url: 'https://cdn.test/2.png', mediaId: '2' },
      { url: 'https://cdn.test/1.png', mediaId: '1' }
    ]);
  });

  it('удаление убирает фото и стирает файл в хранилище', async () => {
    const { onChange } = renderGallery([{ url: 'https://cdn.test/1.png', mediaId: 'm1' }]);

    fireEvent.click(screen.getByLabelText('Удалить'));

    await waitFor(() => expect(removeMock).toHaveBeenCalledWith('b1', 'm1'));
    expect(onChange).toHaveBeenCalledWith([]);
  });

  // Десять фото — потолок и на сервере: кнопка не должна звать туда, где ответят отказом.
  it('на десятом фото кнопка добавления выключается', () => {
    renderGallery(
      Array.from({ length: 10 }, (_, index) => ({
        url: `https://cdn.test/${index}.png`,
        mediaId: `m${index}`
      }))
    );

    expect(screen.getByText('Больше 10 фото добавить нельзя')).toBeInTheDocument();
    expect(screen.getByText('Добавить фото').closest('button')).toBeDisabled();
  });
});
