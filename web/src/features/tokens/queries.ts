import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * The member's personal tokens (M2, T19a, note 2026-09-15-token-personali-e-agente-del-validatore §3.1), as query options.
 * A component never fetches: it asks for these (design M0 §7.4).
 */

export type PersonalTokenDto = components['schemas']['PersonalTokenDto'];
export type PersonalTokenWriteDto = components['schemas']['PersonalTokenWriteDto'];
export type PersonalTokenIssuedDto = components['schemas']['PersonalTokenIssuedDto'];
export type PersonalTokenPage = components['schemas']['PagedResultOfPersonalTokenDto'];

export const tokensKey = ['tokens'] as const;

export function tokensListKey(search: ListSearch) {
  return [...tokensKey, 'list', search] as const;
}

/** The tokens that still open something: revoked and expired ones are off the default view, like the server's. */
export function tokensListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: tokensListKey(search),
    queryFn: async (): Promise<PersonalTokenPage> =>
      unwrap(
        await api.GET('/api/me/tokens', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}
