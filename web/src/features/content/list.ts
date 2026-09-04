import { col, type ColumnSpec } from '../../shared/list';

import type { ContentListDto } from './queries';

/**
 * The columns of the content list. Declarations only: `DataList` decides how a translated value, a
 * date or a badge is drawn, so the same kind of column looks the same in every list of the hub
 * (design M0 §7.5).
 *
 * `sortable` says what the server declared in `CrudOptions.Sortable`, not what would be nice: a
 * column marked sortable that the server does not know is answered with 400.
 */
export const contentColumns: readonly ColumnSpec<ContentListDto>[] = [
  col.localized('title'),
  col.text('slug', { sortable: true }),
  col.badge('kind', 'content', { sortable: true }),
  col.badge('status', 'content', { sortable: true }),
  col.badge('visibility', 'content'),
  col.date('publishedAt', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];
