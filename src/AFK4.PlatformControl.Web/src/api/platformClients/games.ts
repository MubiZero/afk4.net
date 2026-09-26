import type { PlatformTransport } from '../platformTransport';
import type { CatalogGameDto, PlatformMediaUploadedDto, UpsertCatalogGameRequest } from '../types';
import { uploadPlatformImage } from './media';

/**
 * Каталог игр платформы. Удаления нет: клубы ссылаются на игры каталога, поэтому игру снимают с
 * публикации — клубы её больше не находят, а у тех, кто уже добавил, она остаётся.
 */
export class GamesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listGames(): Promise<CatalogGameDto[]> {
    return this.transport.send<CatalogGameDto[]>('GET', '/api/platform/games');
  }

  public createGame(request: UpsertCatalogGameRequest): Promise<CatalogGameDto> {
    return this.transport.send<CatalogGameDto>('POST', '/api/platform/games', request);
  }

  public updateGame(catalogGameId: string, request: UpsertCatalogGameRequest): Promise<CatalogGameDto> {
    return this.transport.send<CatalogGameDto>(
      'PUT', `/api/platform/games/${encodeURIComponent(catalogGameId)}`, request);
  }

  /** Картинка магазина Steam по номеру приложения — копией в нашем хранилище. 404 — у Steam её нет. */
  public steamCover(steamAppId: string): Promise<PlatformMediaUploadedDto> {
    return this.transport.send<PlatformMediaUploadedDto>('POST', '/api/platform/games/steam-cover', { steamAppId });
  }

  public uploadCover(file: File): Promise<PlatformMediaUploadedDto> {
    return uploadPlatformImage(this.transport, 'catalog-cover', file);
  }
}
