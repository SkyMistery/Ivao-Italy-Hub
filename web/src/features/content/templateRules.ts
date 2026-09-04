import { allSections, type Body } from '../../blocks';

/**
 * What the template a page was made from still says about it (design M0 §5.6, §7.7).
 *
 * The page itself does not carry these: `required`, `locked` and `allowedBlocks` are refused on a
 * row that is not a template, and the server strips them when it copies one — otherwise a page
 * could lift its own restrictions by editing its own body. So the editor reads them from the
 * template, matching section by `key`, which is the one thing a copy keeps.
 *
 * A template that has since changed is not propagated: a section the template added is simply not
 * on this page, and a rule for a `key` the page no longer has is not applied to anything. The
 * public keeps seeing the published version either way (CLAUDE.md §2).
 */
export interface SectionRule {
  /** The page may not delete this section. */
  readonly required: boolean;
  /** The page may edit the properties of its blocks, and nothing else about it. */
  readonly locked: boolean;
  /** Which block types may be added here; null means any. */
  readonly allowedBlocks: readonly string[] | null;
}

export const NO_RULES: ReadonlyMap<string, SectionRule> = new Map();

export function templateRules(template: Body | null | undefined): ReadonlyMap<string, SectionRule> {
  if (!template) {
    return NO_RULES;
  }

  const rules = new Map<string, SectionRule>();

  for (const section of allSections(template)) {
    if (typeof section.key !== 'string' || section.key.length === 0) {
      // A section with no key cannot be matched to the copy of it, so it imposes nothing. The
      // seeded templates all carry one; a hand written template that does not is simply free.
      continue;
    }

    rules.set(section.key, {
      required: section.required === true,
      locked: section.locked === true,
      allowedBlocks: section.allowedBlocks ?? null,
    });
  }

  return rules;
}

/** The rule for one section of the page, or the free one when the template says nothing. */
export function ruleFor(
  rules: ReadonlyMap<string, SectionRule>,
  key: string | null | undefined,
): SectionRule {
  return (
    (key === null || key === undefined ? undefined : rules.get(key)) ?? {
      required: false,
      locked: false,
      allowedBlocks: null,
    }
  );
}
