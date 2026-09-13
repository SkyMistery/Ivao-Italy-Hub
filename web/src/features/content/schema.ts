import { z } from 'zod';

import { PADDINGS, WIDTHS } from '../../blocks';
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
/**
 * What the form of a document offers beyond its own row (G14): the published documents that may
 * have replaced this one.
 */
export interface DocumentChoices {
  successors: readonly ChoiceOption[];
}

const NO_DOCUMENT_CHOICES: DocumentChoices = { successors: [] };

/**
 * Where a page may be put (note 2026-09-13-contenuti-centralizzati, 3.7): the pages of the site it
 * can sit under, already labelled with their address, and whether this person may leave it at the
 * top of the site — which is what `Content.Approve` is for. Without it the field is required: a page
 * of a department sits under a page of the site.
 */
export interface PageChoices {
  parents: readonly ChoiceOption[];
  mayBeAtTheTop: boolean;
}

const NO_PAGE_CHOICES: PageChoices = { parents: [], mayBeAtTheTop: true };

export function contentMetadataSchema(
  kind: ContentKind,
  categories: readonly ChoiceOption[] = [],
  document: DocumentChoices = NO_DOCUMENT_CHOICES,
  page: PageChoices = NO_PAGE_CHOICES,
) {
  const common = {
    // Fixed by the list this row was opened from, exactly as the department is: `/staff/x/news`
    // edits news. A select here would let a page become a document with the fields of a page still
    // on screen, which is a form that lies about what it is editing.
    kind: z.enum(['Page', 'News', 'Document', 'Dashboard']).meta({ hidden: true }),
    // Proposed from the title while nobody writes it by hand (design M0 §7.5, asked for after the
    // demo of M1). An existing row never moves: it already carries an address, so the proposal
    // stands aside from the first render — and an address outlives the page that has it.
    slug: z.string().meta({ slugFrom: 'title' }),
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

  if (kind === 'Page') {
    return z.object({
      ...common,
      // Carried as text for the reason a category is: a select whose labels are not its values is a
      // text field with choices. Optional — "at the top of the site" — only for whoever may put a
      // page there; the server refuses the top to anybody else anyway.
      parentId: page.mayBeAtTheTop
        ? z.string().optional().meta({ choices: page.parents })
        : z.string().min(1).meta({ choices: page.parents }),
    });
  }

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
      // ---- the life of a document (G14) ----
      // When it comes into force and when it is due for a look. All optional: a guide filed among
      // the documents may have none of them.
      effectiveOn: z.string().optional().meta({ date: true }),
      reviewOn: z.string().optional().meta({ date: true }),
      // Archived and superseded are a date and a successor, not a status (implementation plan,
      // G14): a retired document stays published so that an old link finds the notice and the way
      // on. The successor is carried as text for the reason a category is: a select whose labels
      // are not its values is a text field with choices.
      retiredAt: z.string().optional().meta({ date: true }),
      supersededById: z.string().optional().meta({ choices: document.successors }),
      showFooter: z.boolean(),
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
  // The page this one sits under, as the id in text; empty at the top of the site. Pages only.
  parentId?: string;
  category?: string;
  coverMediaId?: number;
  pinned?: boolean;
  fileMediaId?: number;
  sort?: number;
  // The life of a document (G14). All optional here for the reason the five above are; a kind that
  // has none of them sends null.
  effectiveOn?: string;
  reviewOn?: string;
  retiredAt?: string;
  supersededById?: string;
  showFooter?: boolean;
};

/**
 * What a section decides about itself: how wide it is, how much air it has, what sits behind it,
 * and whether its blocks follow one another or stand in columns.
 *
 * `title` is the name in the editor's tree and is never drawn on the page — what a visitor reads
 * is a `heading` block, which is a block they can move, translate and delete.
 *
 * It is a **function** since G11a, for the same reason `contentMetadataSchema` is one: four of
 * these fields exist only on a template. `key`, `required`, `locked` and `allowedBlocks` are what a
 * template imposes on the pages made from it (design M1 §9.1), and the server refuses three of them
 * outright on a row that is not one — a page carrying them could lift its own restrictions.
 */
export function sectionSettingsSchema(
  /**
   * Null on a page. On a template: the block types of the registry, already labelled in the
   * language on screen, and whether this section still has no `key` — because a key is written
   * once and then only shown (decision `2026-09-07-scrivere-un-template.md`).
   */
  template: { blocks: readonly ChoiceOption[]; unnamed: boolean } | null,
) {
  // ⚠️ `layout` and `background` are **not** here, since 10 September 2026. They are the two things
  // about a section that are judged by eye rather than written, and they live in `SectionFrame` as
  // pictures applied at once — one place each, or a form holding a stale background would undo a
  // swatch the moment somebody pressed Apply.
  const common = {
    title: localized(),
    // Only read when the background is `image`, and chosen from the library like every other file.
    // Left here rather than hidden behind the choice: the generator draws a schema, and a field that
    // appears and disappears with the value of another one would be the first rule of its kind.
    mediaId: z.number().int().optional().meta({ media: true }),
    padding: z.enum(PADDINGS),
    width: z.enum(WIDTHS),
  };

  if (template === null) {
    return z.object(common);
  }

  const imposed = {
    required: z.boolean(),
    locked: z.boolean(),
    // Empty means "any", which is why it is optional rather than a list starting at nothing: a
    // section that allowed no block at all would be a section nobody could put anything in.
    allowedBlocks: z.array(z.string()).optional().meta({ multi: true, choices: template.blocks }),
  };

  if (!template.unnamed) {
    return z.object({ ...common, ...imposed });
  }

  // ⚠️ The handle a copy is matched back by, and the only one it keeps. Changing it on a template
  // that already has pages breaks that match in silence — the section of every page becomes
  // "removed from the template" and this one becomes "added", and nobody did anything wrong. So the
  // form offers it while it is empty and shows it afterwards. The server still takes anything from
  // a `PUT`: this is the form not letting somebody trip, not a rule of the domain.
  return z.object({ ...common, key: z.string().optional(), ...imposed });
}

/**
 * The values a section form carries, on a page or on a template. The four a template imposes are
 * optional here for the same reason the kind-specific three of a content row are: the schema above
 * decides which are drawn, and `SectionProperties` decides what a section that has none of them
 * writes back.
 */
export type SectionFormValues = z.output<ReturnType<typeof sectionSettingsSchema>> & {
  key?: string;
  required?: boolean;
  locked?: boolean;
  allowedBlocks?: string[];
};
