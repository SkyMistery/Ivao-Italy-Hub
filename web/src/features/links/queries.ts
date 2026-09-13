import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call this feature makes, as query options. A component never fetches: it asks for these
 * and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type LinkListDto = components['schemas']['LinkListDto'];
export type LinkDetailDto = components['schemas']['LinkDetailDto'];
export type LinkWriteDto = components['schemas']['LinkWriteDto'];
export type LinkPage = components['schemas']['PagedResultOfLinkListDto'];

export const linksKey = ['links'] as const;

export function linksListKey(department: Department | undefined, search: ListSearch) {
  return [...linksKey, 'list', department, search] as const;
}

export function linkKey(id: number) {
  return [...linksKey, 'detail', id] as const;
}

/**
 * One page of links: of one department when it is given, otherwise every link the reader may read —
 * the rows of their departments and the public rows of the others (note
 * 2026-09-13-contenuti-centralizzati, 3.4). The department is a filter and not a path segment
 * because the resource is `/api/links` — one CRUD engine, one route (`CrudOptions.Filterable`).
 */
export function linksListQuery(department: Department | undefined, search: ListSearch) {
  return queryOptions({
    queryKey: linksListKey(department, search),
    queryFn: async (): Promise<LinkPage> =>
      unwrap(
        await api.GET('/api/links', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(
            department === undefined ? {} : { ownerDepartment: department },
          ),
        }),
      ),
  });
}

/**
 * Every link that is in use, whichever department wrote it, so that a screen can offer the outside
 * addresses this site already knows about instead of asking somebody to paste one.
 *
 * ⚠️ It is what makes the menu's closed set possible (decided 8 Sep 2026): an address that leaves
 * this site lives in `cms_links` and nowhere else, so the menu offers these and the server refuses
 * anything that is not one of them. A retired link is not offered — and not accepted either.
 *
 * ⚠️ And it takes what is being typed, for the reason the pages do: a page of this list is a hundred
 * rows, and a closed field that cannot offer the hundred and first cannot point at it either. The
 * server searches the title and the address, which is what the entry shows.
 */
export function activeLinksQuery(q = '') {
  return queryOptions({
    queryKey: [...linksKey, 'active', q] as const,
    queryFn: async (): Promise<LinkPage> =>
      unwrap(
        await api.GET('/api/links', {
          params: { query: { page: 1, pageSize: 100, ...(q === '' ? {} : { q }) } },
          querySerializer: listQuerySerializer({ isActive: 'true' }),
        }),
      ),
  });
}

export function linkQuery(id: number) {
  return queryOptions({
    queryKey: linkKey(id),
    queryFn: async (): Promise<LinkDetailDto> =>
      unwrap(await api.GET('/api/links/{id}', { params: { path: { id: String(id) } } })),
  });
}
