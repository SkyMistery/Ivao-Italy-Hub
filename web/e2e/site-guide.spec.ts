import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from '@playwright/test';

import { stubThePublishedPage } from './fixtures';

/**
 * The guide «How to make a page» (design `13-design-struttura-del-sito.md` §5.2) drawn from its **seed**, in both
 * languages: the file `seed/content-pages/how-to-make-a-page.json` with every `{ "$t": … }` resolved from
 * `locales/{en,it}/seed.json`, the way `ContentSeeder` resolves it. What the server does with the seed (the row, its
 * visibility, the setting it is chosen for) is `ContentSettingsTests`'s; here, that what it says is drawn — the seven
 * headings, the Markdown as lists and links — and that the links lead into the back office.
 */

const root = resolve(process.cwd(), '..');
const SLUG = 'how-to-make-a-page';

const read = (path: string): unknown => JSON.parse(readFileSync(`${root}/${path}`, 'utf8'));

const seeds = { en: read('locales/en/seed.json'), it: read('locales/it/seed.json') } as const;

function word(tree: unknown, key: string): string {
  const found = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], tree);
  if (typeof found !== 'string') {
    throw new Error(`No text at ${key}`);
  }
  return found;
}

/** Every `{ "$t": key }` of the seed becomes `{ en, it }`, as the seeder writes it in the languages of the division. */
function resolved(node: unknown): unknown {
  if (Array.isArray(node)) {
    return node.map(resolved);
  }
  if (node !== null && typeof node === 'object') {
    const entries = Object.entries(node);
    const [first] = entries;
    if (entries.length === 1 && first !== undefined && first[0] === '$t' && typeof first[1] === 'string') {
      return { en: word(seeds.en, first[1]), it: word(seeds.it, first[1]) };
    }
    return Object.fromEntries(entries.map(([key, value]) => [key, resolved(value)]));
  }
  return node;
}

const seed = read(`seed/content-pages/${SLUG}.json`) as {
  body: unknown;
  visibility: string;
  setting: string;
};
const body = resolved(seed.body);

const TOPICS = ['where', 'address', 'review', 'sections', 'blocks', 'media', 'claude'] as const;

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the seed is the staff guide the settings choose', () => {
  expect(seed.visibility).toBe('Staff');
  expect(seed.setting).toBe('content.guidePage');
});

for (const locale of ['en', 'it'] as const) {
  test(`the guide reads in ${locale}, its seven parts and the links into the back office`, async ({
    page,
    context,
  }) => {
    await context.addCookies([{ name: 'hub.lang', value: locale, domain: '127.0.0.1', path: '/' }]);
    await stubThePublishedPage(page, SLUG, body);

    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(`/${SLUG}`);

    await expect(
      page.getByRole('heading', { name: word(seeds[locale], 'seed.pages.howTo.hero.heading') }),
    ).toBeVisible();

    for (const topic of TOPICS) {
      await expect(
        page.getByRole('heading', {
          name: word(seeds[locale], `seed.pages.howTo.body.${topic}.heading`),
          level: 2,
        }),
      ).toBeVisible();
    }

    // The Markdown is drawn, not shown: lists are lists, and the addresses are links.
    await expect(page.locator('main li').first()).toBeVisible();
    for (const address of ['/staff/content', '/staff/media', '/staff/admin/ui-kit']) {
      await expect(page.locator(`main a[href="${address}"]`)).toHaveCount(1);
    }
    await expect(page.locator('main')).not.toContainText('**');

    await page.screenshot({ path: test.info().outputPath(`site-guide-${locale}.png`), fullPage: true });
  });
}
