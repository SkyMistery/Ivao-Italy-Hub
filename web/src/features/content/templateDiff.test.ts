import { describe, expect, test } from 'vitest';

import { allSections, type Body, type BlockEnvelope, type SectionEnvelope } from '../../blocks';

import {
  applyDifference,
  templateDiff,
  type AlignableDifference,
  type TemplateDifference,
} from './templateDiff';

/**
 * The differences between a page and the template it was made from (design M1 §9.1). The acceptance
 * criterion of G11 is `TemplateDiffDetectsAddedRemovedAndChanged`, which is the first test here.
 *
 * What is worth testing is not that a comparison compares. It is the four ways of getting this
 * wrong: reporting a nested section twice, reporting a keyless section at all, reporting "changed"
 * for a page that is perfectly fine, and — the one the design is loudest about — applying more than
 * the single difference somebody asked for.
 */

const block = (id: string, type: string): BlockEnvelope => ({
  id,
  type,
  version: 1,
  props: {},
  renderMode: null,
  frozen: null,
  column: 0,
});

const section = (key: string | null, extra: Partial<SectionEnvelope> = {}): SectionEnvelope => ({
  id: key === null ? 's_free' : `s_${key}`,
  ...(key === null ? {} : { key }),
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: [],
  sections: [],
  ...extra,
});

const body = (...sections: SectionEnvelope[]): Body => ({ schemaVersion: 1, sections });

/** What was reported, short enough to read in an assertion. */
const summary = (differences: readonly TemplateDifference[]) =>
  differences.map((difference) =>
    difference.kind === 'changed'
      ? `changed:${difference.key}:${difference.reasons.join('+')}`
      : `${difference.kind}:${difference.key}`,
  );

const keys = (page: Body) => allSections(page).map((found) => found.key ?? '(none)');

/** Narrows to what an "align" may act on, and fails loudly rather than silently when it cannot. */
const alignable = (difference: TemplateDifference | undefined): AlignableDifference => {
  if (difference === undefined || difference.kind === 'changed') {
    throw new Error(`not a difference an align can apply: ${difference?.kind ?? 'none'}`);
  }

  return difference;
};

describe('templateDiff', () => {
  test('detects a section added to the template, one removed from it, and one that no longer fits', () => {
    const template = body(section('hero'), section('news', { allowedBlocks: ['text'] }), section('contacts'));

    const page = body(
      section('hero'),
      section('news', { blocks: [block('b1', 'text'), block('b2', 'gallery')] }),
      section('archive'),
    );

    expect(summary(templateDiff(page, template))).toEqual([
      'changed:news:blocks',
      'added:contacts',
      'removed:archive',
    ]);
  });

  test('a page that still fits its template has nothing to report', () => {
    const template = body(section('hero'), section('news', { allowedBlocks: ['text', 'heading'] }));
    const page = body(section('hero'), section('news', { blocks: [block('b1', 'heading')] }));

    expect(templateDiff(page, template)).toEqual([]);
  });

  test('a locked section whose copy no longer holds the same blocks is reported', () => {
    // The only way this happens is the template moving after the copy was made: the editor refuses
    // to restructure a locked section, which is what makes the divergence mean something.
    const template = body(
      section('legal', { locked: true, blocks: [block('t1', 'text'), block('t2', 'callout')] }),
    );
    const page = body(section('legal', { blocks: [block('b1', 'text')] }));

    expect(summary(templateDiff(page, template))).toEqual(['changed:legal:structure']);
  });

  test('a locked section that still matches is not reported', () => {
    const template = body(section('legal', { locked: true, blocks: [block('t1', 'text')] }));
    const page = body(section('legal', { blocks: [block('b1', 'text')] }));

    expect(templateDiff(page, template)).toEqual([]);
  });

  test('a section with no key is compared to nothing, in either direction', () => {
    // A section somebody added by hand carries no key, so the template says nothing about it — and
    // a keyless section of the template cannot be matched to its copy, so it asks for nothing.
    const template = body(section('hero'), section(null));
    const page = body(section('hero'), section(null, { id: 's_mine' }));

    expect(templateDiff(page, template)).toEqual([]);
  });

  test('a nested section arrives with its parent rather than being reported twice', () => {
    const template = body(section('hero', { sections: [section('hero.notice')] }));
    const page = body();

    // Only the parent: adding it brings the child, and offering both would be one difference
    // answered twice.
    expect(summary(templateDiff(page, template))).toEqual(['added:hero']);

    const aligned = applyDifference(page, template, {
      kind: 'added',
      key: 'hero',
      section: template.sections[0]!,
    });
    expect(keys(aligned)).toEqual(['hero', 'hero.notice']);
    expect(templateDiff(aligned, template)).toEqual([]);
  });

  test('a nested section of a parent the page has is reported on its own', () => {
    const template = body(section('hero', { sections: [section('hero.notice')] }));
    const page = body(section('hero'));

    expect(summary(templateDiff(page, template))).toEqual(['added:hero.notice']);
  });

  test('no template is no differences at all, rather than everything removed', () => {
    // A page whose template the reader may not open, or one made from no template: the editor falls
    // back to no rules, and it must not then offer to delete the whole page.
    expect(templateDiff(body(section('hero')), null)).toEqual([]);
  });
});

describe('applyDifference', () => {
  const template = body(
    section('hero'),
    section('news', { locked: true, required: true, allowedBlocks: ['text'], blocks: [block('t1', 'text')] }),
    section('contacts'),
  );

  test('applies one difference and leaves the others where they were', () => {
    const page = body(section('hero'), section('archive'));

    const differences = templateDiff(page, template);
    expect(summary(differences)).toEqual(['added:news', 'added:contacts', 'removed:archive']);

    const aligned = applyDifference(page, template, alignable(differences[0]));

    // The one that was asked for, and only that one: `contacts` is still missing and `archive` is
    // still here. This is the assertion design §9.1 is written for.
    expect(keys(aligned)).toEqual(['hero', 'news', 'archive']);
    expect(summary(templateDiff(aligned, template))).toEqual(['added:contacts', 'removed:archive']);
  });

  test('the added section lands where the template holds it, not at the end', () => {
    const page = body(section('hero'), section('contacts'));
    const differences = templateDiff(page, template);

    const aligned = applyDifference(page, template, alignable(differences[0]));
    expect(keys(aligned)).toEqual(['hero', 'news', 'contacts']);
  });

  test('the added section is a copy, and leaves the template-only keys behind', () => {
    const page = body(section('hero'), section('contacts'));
    const aligned = applyDifference(page, template, alignable(templateDiff(page, template)[0]));

    const added = allSections(aligned).find((found) => found.key === 'news')!;
    const original = template.sections[1]!;

    // A page that carried `locked` or `allowedBlocks` could lift its own restrictions, which is
    // why the server strips them when it copies a template (`TemplateCopy.Reidentify`) and why
    // the envelope validator refuses them on a row that is not one.
    expect(added.locked).toBeUndefined();
    expect(added.required).toBeUndefined();
    expect(added.allowedBlocks).toBeUndefined();

    // And nothing in it still answers to the template's identifiers.
    expect(added.id).not.toBe(original.id);
    expect(added.blocks.map((carried) => carried.id)).not.toEqual(original.blocks.map((one) => one.id));
    expect(added.blocks.map((carried) => carried.type)).toEqual(['text']);
  });

  test('removing takes that section and nothing else', () => {
    const page = body(section('hero'), section('archive'), section('news'), section('contacts'));
    const removal = templateDiff(page, template).find((difference) => difference.kind === 'removed');

    const aligned = applyDifference(page, template, alignable(removal));
    expect(keys(aligned)).toEqual(['hero', 'news', 'contacts']);
  });
});
