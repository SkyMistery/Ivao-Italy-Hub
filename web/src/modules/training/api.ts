import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import {
  listQuerySerializer,
  listSearchSchema,
  toQuery,
  type ListSearch,
  type Page,
} from '../../shared/list';

import {
  settingsFromFormValues,
  sheetItemFilters,
  sheetItemFromFormValues,
  staffTrainingsFilters,
  type RatingKind,
  type SettingsFormValues,
  type SheetItemFormValues,
  type StaffQueue,
  type TrainingSettings,
} from './schemas';

/**
 * Every call the screens of the training make (M3): the settings through the core's settings of a module, what they are
 * chosen from — the ratings and the positions the division trains, which the module asks of the core (A4) —, the items
 * of the evaluation sheet through the CRUD engine (A5), the trainee's own side — their page, the request and its
 * cancellation (A6) —, and the staff's side — the list of the trainings, the page of one, accepting, refusing and
 * assigning its trainer (A7).
 */

export type TrainingRatingDto = components['schemas']['TrainingRatingDto'];
export type TrainingPositionDto = components['schemas']['TrainingPositionDto'];
export type SheetItemDto = components['schemas']['SheetItemDto'];
export type SheetItemListDto = components['schemas']['SheetItemListDto'];
export type SheetItemPage = components['schemas']['PagedResultOfSheetItemListDto'];
export type MyTrainingDto = components['schemas']['MyTrainingDto'];
export type MyTrainingPathDto = components['schemas']['MyTrainingPathDto'];
export type TraineeTrainingDto = components['schemas']['TraineeTrainingDto'];
export type TrainingRequestWriteDto = components['schemas']['TrainingRequestWriteDto'];
export type TrainingState = components['schemas']['TrainingState'];
export type StaffTrainingDto = components['schemas']['StaffTrainingDto'];
export type StaffTrainingRowDto = components['schemas']['StaffTrainingRowDto'];
export type TrainerCandidateDto = components['schemas']['TrainerCandidateDto'];
export type TrainingMemberDto = components['schemas']['TrainingMemberDto'];
export type TrainingAssignmentDto = components['schemas']['TrainingAssignmentDto'];

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'training';

const settingsKey = ['training', 'settings'] as const;
const sheetItemsKey = ['training', 'sheet-items'] as const;
const mineKey = ['training', 'mine'] as const;

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

// ---- the trainee's side (A6) --------------------------------------------------------------------------------------------

/**
 * The trainee's page, the one answer both of their screens read (design M3 §4.1): who they are, where they stand on each
 * ladder — what they may ask for, or the first rule that refuses with what it needs to say why —, the question on the
 * theory, and their trainings, newest first.
 */
export function mineQuery() {
  return queryOptions({
    queryKey: mineKey,
    queryFn: async (): Promise<MyTrainingDto> => unwrap(await api.GET('/api/training/mine')),
  });
}

/**
 * A request (§2.2): the training written — asked for, or refused by the hub when the trainee said the theory is not passed —
 * or the refusals, field by field, as an `ApiError`. The page is read again either way: what the trainee may ask for now is
 * the server's to say.
 */
export function useRequestTraining() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: TrainingRequestWriteDto): Promise<TraineeTrainingDto> =>
      unwrap(await api.POST('/api/training/mine', { body })),
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: mineKey });
    },
  });
}

/** A request taken back while nobody accepted it, at the version the trainee saw: 409 when it moved on since. */
export function useCancelTraining() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (training: TraineeTrainingDto): Promise<TraineeTrainingDto> =>
      unwrap(
        await api.POST('/api/training/mine/{id}/cancel', {
          params: { path: { id: training.id } },
          body: { rowVersion: training.rowVersion },
        }),
      ),
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: mineKey });
    },
  });
}

// ---- the staff's side (A7) ----------------------------------------------------------------------------------------------

const staffKey = ['training', 'staff'] as const;

/**
 * A person as the staff's pages name them: the name the hub has, and the VID that always is. (The core's helper for a person
 * whose data was erased arrives with A12a; no data of a trainee is erased before A12b.)
 */
export function memberLabel(member: TrainingMemberDto): string {
  return member.name === null || member.name === ''
    ? String(member.vid)
    : `${member.name} (${String(member.vid)})`;
}

/** A row of the list as the list draws it: the trainee and the trainer written out, for its columns of text. */
export type StaffTrainingRow = StaffTrainingRowDto & {
  readonly traineeName: string;
  readonly trainerName: string | null;
};

/**
 * A page of the staff's list (design M3 §4.2): every training, or those of one view — to approve, to assign, in progress, to
 * close, the history — and of one ladder, as the search says.
 */
export function staffTrainingsQuery(
  search: ListSearch & {
    readonly queue?: StaffQueue | undefined;
    readonly kind?: RatingKind | undefined;
  },
) {
  return queryOptions({
    queryKey: [...staffKey, 'list', search] as const,
    queryFn: async (): Promise<Page<StaffTrainingRow>> => {
      const page = unwrap(
        await api.GET('/api/training/queue', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(staffTrainingsFilters(search)),
        }),
      );

      return {
        ...page,
        items: page.items.map((row) => ({
          ...row,
          traineeName: memberLabel(row.trainee),
          trainerName: row.trainer === null ? null : memberLabel(row.trainer),
        })),
      };
    },
  });
}

/** The page of one training: the request, the decision, the trainer, and what the reader may do on it now. */
export function staffTrainingQuery(id: number) {
  return queryOptions({
    queryKey: [...staffKey, 'one', id] as const,
    queryFn: async (): Promise<StaffTrainingDto> =>
      unwrap(await api.GET('/api/training/trainings/{id}', { params: { path: { id } } })),
  });
}

/** Whoever may train it (§2.4), asked only by whoever may assign it: the server's rule, never repeated here. */
export function trainerCandidatesQuery(id: number) {
  return queryOptions({
    queryKey: [...staffKey, 'trainers', id] as const,
    queryFn: async (): Promise<TrainerCandidateDto[]> =>
      unwrap(await api.GET('/api/training/trainings/{id}/trainers', { params: { path: { id } } })),
  });
}

/** The three steps of the staff on a training, each answering with the page as it is afterwards. */
export type StaffStep =
  | { readonly step: 'accept'; readonly rowVersion: string }
  | { readonly step: 'reject'; readonly reason: string; readonly rowVersion: string }
  | { readonly step: 'assign'; readonly assignment: TrainingAssignmentDto };

export function useStaffStep(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (step: StaffStep): Promise<StaffTrainingDto> => {
      const path = { params: { path: { id } } };
      switch (step.step) {
        case 'accept':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/accept', {
              ...path,
              body: { rowVersion: step.rowVersion },
            }),
          );
        case 'reject':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/reject', {
              ...path,
              body: { reason: step.reason, rowVersion: step.rowVersion },
            }),
          );
        case 'assign':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/assign', { ...path, body: step.assignment }),
          );
      }
    },
    onSuccess: async (page) => {
      queryClient.setQueryData(staffTrainingQuery(id).queryKey, page);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: [...staffKey, 'list'] }),
        queryClient.invalidateQueries({ queryKey: [...staffKey, 'trainers', id] }),
      ]);
    },
  });
}
