import { z } from 'zod';

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

export const headingSchema = z.object({
  // A number rather than a set of choices on purpose: every string inside `props` is what the
  // search index extracts as the text of the page, and "2" is not text (design M0 §5.3).
  level: z.number().int(),
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
  department: z.string(),
  limit: z.number().int(),
});
