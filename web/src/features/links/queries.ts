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

export function linksListKey(department: Department, search: ListSearch) {
  return [...linksKey, 'list', department, search] as const;
}

export function linkKey(id: number) {
  return [...linksKey, 'detail', id] as const;
}

/**
 * One page of the links of a department. The department is a filter and not a path segment because
 * the resource is `/api/links` — one CRUD engine, one route — and the back office narrows it
 * (`CrudOptions.Filterable`).
 */
export function linksListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: linksListKey(department, search),
    queryFn: async (): Promise<LinkPage> =>
      unwrap(
        await api.GET('/api/links', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
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
