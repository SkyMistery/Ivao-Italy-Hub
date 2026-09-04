import { col, type ColumnSpec } from '../../../shared/list';

import type { GrantListDto } from './queries';

/**
 * The columns of the grants list. Declarations only, like every other list of the hub: `DataList`
 * decides how a date or a badge is drawn (design M0 §7.5).
 *
 * `suspendedAt` earns a column of its own because it means something an administrator has to be
 * able to see at a glance: the roster sync stopped seeing that VID as staff, so the grant is asleep
 * rather than gone, and it will wake up on its own if the position comes back.
 */
export const grantColumns: readonly ColumnSpec<GrantListDto>[] = [
  col.number('vid', { sortable: true }),
  col.text('value', { sortable: true }),
  col.department('department'),
  col.badge('effect', 'grants', { sortable: true }),
  col.date('expiresAt'),
  col.date('suspendedAt'),
  col.text('reason'),
  col.date('updatedAt', { sortable: true }),
];
