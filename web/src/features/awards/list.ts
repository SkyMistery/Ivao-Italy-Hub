import { col, type ColumnSpec } from '../../shared/list';

import type { AwardAssignmentListDto, AwardListDto, AwardSignalListDto } from './queries';

/**
 * The columns of the three award lists. Declarations only, like every list of the hub (design M0
 * §7.5); `sortable` says what the server declared in `CrudOptions.Sortable`.
 */
export const awardColumns: readonly ColumnSpec<AwardListDto>[] = [
  col.media('imageMediaId'),
  col.localized('name'),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];

export const awardAssignmentColumns: readonly ColumnSpec<AwardAssignmentListDto>[] = [
  col.localized('awardName'),
  col.number('vid', { sortable: true }),
  col.text('reason'),
  col.number('createdBy'),
  col.date('createdAt', { sortable: true }),
];

export const awardSignalColumns: readonly ColumnSpec<AwardSignalListDto>[] = [
  col.number('vid', { sortable: true }),
  col.text('reason'),
  col.localized('awardName'),
  col.text('sourceModule'),
  col.badge('status', 'awardSignals'),
  col.date('createdAt', { sortable: true }),
];
