import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call this feature makes, as query options. A component never fetches: it asks for these
 * and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type ContentListDto = components['schemas']['ContentListDto'];
export type ContentDetailDto = components['schemas']['ContentDetailDto'];
export type ContentWriteDto = components['schemas']['ContentWriteDto'];
export type PublicContentDto = components['schemas']['PublicContentDto'];
export type ContentKind = components['schemas']['ContentKind'];
export type ContentPage = components['schemas']['PagedResultOfContentListDto'];

export const contentKey = ['content'] as const;

export function contentListKey(department: Department, search: ListSearch, kind: ContentKind | null) {
  return [...contentKey, 'list', department, kind, search] as const;
}

export function contentDetailKey(id: number) {
  return [...contentKey, 'detail', id] as const;
}

export function templatesKey(kind: ContentKind | null) {
  return [...contentKey, 'templates', kind] as const;
}

export function publicContentKey(kind: ContentKind, slug: string) {
  return [...contentKey, 'public', kind, slug] as const;
}

/**
 * One page of the content of a department. Templates are not in it: the server keeps them out
 * unless a caller asks, which is what the template picker does through `templatesQuery`.
 */
export function contentListQuery(
  department: Department,
  search: ListSearch,
  kind: ContentKind | null = null,
) {
  return queryOptions({
    queryKey: contentListKey(department, search, kind),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({
            ownerDepartment: department,
            ...(kind === null ? {} : { kind }),
          }),
        }),
      ),
  });
}

/** The templates a page may be made from. Owned by the web team, readable by every staff member. */
export function templatesQuery(kind: ContentKind | null = null) {
  return queryOptions({
    queryKey: templatesKey(kind),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: { page: 1, pageSize: 100 } },
          querySerializer: listQuerySerializer({
            isTemplate: 'true',
            ...(kind === null ? {} : { kind }),
          }),
        }),
      ),
  });
}

export function contentQuery(id: number) {
  return queryOptions({
    queryKey: contentDetailKey(id),
    queryFn: async (): Promise<ContentDetailDto> =>
      unwrap(await api.GET('/api/content/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** What a visitor reads: the published version, or nothing at all. */
export function publicContentQuery(kind: ContentKind, slug: string) {
  return queryOptions({
    queryKey: publicContentKey(kind, slug),
    queryFn: async (): Promise<PublicContentDto> =>
      unwrap(
        await api.GET('/api/content/public/{kind}/{slug}', {
          params: { path: { kind, slug } },
        }),
      ),
  });
}
