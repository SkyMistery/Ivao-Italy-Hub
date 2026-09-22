import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  choose,
  createContent,
  deleteContent,
  publishContent,
  readInEnglish,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The "done when" of T9 (M2), through the real screens: the flight operations department writes a public error and a
 * general rule with the parameters of the disconnections, a tour amends the rule with one number of its own and follows
 * the general rule for the other, and a published page with the block of the public errors shows the error to a visitor.
 * Every row goes back at the end: general rules and errors are the division's, and the bench database stays.
 */

const flightops = englishFlightOps();
const rules = flightops.rules;
const tourErrors = flightops.tourErrors;
const checks = flightops.checks;

const stamp = Date.now().toString(36);
const code = `B${stamp}`.slice(0, 10).toUpperCase();
const errorName = { en: `Bench disconnection ${stamp}`, it: `Disconnessione del banco ${stamp}` };
const tourName = { en: `Bench rules ${stamp}`, it: `Regole del banco ${stamp}` };
const ruleTitle = { en: `Bench connection ${stamp}`, it: `Connessione del banco ${stamp}` };
const slug = `bench-rules-${stamp}`;

const asTheClientDoes = { 'X-Requested-With': 'hub' };

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/** The identifier of the one row of a list whose field has this value, asked of the API as the screens ask it. */
async function idOf(context: BrowserContext, uri: string, field: string, value: string): Promise<number> {
  const response = await context.request.get(uri);
  expect(response.status()).toBe(200);
  const page = (await response.json()) as { items: Record<string, unknown>[] };
  const row = page.items.find((item) => JSON.stringify(item[field]).includes(value));
  expect(row, `${value} in ${uri}`).toBeDefined();
  return row!.id as number;
}

async function remove(context: BrowserContext, uri: string): Promise<void> {
  const response = await context.request.delete(uri, { headers: asTheClientDoes });
  expect(response.status(), await response.text()).toBeLessThan(300);
}

async function items(context: BrowserContext, uri: string): Promise<Record<string, unknown>[]> {
  const response = await context.request.get(uri);
  expect(response.status()).toBe(200);
  return ((await response.json()) as { items: Record<string, unknown>[] }).items;
}

/**
 * The rows a run of this spec that stopped half way left behind — its tours first, which take their amendments, then its
 * general rules and its errors — recognised by the names this spec gives them and nothing else.
 */
async function removeLeftovers(context: BrowserContext): Promise<void> {
  for (const tour of await items(context, '/api/flightops/tours?pageSize=100&q=bench-rules-')) {
    await remove(context, `/api/flightops/tours/${String(tour.id)}`);
  }
  for (const rule of await items(context, '/api/flightops/rules?pageSize=100&q=Bench%20connection')) {
    await remove(context, `/api/flightops/rules/${String(rule.id)}`);
  }
  for (const error of await items(context, '/api/flightops/errors?pageSize=100&q=Bench%20disconnection')) {
    await remove(context, `/api/flightops/errors/${String(error.id)}`);
  }
}

test('a general rule with parameters is amended by a tour, and a page shows the public errors', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(90_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  let contentId: number | null = null;
  await removeLeftovers(context);
  try {
    await composeAndRead();
  } finally {
    // ---------------------------------------------------------------- everything back, however far it got
    if (contentId !== null) {
      await deleteContent(context, contentId);
    }
    await removeLeftovers(context);
  }

  async function composeAndRead() {
    // ---------------------------------------------------------------- a public error
    await page.goto('/staff/tours/errors/new');
    await expect(page.getByRole('heading', { name: tourErrors.create })).toBeVisible();
    await choose(page, checks.fields.checkKey, checks.options.checkKey.disconnections);
    await expect(page.getByText(checks.explain.disconnections)).toBeVisible();
    const errorForm = page.locator('form').last();
    await writeInBothLanguages(errorForm, tourErrors.fields.name, 'name', errorName);
    await writeInBothLanguages(errorForm, tourErrors.fields.description, 'description', {
      en: 'Offline longer than the rule allows.',
      it: 'Disconnesso più a lungo di quanto la regola consenta.',
    });
    await choose(page, tourErrors.fields.category, tourErrors.options.category.Warning);
    await page.locator('[id="yearlyMax"]').fill('3');
    await page.getByRole('switch', { name: tourErrors.fields.isPublic }).click();
    await whileWaitingFor(page, 'POST', '/api/flightops/errors', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    // The list, with the search it adds to its address.
    await expect(page).toHaveURL(/\/staff\/tours\/errors(\?|$)/);
    await idOf(context, '/api/flightops/errors?pageSize=100', 'name', errorName.en);

    // ---------------------------------------------------------------- the general rule of the disconnections
    await page.goto('/staff/tours/rules/new');
    await choose(page, checks.fields.checkKey, checks.options.checkKey.disconnections);
    // The form below is drawn again for the check chosen: what is written before that is written into the old one.
    await expect(page.locator('[id="parameters.maxTotalDisconnectMinutes"]')).toBeVisible();
    const ruleForm = page.locator('form').last();
    await page.locator('[id="code"]').fill(code.toLowerCase());
    await writeInBothLanguages(ruleForm, rules.fields.title, 'title', ruleTitle);
    await writeInBothLanguages(ruleForm, rules.fields.text, 'text', {
      en: 'Stay connected for the whole flight.',
      it: 'Resta connesso per tutto il volo.',
    });
    // Only the total: the single disconnection is the check's starting value.
    await page.locator('[id="parameters.maxTotalDisconnectMinutes"]').fill('25');
    await page.getByRole('checkbox', { name: new RegExp(errorName.en) }).check();
    await whileWaitingFor(page, 'POST', '/api/flightops/rules', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/rules(\?|$)/);
    const row = page.getByRole('row', { name: new RegExp(code) });
    await expect(row.getByText('15 min, Σ 25 min')).toBeVisible();

    // ---------------------------------------------------------------- a tour amends it
    const now = Date.now();
    await page.goto('/staff/tours/new');
    await choose(page, flightops.tours.fields.kind, flightops.tours.options.kind.Free);
    await writeInBothLanguages(page.locator('form'), flightops.tours.fields.title, 'title', tourName);
    await page.locator('[id="slug"]').fill(slug);
    await writeInBothLanguages(page.locator('form'), flightops.tours.fields.summary, 'summary', tourName);
    await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now + 24 * 3600 * 1000)));
    await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
    await page.locator('[id="dailyLegLimit"]').fill('5');
    await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);

    await page.getByRole('tab', { name: flightops.tours.tabs.rules }).click();
    const inForce = page.getByTestId('effective-rules');
    // Each rule in force is a row of its own; the bench may hold other general rules of other runs.
    const ruleInForce = (ruleCode: string) =>
      inForce.getByRole('listitem').filter({ has: page.getByText(ruleCode, { exact: true }) });
    await expect(ruleInForce(code).getByText('15 min · Σ 25 min')).toBeVisible();

    await page
      .getByRole('row', { name: new RegExp(code) })
      .getByRole('link', { name: rules.amend })
      .click();
    await expect(page.getByText(rules.amendingHint)).toBeVisible();
    await page.locator('[id="code"]').fill(`${code}A`);
    await page.locator('[id="parameters.maxSingleDisconnectMinutes"]').fill('10');
    await whileWaitingFor(page, 'POST', '/api/flightops/rules', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/tab=rules/);

    // In force: the amendment in the general rule's place, its single disconnection and the general rule's total.
    await expect(inForce.getByText(code, { exact: true })).toBeHidden();
    await expect(ruleInForce(`${code}A`).getByText('10 min · Σ 25 min')).toBeVisible();

    // ---------------------------------------------------------------- a page with the public errors
    const pageSlug = `bench-errors-${stamp}`;
    const content = await createContent(context, {
      slug: pageSlug,
      visibility: 'Public',
      title: { en: 'Errors of the tours', it: 'Errori dei tour' },
      body: {
        schemaVersion: 1,
        sections: [
          {
            id: 's_errors',
            layout: 'stacked',
            background: 'none',
            padding: 'md',
            width: 'default',
            blocks: [
              { id: 'b_errors', type: 'flightops.errorCatalog', version: 1, props: {}, renderMode: 'live' },
            ],
            sections: [],
          },
        ],
      },
    });

    contentId = content.id;
    await publishContent(context, content.id);

    const visitor = await browser.newContext();
    await readInEnglish(visitor);
    const reader = await visitor.newPage();
    reader.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });
    await reader.goto(`/${pageSlug}`);
    // Only this run's error: the bench may hold public errors of its own.
    const shown = reader
      .getByRole('listitem')
      .filter({ has: reader.getByText(errorName.en, { exact: true }) });
    await expect(shown.getByText('Offline longer than the rule allows.')).toBeVisible();
    await expect(shown.getByText(new RegExp(`${code} ${ruleTitle.en}`))).toBeVisible();
    await visitor.close();
  }
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      tabs: { rules: string };
      fields: { title: string; summary: string; kind: string };
      options: { kind: { Free: string } };
    };
    rules: { amend: string; amendingHint: string; fields: { title: string; text: string } };
    tourErrors: {
      create: string;
      fields: { name: string; description: string; category: string; isPublic: string };
      options: { category: { Warning: string } };
    };
    checks: {
      fields: { checkKey: string };
      options: { checkKey: { disconnections: string } };
      explain: { disconnections: string };
    };
  };
}
