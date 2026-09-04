import { z } from 'zod';

import { LAYOUTS, BACKGROUNDS, PADDINGS, WIDTHS } from '../../blocks';
import { DEPARTMENTS } from '../../shared/api/department';
import { localized } from '../../shared/forms';

/**
 * The metadata of a content row, as a zod schema mirroring `ContentWriteDto`. Types and what is
 * required, and nothing else: every real rule — a slug that is an address, a title in every
 * language before publishing — belongs to the server and is answered by it (design M0 §7.5).
 *
 * The body is not here. It is edited by the section tree, not by a field, and it travels with the
 * same payload; `mutations.ts` is where the two are put back together.
 *
 * `seo` is not here either, and that is a gap rather than a decision: it is a translated *object*
 * per language, and the form generator draws translated strings. Until a screen needs it, it is
 * sent back exactly as it was loaded.
 */
export const contentMetadataSchema = z.object({
  kind: z.enum(['Page', 'News', 'Document']),
  slug: z.string(),
  // Fixed by the route, like the department of a link: the list is `/staff/<dept>/content`.
  ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
  visibility: z.enum(['Public', 'Members', 'Staff', 'Department']),
  // Set once, by the template picker or by nobody. A checkbox here would let a page promote
  // itself into a template, which is a permission and not a field.
  isTemplate: z.boolean().meta({ hidden: true }),
  title: localized(),
  summary: localized().meta({ multiline: true }),
  rowVersion: z.string().meta({ hidden: true }),
});

export type ContentFormValues = z.output<typeof contentMetadataSchema>;

/**
 * What a section decides about itself: how wide it is, how much air it has, what sits behind it,
 * and whether its blocks follow one another or stand in columns.
 *
 * `title` is the name in the editor's tree and is never drawn on the page — what a visitor reads
 * is a `heading` block, which is a block they can move, translate and delete.
 */
export const sectionSettingsSchema = z.object({
  title: localized(),
  layout: z.enum(LAYOUTS),
  background: z.enum(BACKGROUNDS),
  padding: z.enum(PADDINGS),
  width: z.enum(WIDTHS),
});

export type SectionFormValues = z.output<typeof sectionSettingsSchema>;
