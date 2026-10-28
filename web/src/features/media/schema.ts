import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

import type { MediaCategory } from './queries';

/**
 * What a file is for (#359), in the order the select offers them: a closed set and never free text,
 * so the picker of a block that shows logos can offer the logos. The server holds the same set as
 * `MediaCategory`; `satisfies` is what tells the two apart the day one of them changes.
 */
export const MEDIA_CATEGORIES = [
  'Image',
  'Icon',
  'Logo',
  'Banner',
  'Document',
] as const satisfies readonly NonNullable<MediaCategory>[];

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
  // Optional only for a file nobody has described since the category was free text: an upload is
  // given the one its bytes say.
  category: z.enum(MEDIA_CATEGORIES).optional(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type MediaFormValues = z.output<typeof mediaSchema>;
