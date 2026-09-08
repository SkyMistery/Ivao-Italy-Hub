import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';

import {
  menuDetailKey,
  menuItemQuery,
  menuKey,
  type MenuItemDetailDto,
  type MenuItemWriteDto,
} from './queries';
import type { MenuItemFormValues } from './schema';

/**
 * Writing a menu entry. An ordinary payload on an ordinary CRUD resource: what the editorial menu
 * costs this hub is this file and its four siblings, which is what design M1 §8.1 asks it to cost.
 */

export function toWriteDto(values: MenuItemFormValues): MenuItemWriteDto {
  return {
    scope: values.scope,
    // Empty is not "the entry number zero", it is "no parent": the select carries text and the
    // column is a nullable identifier, and this is the one line where the two meet.
    parentId: values.parentId ? Number(values.parentId) : null,
    // Sent as it stands, so the server can name the language that is missing rather than being
    // handed a field that quietly became null.
    label: values.label,
    path: values.path.trim(),
    sort: values.sort,
    visibility: values.visibility,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/** The form as a new entry starts it: in the public menu, at the top level, visible to everybody. */
export function emptyMenuItem(locales: readonly string[]): MenuItemFormValues {
  return {
    scope: 'Public',
    parentId: '',
    label: Object.fromEntries(locales.map((locale) => [locale, ''])),
    path: '',
    sort: 0,
    visibility: 'Public',
    isActive: true,
    rowVersion: NEW_ROW_VERSION,
  };
}

/** The form as an existing entry fills it, with every language of the division present as a tab. */
export function toFormValues(item: MenuItemDetailDto, locales: readonly string[]): MenuItemFormValues {
  return {
    scope: item.scope,
    parentId: item.parentId === null ? '' : String(item.parentId),
    label: Object.fromEntries(locales.map((locale) => [locale, item.label?.[locale] ?? ''])),
    path: item.path,
    sort: item.sort,
    visibility: item.visibility,
    isActive: item.isActive,
    rowVersion: item.rowVersion,
  };
}

export function useCreateMenuItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: MenuItemFormValues): Promise<MenuItemDetailDto> =>
      unwrap(await api.POST('/api/menu', { body: toWriteDto(values) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: menuKey });
    },
  });
}

export function useUpdateMenuItem(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: MenuItemFormValues): Promise<MenuItemDetailDto> =>
      unwrap(
        await api.PUT('/api/menu/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (item) => {
      queryClient.setQueryData(menuDetailKey(id), item);
      await queryClient.invalidateQueries({ queryKey: menuKey });
    },
  });
}

/**
 * Deleting an entry. ⚠️ The page it pointed at is untouched: a menu entry is a signpost and not
 * the thing it points at, so what disappears is the signpost. Putting it back is a new row.
 */
export function useDeleteMenuItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/menu/{id}', { params: { path: { id: String(id) } } })),
    // ⚠️ Deliberately not awaited, and deliberately not `async`. What has just been deleted is the
    // row a screen is **looking at**, so invalidating waits for that screen's own query to refetch
    // — a row that no longer exists. The refetch 404s and retries, `onSuccess` never settles, and
    // the callbacks a caller passed to `mutate` never run: the screen deletes the row and then sits
    // there saying nothing. Found in G12, and caused by making the screens read the query rather
    // than the loader (`decisions/2026-09-07-il-loader-non-e-la-riga.md`), which is what gave that
    // query an observer in the first place.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: menuKey });
    },
  });
}

/**
 * Changing one field of one entry from the list, without opening it.
 *
 * ⚠️ It reads the row and writes it back, which is the decision of
 * `decisions/2026-09-08-modificare-da-una-lista.md`: the list is handed a projection and the engine
 * writes with the whole payload, so a cell has to fetch the row it is editing before it can save
 * one field of it. Two round trips, no new verb on the API — and `rowVersion` still answers 409 to
 * somebody who saved in between, exactly as it does from the form.
 *
 * It lives here and not in `DataList` because only this feature knows what a menu entry is: the
 * list engine draws a control and hands back a field and a value.
 */
export function useInlineEditMenuItem(locales: readonly string[]) {
  const queryClient = useQueryClient();

  return async (id: number, field: string, value: unknown): Promise<void> => {
    const current = await queryClient.fetchQuery(menuItemQuery(id));
    const values = { ...toFormValues(current, locales), [field]: value };

    const saved = unwrap(
      await api.PUT('/api/menu/{id}', {
        params: { path: { id: String(id) } },
        body: toWriteDto(values),
      }),
    );

    queryClient.setQueryData(menuDetailKey(id), saved);
    // Not awaited: the row on screen is already right, and waiting for every list under that key to
    // refetch is what made a delete say nothing at all (`2026-09-07-dopo-la-demo.md`, defect D2).
    void queryClient.invalidateQueries({ queryKey: menuKey });
  };
}
