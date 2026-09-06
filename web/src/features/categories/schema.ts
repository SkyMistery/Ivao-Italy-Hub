import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

/**
 * The form of a category, as a zod schema mirroring `CategoryWriteDto`. Types and what is required,
 * and nothing else: the shape of a key and a label in every language are the server's rules and it
 * answers with them (design M0 §7.5).
 */
export const categorySchema = z.object({
  // Fixed by the route, exactly as it is on a link or a file: the list is `/staff/<dept>/categories`.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  kind: z.enum(['News', 'Document']),
  // The stable name a content row stores. It is not the name anybody reads — that is the label —
  // and changing it leaves the rows already filed under the old one where they are.
  key: z.string(),
  label: localized(),
  sort: z.number().int(),
  isActive: z.boolean(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type CategoryFormValues = z.output<typeof categorySchema>;
