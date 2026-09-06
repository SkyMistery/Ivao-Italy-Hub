import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call the calendar makes, as query options. A component never fetches: it asks for these
 * and React Query decides whether that means a round trip (design M0 §7.4).
 *
 * ⚠️ There is nothing here for the **public** calendar, and that is the point: what a visitor reads
 * is the `calendar` data block, asked through `blockDataQuery` like any other live block. A second
 * reader of `cms_calendar_entries` would be a second place for the two to disagree about what a
 * department may show (design M1 §4).
 */

export type CalendarListDto = components['schemas']['CalendarListDto'];
export type CalendarDetailDto = components['schemas']['CalendarDetailDto'];
export type CalendarWriteDto = components['schemas']['CalendarWriteDto'];
export type CalendarPage = components['schemas']['PagedResultOfCalendarListDto'];

export const calendarKey = ['calendar'] as const;

export function calendarListKey(department: Department, search: ListSearch) {
  return [...calendarKey, 'list', department, search] as const;
}

export function calendarEntryKey(id: number) {
  return [...calendarKey, 'detail', id] as const;
}

/** One page of the entries of a department, the staff's own and the projected ones together. */
export function calendarListQuery(department: Department, search: ListSearch) {
  return queryOptions({
    queryKey: calendarListKey(department, search),
    queryFn: async (): Promise<CalendarPage> =>
      unwrap(
        await api.GET('/api/calendar', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ ownerDepartment: department }),
        }),
      ),
  });
}

export function calendarEntryQuery(id: number) {
  return queryOptions({
    queryKey: calendarEntryKey(id),
    queryFn: async (): Promise<CalendarDetailDto> =>
      unwrap(await api.GET('/api/calendar/{id}', { params: { path: { id: String(id) } } })),
  });
}
