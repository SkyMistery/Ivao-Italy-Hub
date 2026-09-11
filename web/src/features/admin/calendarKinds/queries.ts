import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../../shared/list';

/**
 * Every call the calendar vocabulary screen makes, as query options. A component never fetches: it
 * asks for these and React Query decides whether that means a round trip (design M0 §7.4).
 *
 * ⚠️ This is the **back office** half of the vocabulary — every row, retired words included, with
 * who changed them and when. The half everybody else needs, and a visitor with them, travels in
 * `/api/me`: a chip on a public calendar has to say the word and the colour, and `/api/me` is the
 * one endpoint the client bootstraps from (plan §16.7).
 */

export type CalendarKindListDto = components['schemas']['CalendarKindListDto'];
export type CalendarKindDetailDto = components['schemas']['CalendarKindDetailDto'];
export type CalendarKindWriteDto = components['schemas']['CalendarKindWriteDto'];
export type CalendarKindPage = components['schemas']['PagedResultOfCalendarKindListDto'];

export const calendarKindsKey = ['calendar-kinds'] as const;

export function calendarKindsListKey(search: ListSearch) {
  return [...calendarKindsKey, 'list', search] as const;
}

export function calendarKindKey(id: number) {
  return [...calendarKindsKey, 'detail', id] as const;
}

/**
 * One page of the vocabulary. No department filter, and none is possible: the words belong to the
 * division and not to anybody's department, which is the whole reason this screen exists.
 */
export function calendarKindsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: calendarKindsListKey(search),
    queryFn: async (): Promise<CalendarKindPage> =>
      unwrap(
        await api.GET('/api/calendar-kinds', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export function calendarKindQuery(id: number) {
  return queryOptions({
    queryKey: calendarKindKey(id),
    queryFn: async (): Promise<CalendarKindDetailDto> =>
      unwrap(await api.GET('/api/calendar-kinds/{id}', { params: { path: { id: String(id) } } })),
  });
}
