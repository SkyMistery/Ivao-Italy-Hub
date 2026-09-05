import { z } from 'zod';

import { LAYOUTS, BACKGROUNDS, PADDINGS, WIDTHS } from '../../blocks';
import { DEPARTMENTS } from '../../shared/api/department';
import { localized, localizedObject } from '../../shared/forms';

/**
 * The metadata of a content row, as a zod schema mirroring `ContentWriteDto`. Types and what is
 * required, and nothing else: every real rule — a slug that is an address, a title in every
 * language before publishing — belongs to the server and is answered by it (design M0 §7.5).
 *
 * The body is not here. It is edited by the section tree, not by a field, and it travels with the
 * same payload; `mutations.ts` is where the two are put back together.
 *
 * `seo` **is** here, since G2: it is a translated *object* — a title, a description and a picture
 * per language — and the generator learned to draw one rather than being handed a JSON box. What
 * goes in it is the minimum a page needs to be shared: design M1 §9.2 decided that, and decided it
 * once.
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
  seo: localizedObject({
    title: z.string().optional(),
    description: z.string().optional().meta({ multiline: true }),
    // The first real client of the media selector: a picture is chosen out of the library, never
    // by typing a number (design M1 §1.5).
    ogImageMediaId: z.number().optional().meta({ media: true }),
  }).optional(),
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
