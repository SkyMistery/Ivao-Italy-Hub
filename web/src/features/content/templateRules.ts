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

/**
 * The permission that may change any of this, held on the template's own department. It lives here
 * because two screens need the same name: the one that checks it, and the one that tells a
 * coordinator why a section will not move (design M1 §9.1).
 */
export const MANAGE_TEMPLATES = 'Content.ManageTemplates';

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

/**
 * The rule the template gives a section's `key`, or the free one when it says nothing. Of the section
 * on its own: a row inside another reads `ruleInside`, or `ruleOfSection` by its id.
 */
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

/**
 * The rule of a row, from its own and from the one of the section it is a row of (issue #460). A locked
 * section is fixed structure, "si compila, non si ristruttura" (design M1 §1.4): its rows are part of
 * that structure, so they are locked with it whatever key they carry — or lack, since a row with no key
 * has no rule of its own to read. Only the lock reaches down; `required` and `allowedBlocks` stay the
 * row's own, and a locked row offers neither a removal nor a block to add anyway.
 */
export function ruleInside(around: SectionRule, own: SectionRule): SectionRule {
  return around.locked && !own.locked ? { ...own, locked: true } : own;
}

/** The rule of the section with this id, with the lock of every section around it; the free one if it is absent. */
export function ruleOfSection(
  rules: ReadonlyMap<string, SectionRule>,
  body: Body,
  id: string | null | undefined,
): SectionRule {
  const walk = (sections: Body['sections'], around: SectionRule): SectionRule | undefined => {
    for (const section of sections) {
      const rule = ruleInside(around, ruleFor(rules, section.key));
      const found = section.id === id ? rule : walk(section.sections, rule);
      if (found !== undefined) {
        return found;
      }
    }

    return undefined;
  };

  const free = ruleFor(rules, null);
  return id === null || id === undefined ? free : (walk(body.sections, free) ?? free);
}
