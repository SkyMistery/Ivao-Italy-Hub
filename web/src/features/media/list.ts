import { col, type ColumnSpec } from '../../shared/list';

import type { MediaListDto } from './queries';

/**
 * The columns of the media library. Declarations only, like every other list of the hub: `DataList`
 * decides how a translated value, a number or a badge is drawn (design M0 §7.5).
 *
 * `sortable` says what the server declared in `CrudOptions.Sortable`; a column that claims more is
 * answered with 400.
 */
export const mediaColumns: readonly ColumnSpec<MediaListDto>[] = [
  col.text('fileName', { sortable: true }),
  col.localized('alt'),
  col.text('contentType'),
  col.number('byteSize', { sortable: true }),
  col.badge('visibility', 'media'),
  col.text('category', { sortable: true }),
  col.date('createdAt', { sortable: true }),
];
