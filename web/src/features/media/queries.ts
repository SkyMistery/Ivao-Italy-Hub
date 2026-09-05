import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, listSearchSchema, type ListSearch } from '../../shared/list';
import type { MediaLibraryQuery, MediaPage, PickableMedia } from '../../shared/ui';

/**
 * Every call the media library makes, as query options. A component never fetches: it asks for
 * these and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type MediaListDto = components['schemas']['MediaListDto'];
export type MediaDetailDto = components['schemas']['MediaDetailDto'];
export type MediaWriteDto = components['schemas']['MediaWriteDto'];
export type MediaListPage = components['schemas']['PagedResultOfMediaListDto'];
export type ContentPage = components['schemas']['PagedResultOfContentListDto'];

export const mediaKey = ['media'] as const;

export function mediaListKey(department: Department, search: ListSearch) {
  return [...mediaKey, 'list', department, search] as const;
}

export function mediaItemKey(id: number) {
  return [...mediaKey, 'detail', id] as const;
}

export function mediaUsageKey(mediaId: number) {
  return [...mediaKey, 'usage', mediaId] as const;
}

/** One page of the library of a department. */
export function mediaListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: mediaListKey(department, search),
    queryFn: async (): Promise<MediaListPage> =>
      unwrap(
        await api.GET('/api/media', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
        }),
      ),
  });
}

export function mediaQuery(id: number) {
  return queryOptions({
    queryKey: mediaItemKey(id),
    queryFn: async (): Promise<MediaDetailDto> =>
      unwrap(await api.GET('/api/media/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** How many files a picker shows before it needs a page of its own. */
const PICKER_PAGE_SIZE = 24;

/**
 * What `MediaPicker` chooses from: the newest files of one department. It is the same resource and
 * the same list engine as the back office screen, asked for a smaller page and narrowed to what a
 * picker actually draws — which is also what lets the form generator carry it without knowing that
 * `/api/media` exists.
 */
export function mediaPickerQuery(department: Department): MediaLibraryQuery {
  const search = listSearchSchema.parse({ pageSize: PICKER_PAGE_SIZE, sort: 'createdAt', dir: 'desc' });

  // Typed as the loose key the generator's prop declares: a picker is carried around by a
  // component that cannot know which resource it came from, so the key has to stop being specific
  // right here rather than at every place that passes it on.
  const queryKey: readonly unknown[] = [...mediaKey, 'picker', department, PICKER_PAGE_SIZE];

  return queryOptions({
    queryKey,
    queryFn: async (): Promise<MediaPage<PickableMedia>> => {
      const page = unwrap(
        await api.GET('/api/media', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
        }),
      );

      return { items: page.items, total: page.total };
    },
  });
}

/**
 * Which contents show this file, asked of the resource that owns the answer. There is no endpoint
 * for it: `filter[usesMedia]` is a filter of the content list, resolved on the server by the one
 * helper that knows how to ask a JSON column (`JsonQuery`).
 *
 * ⚠️ Departmental, like every list: it answers for the departments the reader works in. Somebody
 * deleting a file therefore sees the pages they could have broken, not every page in the division.
 */
export function mediaUsageQuery(mediaId: number) {
  return queryOptions({
    queryKey: mediaUsageKey(mediaId),
    queryFn: async (): Promise<ContentPage> =>
      unwrap(
        await api.GET('/api/content', {
          params: { query: toQuery(listSearchSchema.parse({})) },
          querySerializer: listQuerySerializer({ usesMedia: String(mediaId) }),
        }),
      ),
  });
}
