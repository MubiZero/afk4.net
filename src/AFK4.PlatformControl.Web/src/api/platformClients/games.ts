import type { PlatformTransport } from '../platformTransport';
import type { CatalogGameDto, UpsertCatalogGameRequest } from '../types';

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
}
