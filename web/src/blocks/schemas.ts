import { z } from 'zod';

import { DEPARTMENTS } from '../shared/api/department';
import { localized } from '../shared/forms';

/**
 * What each block of the core holds, as zod. This is the only description of a block's properties
 * anywhere: the backend keeps `props` opaque (CLAUDE.md §2) and the same schema is what
 * `SchemaForm` turns into the property form in the editor.
 *
 * Separate from the components so that each file exports one kind of thing, which is what keeps
 * fast refresh working on the components.
 */

/** The four tones a callout can take. */
export const CALLOUT_TONES = ['info', 'success', 'warning', 'danger'] as const;

/** How far down the outline a heading sits. Four is as deep as a page of this hub ever goes. */
export const HEADING_LEVELS = [1, 2, 3, 4];

export const headingSchema = z.object({
  // A number with choices rather than a `z.enum`: every string inside `props` is extracted as the
  // text of the page for the search index, and "2" is not text (design M0 §5.3). The generator
  // still draws a select, so nobody can type a level that does not exist.
  level: z.number().int().meta({ choices: HEADING_LEVELS }),
  text: localized(),
});

export const textSchema = z.object({
  markdown: localized().meta({ multiline: true }),
});

export const calloutSchema = z.object({
  tone: z.enum(CALLOUT_TONES),
  title: localized(),
  text: localized().meta({ multiline: true }),
});

export const ctaSchema = z.object({
  label: localized(),
  href: z.string(),
});

export const linkListSchema = z.object({
  category: z.string(),
  // A choice and not free text: a department typed by hand is a department that can be misspelled,
  // and the server answers a name it does not know with nothing at all rather than with everything.
  department: z.enum(DEPARTMENTS).optional(),
  // The default lives on the field, so a new block starts at ten and nothing else has to know it.
  limit: z.number().int().default(10),
});
