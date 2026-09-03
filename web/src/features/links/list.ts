import { col, type ColumnSpec } from '../../shared/list';

import type { LinkListDto } from './queries';

/**
 * The columns of the links list. Declarations only: `DataList` decides how a translated value, a
 * date or a badge is drawn, so the same kind of column looks the same in every list of the hub
 * (design M0 §7.5).
 *
 * `sortable` is not a matter of taste: it says what the server declared in `CrudOptions.Sortable`,
 * and a column marked sortable that the server does not know is answered with 400.
 */
export const linkColumns: readonly ColumnSpec<LinkListDto>[] = [
  col.localized('title'),
  col.text('url', { sortable: true }),
  col.badge('visibility', 'links'),
  col.text('category', { sortable: true }),
  col.number('sort', { sortable: true }),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];
