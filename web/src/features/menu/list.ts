import { col, type ColumnSpec } from '../../shared/list';

import type { MenuItemListDto } from './queries';

/**
 * The columns of the site menu. Declarations only, like every other list of the hub: `DataList`
 * decides how a translated value, a number or a badge is drawn (design M0 §7.5).
 *
 * `sortable` says what the server declared in `CrudOptions.Sortable`; a column that claims more is
 * answered with 400.
 */
export const menuColumns: readonly ColumnSpec<MenuItemListDto>[] = [
  col.localized('label'),
  col.text('path', { sortable: true }),
  col.badge('scope', 'menu', { sortable: true }),
  // The visibility of a menu entry, read from the words the content screens already use: an entry
  // is shown to the same four audiences a page is, and four labels written twice are four labels
  // that drift.
  col.badge('visibility', 'content'),
  col.number('sort', { sortable: true }),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];
