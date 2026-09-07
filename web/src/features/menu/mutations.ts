import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';

import { menuDetailKey, menuKey, type MenuItemDetailDto, type MenuItemWriteDto } from './queries';
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
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: menuKey });
    },
  });
}
