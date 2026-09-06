import { col, type ColumnSpec } from '../../shared/list';

import type { CategoryListDto } from './queries';

/**
 * The columns of the category vocabulary. Declarations only, like every other list of the hub:
 * `DataList` decides how a translated value, a number or a badge is drawn (design M0 §7.5).
 *
 * `sortable` says what the server declared in `CrudOptions.Sortable`; a column that claims more is
 * answered with 400.
 */
export const categoryColumns: readonly ColumnSpec<CategoryListDto>[] = [
  col.localized('label'),
  col.text('key', { sortable: true }),
  col.badge('kind', 'categories', { sortable: true }),
  col.number('sort', { sortable: true }),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];
