import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call the category vocabulary makes, as query options. A component never fetches: it asks
 * for these and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type CategoryListDto = components['schemas']['CategoryListDto'];
export type CategoryDetailDto = components['schemas']['CategoryDetailDto'];
export type CategoryWriteDto = components['schemas']['CategoryWriteDto'];
export type CategoryPage = components['schemas']['PagedResultOfCategoryListDto'];
export type ContentKind = components['schemas']['ContentKind'];

export const categoryKey = ['categories'] as const;

export function categoryListKey(department: Department, search: ListSearch) {
  return [...categoryKey, 'list', department, search] as const;
}

export function categoryDetailKey(id: number) {
  return [...categoryKey, 'detail', id] as const;
}

export function categoriesOfKindKey(department: Department, kind: ContentKind) {
  return [...categoryKey, 'ofKind', department, kind] as const;
}

/** One page of the vocabulary of a department, as the back office list shows it. */
export function categoryListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: categoryListKey(department, search),
    queryFn: async (): Promise<CategoryPage> =>
      unwrap(
        await api.GET('/api/categories', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
        }),
      ),
  });
}

export function categoryQuery(id: number) {
  return queryOptions({
    queryKey: categoryDetailKey(id),
    queryFn: async (): Promise<CategoryDetailDto> =>
      unwrap(await api.GET('/api/categories/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** How many shelves one department is expected to have. Well above what a select stays usable at. */
const VOCABULARY_PAGE_SIZE = 100;

/**
 * The shelves a row of this kind may be filed under, for the select in the content form. Active
 * ones only: retiring a category means nothing new goes on that shelf, while the rows already
 * there keep their key (design M1 §3.4).
 */
export function categoriesOfKindQuery(department: Department, kind: ContentKind) {
  const search = listSearchSchema.parse({ pageSize: VOCABULARY_PAGE_SIZE, sort: 'sort' });

  return queryOptions({
    queryKey: categoriesOfKindKey(department, kind),
    queryFn: async (): Promise<CategoryPage> =>
      unwrap(
        await api.GET('/api/categories', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({
            ownerDepartment: department,
            kind,
            isActive: 'true',
          }),
        }),
      ),
  });
}
