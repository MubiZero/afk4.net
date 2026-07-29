import type { PlatformTransport } from '../platformTransport';

export interface PlatformSearchResult {
  kind: 'organization' | 'club' | 'owner';
  id: string;
  title: string;
  context: string;
  href: string;
}

export class SearchApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public search(query: string, signal?: AbortSignal): Promise<PlatformSearchResult[]> {
    return this.transport.send('GET', `/api/platform/search?query=${encodeURIComponent(query)}`, undefined, signal);
  }
}
