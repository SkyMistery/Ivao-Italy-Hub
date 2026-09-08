import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap, unwrapEmpty } from '../../../shared/api/client';
import { NEW_ROW_VERSION } from '../../../shared/api/rowVersion';
import { bootstrapKey } from '../../me/queries';

import {
  calendarKindKey,
  calendarKindsKey,
  type CalendarKindDetailDto,
  type CalendarKindWriteDto,
} from './queries';
import type { CalendarKindFormValues } from './schema';

/**
 * Writing a word of the vocabulary.
 *
 * ⚠️ Every one of these invalidates the **bootstrap** as well as its own list, for the same reason
 * a grant does: the vocabulary travels in `/api/me`, so a word renamed or retired here is already
 * on screen everywhere else — in a chip on a public page, in the select of the entry form — and a
 * list that refreshed alone would leave the rest of the application saying the old word.
 */

export function toWriteDto(values: CalendarKindFormValues): CalendarKindWriteDto {
  return {
    key: values.key.trim(),
    // Sent as it stands, so the server can say which language is missing rather than being handed
    // a field that quietly became null.
    label: values.label,
    colour: values.colour,
    sort: values.sort,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/** The form as a new word starts it. */
export function emptyCalendarKind(locales: readonly string[]): CalendarKindFormValues {
  return {
    key: '',
    label: Object.fromEntries(locales.map((locale) => [locale, ''])),
    colour: 'blue',
    sort: 0,
    isActive: true,
    // The server reads an empty version as "the row as it is now", which for a create is the only
    // thing it can mean (`shared/api/rowVersion.ts`).
    rowVersion: NEW_ROW_VERSION,
  };
}

export function toFormValues(
  kind: CalendarKindDetailDto,
  locales: readonly string[],
): CalendarKindFormValues {
  const spread = (value: Record<string, string> | undefined) =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    key: kind.key,
    label: spread(kind.label),
    colour: kind.colour as CalendarKindFormValues['colour'],
    sort: kind.sort,
    isActive: kind.isActive,
    rowVersion: kind.rowVersion,
  };
}

function useInvalidate() {
  const queryClient = useQueryClient();

  return async () => {
    await queryClient.invalidateQueries({ queryKey: calendarKindsKey });
    await queryClient.invalidateQueries({ queryKey: bootstrapKey });
  };
}

export function useCreateCalendarKind() {
  const invalidate = useInvalidate();

  return useMutation({
    mutationFn: async (values: CalendarKindFormValues): Promise<CalendarKindDetailDto> =>
      unwrap(await api.POST('/api/calendar-kinds', { body: toWriteDto(values) })),
    onSuccess: invalidate,
  });
}

export function useUpdateCalendarKind(id: number) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidate();

  return useMutation({
    mutationFn: async (values: CalendarKindFormValues): Promise<CalendarKindDetailDto> =>
      unwrap(
        await api.PUT('/api/calendar-kinds/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (kind) => {
      queryClient.setQueryData(calendarKindKey(id), kind);
      await invalidate();
    },
  });
}

export function useDeleteCalendarKind() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/calendar-kinds/{id}', { params: { path: { id: String(id) } } })),
    // ⚠️ Deliberately not awaited: what has just been deleted is the row a screen is looking at, so
    // waiting for the invalidation waits for that screen's own query to refetch a row that no
    // longer exists — which 404s, retries, and never lets the caller's callbacks run
    // (`decisions/2026-09-07-dopo-la-demo.md`, defect D2).
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: calendarKindsKey });
      void queryClient.invalidateQueries({ queryKey: bootstrapKey });
    },
  });
}
