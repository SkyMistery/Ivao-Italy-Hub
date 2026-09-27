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
 * cancellation (A6), the page of one training and the choice of its date (A8) —, and the staff's side — the list of the
 * trainings, the page of one, accepting, refusing and assigning its trainer (A7), what a date meets, the dates proposed and
 * one taken back, the date set by hand, and the closing (A8); what the session came to — rescheduled, not attended, or
 * reported with the sheet (A9).
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
export type TraineeSlotDto = components['schemas']['TraineeSlotDto'];
export type StaffSlotDto = components['schemas']['StaffSlotDto'];
export type DateWarning = components['schemas']['DateWarning'];
export type DateConflictsDto = components['schemas']['DateConflictsDto'];
export type TrainingSlotWriteDto = components['schemas']['TrainingSlotWriteDto'];
export type TrainingSlotsWriteDto = components['schemas']['TrainingSlotsWriteDto'];
export type TrainingDateWriteDto = components['schemas']['TrainingDateWriteDto'];
export type SheetSection = components['schemas']['SheetSection'];
export type TheoryMark = components['schemas']['TheoryMark'];
export type SessionOutcome = components['schemas']['SessionOutcome'];
export type StaffEvaluationDto = components['schemas']['StaffEvaluationDto'];
export type StaffSessionDto = components['schemas']['StaffSessionDto'];
export type TraineeEvaluationDto = components['schemas']['TraineeEvaluationDto'];
export type TraineeSessionDto = components['schemas']['TraineeSessionDto'];
export type TrainingEvaluationWriteDto = components['schemas']['TrainingEvaluationWriteDto'];
export type TrainingReportDto = components['schemas']['TrainingReportDto'];

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

/**
 * One training of the trainee's (design M3 §4.1, A8): what `/training/mine` says of it, with its trainer and, while it waits for
 * its date, the dates still to come to choose from. Another member's is a 404. Below the trainee's page in the cache, so every
 * step of theirs reads it again too.
 */
export function mineOneQuery(id: number) {
  return queryOptions({
    queryKey: [...mineKey, 'one', id] as const,
    queryFn: async (): Promise<TraineeTrainingDto> =>
      unwrap(await api.GET('/api/training/mine/{id}', { params: { path: { id } } })),
  });
}

/**
 * The trainee's choice among the dates proposed (§2.5, d1), at the version of the training they saw: their training, dated, or
 * the refusals — and a 409 when the trainer changed the dates meanwhile. Their pages are read again either way: what may be
 * chosen now is the server's to say.
 */
export function useChooseDate(training: TraineeTrainingDto) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (slotId: number): Promise<TraineeTrainingDto> =>
      unwrap(
        await api.POST('/api/training/mine/{id}/choose', {
          params: { path: { id: training.id } },
          body: { slotId, rowVersion: training.rowVersion },
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

/**
 * A state as the pages show it (R.4): a dated training whose day is over in the division's time zone shows as held (design M3
 * §1.2), which nothing writes — the server says it (`held`).
 */
export type ShownState = TrainingState | 'Held';

export function shownState(training: { readonly state: TrainingState; readonly held: boolean }): ShownState {
  return training.state === 'Scheduled' && training.held ? 'Held' : training.state;
}

/**
 * A row of the list as the list draws it: the trainee and the trainer written out, for its columns of text, and its state as
 * the pages show it — held, from the day after its session.
 */
export type StaffTrainingRow = Omit<StaffTrainingRowDto, 'state'> & {
  readonly state: ShownState;
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
          state: shownState(row),
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

/**
 * The steps of the staff on a training, each answering with the page as it is afterwards: accept, refuse, assign (A7); the dates
 * proposed, one taken back, the date set by hand, and the closing (A8); the session rescheduled with its internal notes, not
 * attended, or reported (A9).
 */
export type StaffStep =
  | { readonly step: 'accept'; readonly rowVersion: string }
  | { readonly step: 'reject'; readonly reason: string; readonly rowVersion: string }
  | { readonly step: 'assign'; readonly assignment: TrainingAssignmentDto }
  | { readonly step: 'propose'; readonly proposal: TrainingSlotsWriteDto }
  | { readonly step: 'withdraw'; readonly slotId: number; readonly rowVersion: string }
  | { readonly step: 'date'; readonly date: TrainingDateWriteDto }
  | { readonly step: 'close'; readonly reason: string; readonly rowVersion: string }
  | { readonly step: 'reschedule'; readonly notes: string | null; readonly rowVersion: string }
  | { readonly step: 'noShow'; readonly rowVersion: string }
  | { readonly step: 'report'; readonly report: TrainingReportDto };

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
        case 'propose':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/slots', { ...path, body: step.proposal }),
          );
        case 'withdraw':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/slots/{slotId}/withdraw', {
              params: { path: { id, slotId: step.slotId } },
              body: { rowVersion: step.rowVersion },
            }),
          );
        case 'date':
          return unwrap(await api.POST('/api/training/trainings/{id}/date', { ...path, body: step.date }));
        case 'close':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/close', {
              ...path,
              body: { reason: step.reason, rowVersion: step.rowVersion },
            }),
          );
        case 'reschedule':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/reschedule', {
              ...path,
              body: { notes: step.notes, rowVersion: step.rowVersion },
            }),
          );
        case 'noShow':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/no-show', {
              ...path,
              body: { rowVersion: step.rowVersion },
            }),
          );
        case 'report':
          return unwrap(
            await api.POST('/api/training/trainings/{id}/report', { ...path, body: step.report }),
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

/**
 * The page of a training read again, after a step somebody else overtook (409): a form of the dates keeps what was written in
 * it, and the next press goes with the version there is now.
 */
export function useRereadStaffTraining(id: number): () => Promise<void> {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: staffTrainingQuery(id).queryKey });
}

/**
 * What a date meets on the days it touches (design M3 §2.5), asked before anybody writes it by whoever may conduct the
 * training: the division's policy, and the warnings — none when the policy is not to look. Without an end, the day it starts.
 */
export async function dateConflicts(
  id: number,
  startsAtUtc: string,
  endsAtUtc: string | null,
): Promise<DateConflictsDto> {
  return unwrap(
    await api.GET('/api/training/trainings/{id}/conflicts', {
      params: { path: { id }, query: endsAtUtc === null ? { startsAtUtc } : { startsAtUtc, endsAtUtc } },
    }),
  );
}
