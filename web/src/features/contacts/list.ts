import { col, type ColumnSpec } from '../../shared/list';

import type { ContactListDto } from './queries';

/**
 * The columns of the contact queue. Declarations only: `DataList` decides how a date or a badge is
 * drawn, so the queue looks like every other list of the hub (design M0 §7.5).
 *
 * The body is not a column. A queue is for choosing which message to open, and a paragraph in a
 * table cell is a paragraph nobody reads.
 */
export const contactColumns: readonly ColumnSpec<ContactListDto>[] = [
  col.text('subject', { sortable: true }),
  col.badge('status', 'contacts', { sortable: true }),
  col.number('createdBy'),
  col.date('createdAt', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];
