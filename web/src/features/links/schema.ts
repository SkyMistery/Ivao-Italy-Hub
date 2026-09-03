import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

/**
 * The form of a link, as a zod schema. It mirrors `LinkWriteDto` and carries nothing else: only
 * types and what is required, because every real rule — a title in every language, an address that
 * is absolute, a sort that is not negative — belongs to the server and is answered by it. A client
 * that repeated them would be a second set of rules to keep in step (design M0 §7.5).
 *
 * The test next to this file is what ties it to the contract, in both directions.
 *
 * Nullability is the one place the form and the DTO differ on purpose: a text box has an empty
 * string, not a null. `mutations.ts` turns an empty description or category back into the `null`
 * the API expects, in one place.
 */
export const linkSchema = z.object({
  // Fixed by the route: the list is `/staff/<dept>/links`, so the department is not a choice on
  // this form. It travels with the payload because the server needs it, and the server checks it
  // twice — on the row as stored and on the row as it would become.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
  title: localized(),
  url: z.string(),
  description: localized().meta({ localized: true, multiline: true }),
  category: z.string(),
  sort: z.number().int(),
  isActive: z.boolean(),
  // The version the form was loaded with. Sending back a stale one is how the server finds out
  // that somebody else saved first, and answers 409.
  rowVersion: z.string().meta({ hidden: true }),
});

export type LinkFormValues = z.output<typeof linkSchema>;
