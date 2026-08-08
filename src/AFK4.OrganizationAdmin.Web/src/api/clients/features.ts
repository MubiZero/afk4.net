import { PlatformApiClient } from '../../platformApi';

export function createFeaturesClient(api: PlatformApiClient) {
  return {
    list(): Promise<string[]> {
      return api.get<{ features: string[] }>('features').then((response) => {
        // A malformed body (missing/null/non-array `features` — version skew, a caching proxy, a
        // backend bug) must be treated the same as a failed request: throw so callers route it into
        // their "list unavailable, assume enabled" fallback instead of getting `undefined` downstream.
        if (!Array.isArray(response.features)) {
          throw new Error('Malformed /features response: features is not an array');
        }
        return response.features;
      });
    }
  };
}
