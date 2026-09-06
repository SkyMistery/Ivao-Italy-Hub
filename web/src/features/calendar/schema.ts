import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

/**
 * The form of a calendar entry, mirroring `CalendarWriteDto`. Types and what is required, and
 * nothing else: the real rules — a title in every language, an end that is not before the start —
 * are the server's and are answered by it (design M0 §7.5).
 *
 * ⚠️ There is no `rowVersion` because `cms_calendar_entries` has no concurrency token: the model of
 * M0 is not touched in this phase, so two members editing one entry end with the second save
 * winning. It is written here rather than left to be discovered.
 */
export const calendarSchema = z.object({
  // Fixed by the route, like the department of a link: the list is `/staff/<dept>/calendar`.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
  // Free text, because the staff writes it: `meeting`, `deadline`, whatever a department uses. The
  // kinds a module projects — `event`, `training`, `tour` — arrive with their rows and are not
  // typed here.
  kind: z.string(),
  title: localized(),
  description: localized().meta({ multiline: true }),
  // An instant, so the value travels as ISO in UTC and the field shows the division's own time
  // underneath it. A calendar is the reason that field exists (design M1 §1.6).
  startsAtUtc: z.string().meta({ datetime: true }),
  endsAtUtc: z.string().optional().meta({ datetime: true }),
  allDay: z.boolean(),
  url: z.string().optional(),
});

export type CalendarFormValues = z.output<typeof calendarSchema>;
