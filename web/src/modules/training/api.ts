import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

import {
  settingsFromFormValues,
  sheetItemFilters,
  sheetItemFromFormValues,
  type RatingKind,
  type SettingsFormValues,
  type SheetItemFormValues,
  type TrainingSettings,
} from './schemas';

/**
 * Every call the screens of the training make (M3): the settings through the core's settings of a module, what they are
 * chosen from — the ratings and the positions the division trains, which the module asks of the core (A4) —, and the items
 * of the evaluation sheet through the CRUD engine (A5).
 */

export type TrainingRatingDto = components['schemas']['TrainingRatingDto'];
export type TrainingPositionDto = components['schemas']['TrainingPositionDto'];
export type SheetItemDto = components['schemas']['SheetItemDto'];
export type SheetItemListDto = components['schemas']['SheetItemListDto'];
export type SheetItemPage = components['schemas']['PagedResultOfSheetItemListDto'];

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'training';

const settingsKey = ['training', 'settings'] as const;
const sheetItemsKey = ['training', 'sheet-items'] as const;

export function settingsQuery() {
  return queryOptions({
    queryKey: settingsKey,
    queryFn: async (): Promise<TrainingSettings> =>
      unwrap(
        await api.GET('/api/modules/{key}/settings', { params: { path: { key: MODULE_KEY } } }),
      ) as TrainingSettings,
  });
}

export function useSaveSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SettingsFormValues): Promise<TrainingSettings> =>
      unwrap(
        await api.PUT('/api/modules/{key}/settings', {
          params: { path: { key: MODULE_KEY } },
          body: settingsFromFormValues(values),
        }),
      ) as TrainingSettings,
    onSuccess: (saved) => {
      queryClient.setQueryData(settingsKey, saved);
    },
  });
}

/** The ratings with a practical training, ladder by ladder (design M3 §1.7). They change with a release, not with a day. */
export function ratingsQuery() {
  return queryOptions({
    queryKey: ['training', 'ratings'] as const,
    queryFn: async (): Promise<TrainingRatingDto[]> => unwrap(await api.GET('/api/training/ratings')),
    staleTime: Infinity,
  });
}

/** The positions of the division those ratings are trained on, from the reference data of the night. */
export function positionsQuery() {
  return queryOptions({
    queryKey: ['training', 'positions'] as const,
    queryFn: async (): Promise<TrainingPositionDto[]> => unwrap(await api.GET('/api/training/positions')),
  });
}

// ---- the evaluation sheet (A5) ------------------------------------------------------------------------------------------

/** A page of items, narrowed to the sheet of one ladder and rating when the search says so. */
export function sheetItemsListQuery(
  search: ListSearch & { readonly kind?: RatingKind | undefined; readonly rating?: number | undefined },
) {
  return queryOptions({
    queryKey: [...sheetItemsKey, 'list', search] as const,
    queryFn: async (): Promise<SheetItemPage> =>
      unwrap(
        await api.GET('/api/training/sheet-items', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(sheetItemFilters(search)),
        }),
      ),
  });
}

/** The last item of the sheet of one rating, if it has any: a new one goes after it. Not asked without a sheet. */
export function lastSheetItemQuery(sheet: { readonly kind: RatingKind; readonly rating: number } | null) {
  return {
    ...sheetItemsListQuery({
      ...listSearchSchema.parse({ pageSize: 1, sort: 'sort', dir: 'desc' }),
      ...(sheet ?? {}),
    }),
    enabled: sheet !== null,
  };
}

export function sheetItemQuery(id: number) {
  return queryOptions({
    queryKey: [...sheetItemsKey, 'detail', id] as const,
    queryFn: async (): Promise<SheetItemDto> =>
      unwrap(await api.GET('/api/training/sheet-items/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function useSaveSheetItem(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SheetItemFormValues): Promise<SheetItemDto> =>
      id === null
        ? unwrap(await api.POST('/api/training/sheet-items', { body: sheetItemFromFormValues(values) }))
        : unwrap(
            await api.PUT('/api/training/sheet-items/{id}', {
              params: { path: { id: String(id) } },
              body: sheetItemFromFormValues(values),
            }),
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: sheetItemsKey });
    },
  });
}

export function useDeleteSheetItem() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/training/sheet-items/{id}', { params: { path: { id: String(id) } } }),
      ),
    // Not awaited: the screen that deleted still observes the row it deleted.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: sheetItemsKey });
    },
  });
}
