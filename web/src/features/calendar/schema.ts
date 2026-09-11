import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized, type ChoiceOption } from '../../shared/forms';

/**
 * The form of a calendar entry, mirroring `CalendarWriteDto`. Types and what is required, and
 * nothing else: the real rules — a title in every language, an end that is not before the start —
 * are the server's and are answered by it (design M0 §7.5).
 *
 * ⚠️ There is no `rowVersion` because `cms_calendar_entries` has no concurrency token: the model of
 * M0 is not touched in this phase, so two members editing one entry end with the second save
 * winning. It is written here rather than left to be discovered.
 */
export function calendarSchema(kinds: readonly ChoiceOption[] = []) {
  return z.object({
    // Fixed by the route, like the department of a link: the list is `/staff/<dept>/calendar`.
    ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
    visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
    // ⚠️ Not free text since G13: the kinds are the division's, decided centrally and the same for
    // everybody (`decisions/2026-09-08-tipi-di-evento-di-divisione.md`). The list is a set only the
    // server knows, which is exactly the case `choices` on a string exists for — the same shape the
    // categories of a news item use, and the label is already resolved into the language on screen.
    kind: z.string().meta({ choices: kinds }),
    title: localized(),
    description: localized().meta({ multiline: true }),
    // An instant, so the value travels as ISO in UTC and the field shows the division's own time
    // underneath it. A calendar is the reason that field exists (design M1 §1.6).
    startsAtUtc: z.string().meta({ datetime: true }),
    endsAtUtc: z.string().optional().meta({ datetime: true }),
    allDay: z.boolean(),
    url: z.string().optional(),
  });
}

export type CalendarFormValues = z.output<ReturnType<typeof calendarSchema>>;
