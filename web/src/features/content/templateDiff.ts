import { newId, type Body, type SectionEnvelope } from '../../blocks';

import { removeSection } from './body';

/**
 * What the template says today that this page does not (design M1 §9.1).
 *
 * The rule this is built on is the one that must not move: **a template never rewrites a page by
 * itself**, and the public keeps reading the published version whatever the template does. What was
 * missing until now is that the editor never *said* anything — a section added to a template was
 * invisible to everybody working on the pages made from it.
 *
 * Sections are matched by `key`, the one thing a copy keeps: `required`, `locked` and
 * `allowedBlocks` are stripped when the server copies a template, so that a page cannot lift its own
 * restrictions (`TemplateCopy.Reidentify`). A section with no key imposes nothing and is compared to
 * nothing.
 *
 * ⚠️ The third state is not "the constraints changed". Nothing anywhere records what the constraints
 * were when the page was made — deliberately, since a page carrying them could edit them — so
 * "changed" cannot be computed. What *can* be answered honestly is **does this page still satisfy
 * what the template says today**, which is what `changed` means here, and it is why it carries the
 * reasons rather than a bare flag.
 */

/** Why a section that exists on both sides no longer satisfies its template. */
export type TemplateChange =
  /** It holds blocks of a type the template no longer allows. */
  | 'blocks'
  /** The template fixes this section, and the copy on this page is no longer the same one. */
  | 'structure';

export type TemplateDifference =
  | { readonly kind: 'added'; readonly key: string; readonly section: SectionEnvelope }
  | { readonly kind: 'removed'; readonly key: string; readonly id: string }
  | {
      readonly kind: 'changed';
      readonly key: string;
      readonly id: string;
      readonly reasons: readonly TemplateChange[];
    };

/**
 * The two differences an "align" may act on. `changed` is deliberately not one of them: aligning it
 * would mean throwing away blocks somebody wrote, and a button that deletes a person's work because
 * a template moved is precisely the button design §9.1 refuses.
 */
export type AlignableDifference = Extract<TemplateDifference, { kind: 'added' | 'removed' }>;

interface Placed {
  readonly section: SectionEnvelope;
  /** The nearest ancestor that has a key, which is the only ancestor the other side can be matched to. */
  readonly parentKey: string | null;
}

/**
 * Every keyed section of a body, in reading order. A key that appears twice is a template written by
 * hand badly; the first one wins rather than the last, so that what the editor reports is stable.
 */
function keyed(body: Body | null | undefined): Map<string, Placed> {
  const found = new Map<string, Placed>();

  const walk = (sections: readonly SectionEnvelope[], parentKey: string | null): void => {
    for (const section of sections) {
      const key = typeof section.key === 'string' && section.key.length > 0 ? section.key : null;

      if (key !== null && !found.has(key)) {
        found.set(key, { section, parentKey });
      }

      walk(section.sections, key ?? parentKey);
    }
  };

  if (body) {
    walk(body.sections, null);
  }

  return found;
}

export function templateDiff(page: Body, template: Body | null | undefined): TemplateDifference[] {
  // No template is not an empty template. A page made from none, one whose template has not
  // arrived yet, and one whose template this reader may not open (design §9.4) all land here — and
  // comparing against nothing would report every section of the page as removed, which is an
  // editor offering to delete the whole page.
  if (!template) {
    return [];
  }

  const inTemplate = keyed(template);
  const inPage = keyed(page);
  const differences: TemplateDifference[] = [];

  for (const [key, { section, parentKey }] of inTemplate) {
    const here = inPage.get(key);

    if (here === undefined) {
      // A section whose parent is not on this page either arrives with its parent: reporting both
      // would be one difference offered twice, and applying the parent would answer the child.
      if (parentKey === null || inPage.has(parentKey)) {
        differences.push({ kind: 'added', key, section });
      }

      continue;
    }

    const reasons = reasonsFor(section, here.section);
    if (reasons.length > 0) {
      differences.push({ kind: 'changed', key, id: here.section.id, reasons });
    }
  }

  for (const [key, { section, parentKey }] of inPage) {
    // The same argument the other way round: removing the parent removes what is inside it.
    if (!inTemplate.has(key) && (parentKey === null || inTemplate.has(parentKey))) {
      differences.push({ kind: 'removed', key, id: section.id });
    }
  }

  return differences;
}

function reasonsFor(template: SectionEnvelope, page: SectionEnvelope): TemplateChange[] {
  const reasons: TemplateChange[] = [];
  const allowed = template.allowedBlocks ?? null;

  if (allowed !== null && page.blocks.some((block) => !allowed.includes(block.type))) {
    reasons.push('blocks');
  }

  // A locked section cannot be restructured from the editor, so a copy that no longer holds the
  // same blocks in the same order is a copy the template moved away from underneath.
  if (template.locked === true && blockTypes(template) !== blockTypes(page)) {
    reasons.push('structure');
  }

  return reasons;
}

const blockTypes = (section: SectionEnvelope): string => section.blocks.map((block) => block.type).join(' ');

/**
 * One difference, applied. One — never all of them: a button that rewrites a page in a single press
 * is a button somebody presses by mistake (design M1 §9.1).
 */
export function applyDifference(
  body: Body,
  template: Body | null | undefined,
  difference: AlignableDifference,
): Body {
  if (difference.kind === 'removed') {
    return removeSection(body, difference.id);
  }

  return insertFromTemplate(body, template, difference.key);
}

/**
 * The template's section, copied into the page where the template holds it: inside the same keyed
 * parent, after the last of its siblings that this page already has. It is the same copy the server
 * makes of a whole template — fresh identifiers, no capture carried over, and the keys only a
 * template may hold left behind (`TemplateCopy.Reidentify`).
 */
function insertFromTemplate(body: Body, template: Body | null | undefined, key: string): Body {
  const inTemplate = keyed(template);
  const inPage = keyed(body);

  const source = inTemplate.get(key);
  if (source === undefined || inPage.has(key)) {
    return body;
  }

  const parentKey = source.parentKey;
  if (parentKey !== null && !inPage.has(parentKey)) {
    return body;
  }

  let anchor: string | null = null;
  for (const [candidate, placed] of inTemplate) {
    if (candidate === key) {
      break;
    }

    if (placed.parentKey === parentKey && inPage.has(candidate)) {
      anchor = candidate;
    }
  }

  const copy = reidentify(source.section);

  const into = (sections: SectionEnvelope[]): SectionEnvelope[] => {
    const at = anchor === null ? 0 : sections.findIndex((section) => section.key === anchor) + 1;
    return sections.toSpliced(at, 0, copy);
  };

  if (parentKey === null) {
    return { ...body, sections: into(body.sections) };
  }

  const under = (sections: SectionEnvelope[]): SectionEnvelope[] =>
    sections.map((section) =>
      section.key === parentKey
        ? { ...section, sections: into(section.sections) }
        : { ...section, sections: under(section.sections) },
    );

  return { ...body, sections: under(body.sections) };
}

function reidentify(section: SectionEnvelope): SectionEnvelope {
  const copy: SectionEnvelope = {
    ...section,
    id: newId('s'),
    blocks: section.blocks.map((block) => ({
      ...block,
      id: newId('b'),
      props: structuredClone(block.props),
      frozen: null,
    })),
    sections: section.sections.map(reidentify),
  };

  // The three the server drops too, and for the same reason: a page holding them could lift its own
  // restrictions, which is what the envelope validator refuses on a row that is not a template.
  delete copy.required;
  delete copy.locked;
  delete copy.allowedBlocks;

  return copy;
}
