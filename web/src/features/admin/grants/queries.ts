import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../../shared/list';

/**
 * Every call the permissions screen makes, as query options. A component never fetches: it asks
 * for these and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type GrantListDto = components['schemas']['GrantListDto'];
export type GrantDetailDto = components['schemas']['GrantDetailDto'];
export type GrantWriteDto = components['schemas']['GrantWriteDto'];
export type GrantPage = components['schemas']['PagedResultOfGrantListDto'];

export const grantsKey = ['grants'] as const;

export function grantsListKey(search: ListSearch) {
  return [...grantsKey, 'list', search] as const;
}

export function grantKey(id: number) {
  return [...grantsKey, 'detail', id] as const;
}

/**
 * One page of the grants. No department filter, and none is possible: a grant belongs to nobody's
 * department, which is exactly what makes this the first screen of the hub on the CRUD engine's
 * global mode (design M0 §3.9).
 */
export function grantsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: grantsListKey(search),
    queryFn: async (): Promise<GrantPage> =>
      unwrap(
        await api.GET('/api/admin/grants', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export function grantQuery(id: number) {
  return queryOptions({
    queryKey: grantKey(id),
    queryFn: async (): Promise<GrantDetailDto> =>
      unwrap(await api.GET('/api/admin/grants/{id}', { params: { path: { id: String(id) } } })),
  });
}

export const superadminsKey = ['superadmins'] as const;

/** Who may bypass every policy. Only a super administrator is answered; everybody else gets 403. */
export function superadminsQuery() {
  return queryOptions({
    queryKey: superadminsKey,
    queryFn: async (): Promise<number[]> => unwrap(await api.GET('/api/admin/superadmins', {})),
  });
}
