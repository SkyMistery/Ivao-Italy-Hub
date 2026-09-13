import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

/**
 * The metadata form of a file, as a zod schema mirroring `MediaWriteDto`. Nothing about the file
 * itself is here — its name on disk, its type, its size, its pixels are what the upload measured —
 * and no rule is repeated: the server validates and answers, and the client shows the answer
 * (design M0 §7.5).
 */
export const mediaSchema = z.object({
  // Fixed before the form opens, exactly as it is on a link: a file keeps the department it was
  // uploaded into.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
  // The alternative text is written once, next to the file, and inherited by every block that
  // shows it (design M1 §1.2). That is why it is required here and optional there.
  alt: localized().meta({ localized: true, multiline: true }),
  title: localized(),
  category: z.string(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type MediaFormValues = z.output<typeof mediaSchema>;
