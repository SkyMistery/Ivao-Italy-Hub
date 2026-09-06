import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';

import { calendarEntryKey, calendarKey, type CalendarDetailDto, type CalendarWriteDto } from './queries';
import type { CalendarFormValues } from './schema';

/**
 * Writing a calendar entry. An ordinary payload for an ordinary CRUD resource: what makes the
 * calendar unusual is not how a row is written but that some rows are not written here at all —
 * a projection belongs to the module it mirrors, and the engine refuses it (design M1 §4).
 */

/** A translated field with nothing written in it is absent, not an object full of empty strings. */
function trimLocalized(value: Record<string, string>): LocalizedString | null {
  const written = Object.entries(value).filter(([, text]) => text.trim().length > 0);
  return written.length === 0 ? null : Object.fromEntries(written);
}

export function toWriteDto(values: CalendarFormValues): CalendarWriteDto {
  return {
    ownerDepartment: values.ownerDepartment,
    visibility: values.visibility,
    kind: values.kind.trim(),
    // Sent as it stands, so the server can name the language that is missing rather than being
    // handed a field that quietly became null.
    title: values.title,
    description: trimLocalized(values.description),
    startsAtUtc: values.startsAtUtc,
    // Null and not absent: the contract declares it nullable, and "no end" is a fact the row holds
    // rather than a field the payload forgot.
    endsAtUtc: values.endsAtUtc === undefined || values.endsAtUtc === '' ? null : values.endsAtUtc,
    allDay: values.allDay,
    url: values.url === undefined || values.url.trim() === '' ? null : values.url.trim(),
  };
}

/** The form as a new entry starts it: in the department of the route, an hour from now, staff only. */
export function emptyCalendarEntry(
  department: Department,
  locales: readonly string[],
  now: Date,
): CalendarFormValues {
  const start = new Date(now);
  start.setUTCMinutes(0, 0, 0);
  start.setUTCHours(start.getUTCHours() + 1);

  const spread = () => Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    ownerDepartment: department,
    // Drafted where only the staff can see it. Making an entry public is a choice somebody takes.
    visibility: 'Staff',
    kind: '',
    title: spread(),
    description: spread(),
    startsAtUtc: start.toISOString(),
    allDay: false,
    url: '',
  };
}

/** The form as an existing entry fills it, with every language of the division present as a tab. */
export function toFormValues(entry: CalendarDetailDto, locales: readonly string[]): CalendarFormValues {
  const spread = (value: LocalizedString | null | undefined): Record<string, string> =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    ownerDepartment: entry.ownerDepartment,
    visibility: entry.visibility,
    kind: entry.kind,
    title: spread(entry.title),
    description: spread(entry.description),
    startsAtUtc: entry.startsAtUtc,
    ...(entry.endsAtUtc === null ? {} : { endsAtUtc: entry.endsAtUtc }),
    allDay: entry.allDay,
    url: entry.url,
  };
}

export function useCreateCalendarEntry() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: CalendarFormValues): Promise<CalendarDetailDto> =>
      unwrap(await api.POST('/api/calendar', { body: toWriteDto(values) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: calendarKey });
    },
  });
}

export function useUpdateCalendarEntry(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: CalendarFormValues): Promise<CalendarDetailDto> =>
      unwrap(
        await api.PUT('/api/calendar/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (entry) => {
      queryClient.setQueryData(calendarEntryKey(id), entry);
      await queryClient.invalidateQueries({ queryKey: calendarKey });
    },
  });
}

export function useDeleteCalendarEntry() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/calendar/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: calendarKey });
    },
  });
}
