import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, test } from 'vitest';

import { allSections, readBody, type Body } from '../../blocks';

import { templateDiff } from './templateDiff';

/**
 * Every page this hub is seeded with satisfies the template it is seeded from.
 *
 * This exists because four of them did not, and nobody could see it. The seeds are written by hand
 * in two directories that nothing compared: `section-page` locked a section called `hero` around a
 * heading and a text block, while every page made from it carries a `hero` block; the about
 * template allowed four kinds of block in its team section and the page put the staff directory
 * there; its contact section was locked around a callout and the page added a button; and the home
 * page gave its own extra section a `key`, which made the editor offer to **delete** it as "no
 * longer in the template".
 *
 * All four were found on the day somebody opened `/about` in the editor for real (G12), because
 * G11 had just built the panel that says so. A comparison nothing runs is a comparison nobody makes:
 * this is the same `templateDiff` the editor uses, pointed at the files instead of at a row.
 */

// The repository root. `process.cwd()` is `web/` when Vitest runs, and `import.meta.url` is not a
// file URL under jsdom — which is a thing this file found out the hard way.
const root = resolve(process.cwd(), '..');

/**
 * A seed body, read the way the application reads one. The `{ "$t": "key" }` a seed writes where a
 * translated string goes is a `Record<string, string>` like any other, so the envelope parses it —
 * which is what makes this test possible at all.
 */
function seedBody(directory: string, file: string): Body {
  const seed = JSON.parse(readFileSync(`${root}/seed/${directory}/${file}`, 'utf8')) as {
    body?: unknown;
  };

  return readBody(seed.body);
}

const listing = (directory: string) =>
  readdirSync(`${root}/seed/${directory}`).filter((file) => file.endsWith('.json'));

const templates = new Map(
  listing('content-templates').map((file) => [
    (JSON.parse(readFileSync(`${root}/seed/content-templates/${file}`, 'utf8')) as { slug: string }).slug,
    seedBody('content-templates', file),
  ]),
);

interface PageSeed {
  readonly slug: string;
  readonly template?: string;
  readonly body?: unknown;
}

const pages = listing('content-pages')
  .map(
    (file) =>
      [file, JSON.parse(readFileSync(`${root}/seed/content-pages/${file}`, 'utf8')) as PageSeed] as const,
  )
  // The dashboard seed is a row per department made straight from its template, with no body of
  // its own: there is nothing to compare it against.
  .filter(([, seed]) => seed.body !== undefined);

describe('the seeded pages and the seeded templates', () => {
  test('there are templates and pages to compare, and each page names a template that exists', () => {
    // The guard that stops every assertion below from passing because a path was wrong.
    expect(templates.size).toBeGreaterThan(0);
    expect(pages.length).toBeGreaterThan(0);

    for (const [file, seed] of pages) {
      expect(templates.has(seed.template ?? ''), `${file} names template "${seed.template}"`).toBe(true);
    }

    for (const [slug, body] of templates) {
      expect(allSections(body).length, `template ${slug} parsed to nothing`).toBeGreaterThan(0);
    }
  });

  test.each(pages.map(([file]) => file))('%s satisfies its template', (file) => {
    const seed = pages.find(([name]) => name === file)![1];
    const page = seedBody('content-pages', file);
    const template = templates.get(seed.template ?? '')!;

    // Read out in full rather than counted: a failure here should say which section and why,
    // because that is the whole of what somebody has to fix.
    expect(
      templateDiff(page, template).map((difference) =>
        difference.kind === 'changed'
          ? `${difference.key}: ${difference.reasons.join(', ')}`
          : `${difference.key}: ${difference.kind}`,
      ),
    ).toEqual([]);
  });

  test('a section a page adds for itself carries no key', () => {
    // A key is the handle back to a section of the template. A page-only section that claims one
    // is reported as "no longer in the template", and the action offered for that is **remove** —
    // so this mistake in a seed is an editor offering to delete a page's own content.
    for (const [file, seed] of pages) {
      const keys = new Set(
        allSections(seedBody('content-pages', file))
          .map((section) => section.key)
          .filter((key): key is string => typeof key === 'string' && key.length > 0),
      );

      const templateKeys = new Set(
        allSections(templates.get(seed.template ?? '')!)
          .map((section) => section.key)
          .filter((key): key is string => typeof key === 'string' && key.length > 0),
      );

      expect(
        [...keys].filter((key) => !templateKeys.has(key)),
        file,
      ).toEqual([]);
    }
  });
});
