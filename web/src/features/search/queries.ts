import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

/**
 * Asking the site what it knows about a word. It is a different mechanism from the `?q=` of a back
 * office list — that one is a `LIKE` over the columns of one table, this reads the FULLTEXT index
 * the interceptor keeps for every publishable row of every module (design M0 §3.6).
 */

export type SearchResponse = components['schemas']['SearchResponseDto'];
export type SearchHit = components['schemas']['SearchHitDto'];

export const searchKey = ['search'] as const;

/** How many hits a page holds. The server caps it; this is what the screens ask for. */
export const SEARCH_PAGE_SIZE = 20;

export function searchQuery(query: string, page: number, locale: string) {
  const trimmed = query.trim();

  return queryOptions({
    queryKey: [...searchKey, trimmed, page, locale] as const,
    queryFn: async (): Promise<SearchResponse> =>
      unwrap(
        await api.GET('/api/search', {
          params: { query: { q: trimmed, page, pageSize: SEARCH_PAGE_SIZE, locale } },
        }),
      ),
    // An empty box is not a search. The server answers it with nothing, and not asking at all is
    // the same answer without the round trip.
    enabled: trimmed.length > 0,
    // What somebody searched a moment ago has not changed: going back to the results of a hit
    // should not ask again.
    staleTime: 30_000,
  });
}
