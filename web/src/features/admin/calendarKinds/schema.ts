import { z } from 'zod';

import { localized } from '../../../shared/forms';
import { CALENDAR_KIND_COLOURS } from '../../../shared/ui';

/**
 * The form of one word of the division's calendar vocabulary, mirroring `CalendarKindWriteDto`.
 * Types and what is required, and nothing else: the real rules — the shape of a key, a label in
 * every language, a colour the design system has — are the server's and are answered by it
 * (design M0 §7.5).
 *
 * The colour is a `z.enum` and not a `choices` string, and that is the difference between this and
 * the permission of a grant: this set is known when the client is compiled, because it is the
 * palette of the badge. The server holds the same list, and an integration test posts a colour it
 * does not know to keep the two agreeing.
 */
export const calendarKindSchema = z.object({
  // The key an entry stores. Writable, like a category's: a key that could never be corrected
  // would mean a typo lives for ever — and proposed from the label, because it ends up in the
  // address of a filtered calendar and nobody should have to type it twice.
  key: z.string().meta({ slugFrom: 'label' }),
  label: localized(),
  colour: z.enum(CALENDAR_KIND_COLOURS),
  sort: z.number().int(),
  // Retires a word without deleting it: the entries written with it keep it, and nobody can file a
  // new one there.
  isActive: z.boolean(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type CalendarKindFormValues = z.output<typeof calendarKindSchema>;
