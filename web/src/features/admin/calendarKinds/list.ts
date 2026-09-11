import { col, type ColumnSpec } from '../../../shared/list';

import type { CalendarKindListDto } from './queries';

/**
 * The columns of the calendar vocabulary. Declarations only, like every other list of the hub:
 * `DataList` decides how a translated value, a number or a flag is drawn (design M0 §7.5).
 *
 * `sortable` says what the server declared in `CrudOptions.Sortable`; a column that claims more is
 * answered with 400.
 */
export const calendarKindColumns: readonly ColumnSpec<CalendarKindListDto>[] = [
  col.localized('label'),
  col.text('key', { sortable: true }),
  // The colour as the word it is. A swatch would be nicer and would need a column kind of its own,
  // which is a decision about the list engine rather than about this screen.
  col.text('colour'),
  col.number('sort', { sortable: true }),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];
