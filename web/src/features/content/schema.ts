import { z } from 'zod';

import { LAYOUTS, BACKGROUNDS, PADDINGS, WIDTHS } from '../../blocks';
import { DEPARTMENTS } from '../../shared/api/department';
import { localized, localizedObject, type ChoiceOption } from '../../shared/forms';

import type { ContentKind } from './queries';

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
 *
 * It is a **function** since G5, for two reasons that are the same reason. The `kind` decides which
 * three of the five kind-specific columns a form shows — a news item has a cover and a pin, a
 * document an order and a file — and the categories of the department are a set only the server
 * knows. Neither is a second form: it is one schema that says what this row is (design M1 §3.2).
 */
export function contentMetadataSchema(kind: ContentKind, categories: readonly ChoiceOption[] = []) {
  const common = {
    // Fixed by the list this row was opened from, exactly as the department is: `/staff/x/news`
    // edits news. A select here would let a page become a document with the fields of a page still
    // on screen, which is a form that lies about what it is editing.
    kind: z.enum(['Page', 'News', 'Document', 'Dashboard']).meta({ hidden: true }),
    slug: z.string(),
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
  };

  // The shelf this row is filed under. A key stored on the row and a translated name shown in the
  // select: the vocabulary is rows a coordinator writes, so neither a `z.enum` nor an i18n key
  // could carry it (design M1 §3.4).
  const category = { category: z.string().optional().meta({ choices: categories }) };

  if (kind === 'News') {
    return z.object({
      ...common,
      ...category,
      coverMediaId: z.number().int().optional().meta({ media: true }),
      pinned: z.boolean(),
    });
  }

  if (kind === 'Document') {
    return z.object({
      ...common,
      ...category,
      // A document with a file is a card with a download; one without is read in the browser like
      // any other page (design M1 §3.3).
      fileMediaId: z.number().int().optional().meta({ media: true }),
      sort: z.number().int(),
    });
  }

  return z.object(common);
}

/**
 * The values every content form carries, whichever kind it is. The kind-specific three are optional
 * here because a page has none of them — the schema above is what decides which are drawn, and the
 * payload builder is what decides what a kind that has none of them sends.
 */
export type ContentFormValues = z.output<ReturnType<typeof contentMetadataSchema>> & {
  category?: string;
  coverMediaId?: number;
  pinned?: boolean;
  fileMediaId?: number;
  sort?: number;
};

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
  // Only read when the background is `image`, and chosen from the library like every other file.
  // Left here rather than hidden behind the choice: the generator draws a schema, and a field that
  // appears and disappears with the value of another one would be the first rule of its kind.
  mediaId: z.number().int().optional().meta({ media: true }),
  padding: z.enum(PADDINGS),
  width: z.enum(WIDTHS),
});

export type SectionFormValues = z.output<typeof sectionSettingsSchema>;
