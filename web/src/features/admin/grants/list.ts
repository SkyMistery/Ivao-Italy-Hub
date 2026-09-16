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
  // Who: a member, or a department at some of its levels (M2).
  col.number('vid', { sortable: true }),
  col.department('positionDepartment'),
  col.list('positionLevels'),
  col.text('value', { sortable: true }),
  col.department('department'),
  // The single row a grant is about, when it is about one (M2, T3). Read only: the module that owns
  // those rows writes it, from a screen that knows which of them exist.
  col.text('resourceScope'),
  col.badge('effect', 'grants', { sortable: true }),
  col.date('expiresAt'),
  col.date('suspendedAt'),
  col.text('reason'),
  col.date('updatedAt', { sortable: true }),
];
