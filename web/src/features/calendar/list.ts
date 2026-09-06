import { col, type ColumnSpec } from '../../shared/list';

import type { CalendarListDto } from './queries';

/**
 * The columns of the calendar of a department. Declarations only, like every other list of the hub.
 *
 * `isProjection` is drawn as the status badge, and it is the column that matters most on this
 * screen: an entry a module owns is shown and cannot be edited, and being told why is the whole
 * difference between a disabled button and a ticket (design M1 §4). The server refuses the write on
 * the same answer this column draws, because the entity is the one place that decides it.
 */
export const calendarColumns: readonly ColumnSpec<CalendarListDto>[] = [
  col.localized('title'),
  col.text('kind', { sortable: true }),
  col.date('startsAtUtc', { sortable: true }),
  col.date('endsAtUtc'),
  col.boolean('allDay'),
  col.boolean('isProjection'),
  col.badge('visibility', 'calendar'),
  col.date('updatedAt', { sortable: true }),
];
