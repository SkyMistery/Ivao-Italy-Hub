import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type BrowserContext, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, choose, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The "done when" of A5 (M3), through the real screens registered from the module manifest: the evaluation sheet of an ATC
 * rating is composed from the back office — two practical items and one of theory, each in both languages of the bench —,
 * narrowed to that rating with the two filters of the list, and read back in its order after a reload, and in Italian too.
 * Then one item moves to the top of the sheet, and the new order is what reads back.
 *
 * The rating is the first the server offers for the ATC ladder: the spec writes no rating of its own. The items go back in a
 * `finally`, and a run that stopped half way is cleaned up at the start: the bench survives between runs.
 */

const training = englishTraining();
const words = training.sheets;
const asTheClientDoes = { 'X-Requested-With': 'hub' };

/** In the title of every item of this spec, in both languages: what the search finds them by, whatever the reader's language. */
const marker = 'trn-bench';
const stamp = Date.now().toString(36);

const items = [
  {
    section: 'Practice',
    en: `${marker} ${stamp} Radio phraseology`,
    it: `${marker} ${stamp} Fraseologia radio`,
  },
  {
    section: 'Practice',
    en: `${marker} ${stamp} Traffic separation`,
    it: `${marker} ${stamp} Separazione del traffico`,
  },
  {
    section: 'Theory',
    en: `${marker} ${stamp} Airspace classes`,
    it: `${marker} ${stamp} Classi di spazio aereo`,
  },
] as const;

interface TrainingRating {
  readonly kind: string;
  readonly number: number;
  readonly shortName: string;
  readonly nameKey: string;
}

test('the sheet of an ATC rating is composed in both languages, and read back in its order', async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const ratings = await context.request.get('/api/training/ratings');
  expect(ratings.status(), await ratings.text()).toBe(200);
  const rating = ((await ratings.json()) as TrainingRating[]).find((candidate) => candidate.kind === 'Atc');
  expect(rating, 'an ATC rating with a practical training').toBeDefined();
  const option = ratingOption(rating!);

  await removeLeftovers(context);
  try {
    // ---------------------------------------------------------------- the list, narrowed to the sheet of the rating
    await page.goto('/staff/training/sheets');
    await expect(page.getByRole('heading', { name: words.title })).toBeVisible();
    await pick(page, 'sheet-items-kind', training.kinds.Atc);
    await pick(page, 'sheet-items-rating', option);
    await expect(page).toHaveURL(new RegExp(`kind=Atc.*rating=${String(rating!.number)}`));

    // ---------------------------------------------------------------- three items, each from the list it goes back to
    for (const item of items) {
      await page.getByRole('link', { name: words.create }).first().click();
      await expect(page.getByRole('heading', { name: words.create })).toBeVisible();

      // A new item starts on the sheet the list was narrowed to.
      await expect(page.getByRole('combobox', { name: words.fields.rating })).toHaveText(option);
      await choose(page, words.fields.section, words.options.section[item.section]);
      await writeInBothLanguages(page.locator('form'), words.fields.title, 'title', {
        en: item.en,
        it: item.it,
      });
      await whileWaitingFor(page, 'POST', '/api/training/sheet-items', async () => {
        await page.getByRole('button', { name: englishCommon.common.save }).click();
      });
      await expect(page).toHaveURL(/\/staff\/training\/sheets\?/);
    }

    // ---------------------------------------------------------------- the order they were written in, after a reload
    await expectOrder(page, [items[0].en, items[1].en, items[2].en]);
    await page.reload();
    await expectOrder(page, [items[0].en, items[1].en, items[2].en]);

    // And in the other language of the bench, the same sheet in the same order.
    await context.addCookies([{ name: 'hub.lang', value: 'it', url: benchUrl }]);
    await page.reload();
    await expectOrder(page, [items[0].it, items[1].it, items[2].it]);
    await readInEnglish(context);
    await page.reload();

    // ---------------------------------------------------------------- the theory item goes to the top of the sheet
    await rowOf(page, items[2].en).getByRole('link', { name: englishCommon.common.edit }).click();
    await expect(page.getByRole('heading', { name: words.edit })).toBeVisible();
    await page.locator('[id="sort"]').fill('0');
    await whileWaitingFor(page, 'PUT', '/api/training/sheet-items/', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/training\/sheets\?/);

    await page.reload();
    await expectOrder(page, [items[2].en, items[0].en, items[1].en]);
  } finally {
    await removeLeftovers(context);
  }
});

/** Picks a value in one of the filters of a list, by the id `ListFilter` gives its select. */
async function pick(page: Page, id: string, option: string): Promise<void> {
  await page.locator(`[id="${id}"]`).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

function rowOf(page: Page, title: string) {
  return page.getByRole('row').filter({ has: page.getByText(title, { exact: true }) });
}

/** This run's rows of the sheet, top to bottom: the bench may hold items of its own. */
async function expectOrder(page: Page, titles: readonly string[]): Promise<void> {
  const ours = page.getByRole('row').filter({ hasText: `${marker} ${stamp}` });
  await expect(ours).toHaveCount(titles.length);

  for (const [index, title] of titles.entries()) {
    await expect(ours.nth(index)).toContainText(title);
  }
}

/** The items a run of this spec left behind, recognised by the marker in their title. */
async function removeLeftovers(context: BrowserContext): Promise<void> {
  const response = await context.request.get(`/api/training/sheet-items?pageSize=100&q=${marker}`);
  expect(response.status(), await response.text()).toBe(200);

  for (const item of ((await response.json()) as { items: { id: number }[] }).items) {
    const removed = await context.request.delete(`/api/training/sheet-items/${String(item.id)}`, {
      headers: asTheClientDoes,
    });
    expect(removed.status(), await removed.text()).toBeLessThan(300);
  }
}

/** The choice of a rating as the form and the filter draw it, from the words of the module and of the core. */
function ratingOption(rating: TrainingRating): string {
  const common = JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/common.json', import.meta.url)), 'utf8'),
  ) as Record<string, unknown>;
  const name = rating.nameKey
    .split('.')
    .reduce<unknown>((node, key) => (node as Record<string, unknown> | undefined)?.[key], common);

  return training.ratingChoice
    .replace('{{kind}}', training.kinds.Atc)
    .replace('{{shortName}}', rating.shortName)
    .replace('{{name}}', String(name));
}

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    kinds: { Atc: string };
    ratingChoice: string;
    sheets: {
      title: string;
      create: string;
      edit: string;
      fields: { title: string; rating: string; section: string };
      options: { section: { Practice: string; Theory: string } };
    };
  };
}
