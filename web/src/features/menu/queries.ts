import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call the site menu makes, as query options. A component never fetches: it asks for these
 * and React Query decides whether that means a round trip (design M0 §7.4).
 */

export type MenuItemListDto = components['schemas']['MenuItemListDto'];
export type MenuItemDetailDto = components['schemas']['MenuItemDetailDto'];
export type MenuItemWriteDto = components['schemas']['MenuItemWriteDto'];
export type MenuItemPage = components['schemas']['PagedResultOfMenuItemListDto'];
export type MenuScope = components['schemas']['MenuScope'];

export const menuKey = ['menu'] as const;

export function menuListKey(search: ListSearch) {
  return [...menuKey, 'list', search] as const;
}

export function menuDetailKey(id: number) {
  return [...menuKey, 'detail', id] as const;
}

export function menuParentsKey(scope: MenuScope) {
  return [...menuKey, 'parents', scope] as const;
}

/**
 * One page of the menu. There is no department in the query, unlike every other list of the back
 * office: every row belongs to the web team, so the server narrows it without being asked
 * (`MenuItem.Owner`), and a second way of saying it here would be a second thing to keep true.
 */
export function menuListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: menuListKey(search),
    queryFn: async (): Promise<MenuItemPage> =>
      unwrap(await api.GET('/api/menu', { params: { query: toQuery(search) } })),
  });
}

export function menuItemQuery(id: number) {
  return queryOptions({
    queryKey: menuDetailKey(id),
    queryFn: async (): Promise<MenuItemDetailDto> =>
      unwrap(await api.GET('/api/menu/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** As many entries as one menu of a site ever sensibly holds, which is well under a page. */
const MENU_PAGE_SIZE = 100;

/**
 * The entries of one menu, for the select that says what a new one hangs under. Depth is one and
 * the server refuses anything deeper, so the form offers what will be accepted rather than letting
 * somebody find out on save; which of these rows are top level is decided where they are drawn,
 * because `filter[parentId]=` has no way of spelling "nothing" and inventing one for this single
 * select would change what a filter means for every resource of the hub.
 */
export function menuParentsQuery(scope: MenuScope) {
  const search = listSearchSchema.parse({ pageSize: MENU_PAGE_SIZE, sort: 'sort' });

  return queryOptions({
    queryKey: menuParentsKey(scope),
    queryFn: async (): Promise<MenuItemPage> =>
      unwrap(
        await api.GET('/api/menu', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ scope }),
        }),
      ),
  });
}
