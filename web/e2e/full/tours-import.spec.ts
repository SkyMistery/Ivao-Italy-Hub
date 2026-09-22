import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchAirports, choose, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The import of the legs of a tour (M2, T8), through the real screens and under the real CSP: a CSV read in the
 * browser by a library loaded only then; an airport the hub does not know refused on the row of the file; the preview
 * of the server — two legs added — and the import; then a second file on "replace", whose preview deletes the leg it
 * no longer contains. The tour is deleted at the end: the bench database is not thrown away between runs.
 */

const flightops = englishFlightOps();
const legs = flightops.legs;
const importing = legs.import;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench import ${stamp}`, it: `Import del banco ${stamp}` };
const summary = { en: `A bench tour imported ${stamp}`, it: `Un tour del banco importato ${stamp}` };
const slug = `bench-import-${stamp}`;

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/** A CSV as a spreadsheet saves it in much of Europe: semicolons, Windows line ends. */
function csv(...rows: string[][]): { name: string; mimeType: string; buffer: Buffer } {
  const text = [['departure', 'arrival', 'callsign'], ...rows].map((row) => row.join(';')).join('\r\n');
  return { name: 'legs.csv', mimeType: 'text/csv', buffer: Buffer.from(text, 'utf8') };
}

function outcome(key: string, count: number): string {
  return importing.outcome[`${key}_${count === 1 ? 'one' : 'other'}`]!.replace('{{count}}', String(count));
}

async function chooseFile(page: Page, file: ReturnType<typeof csv>): Promise<void> {
  await whileWaitingFor(page, 'POST', '/import/preview', async () => {
    await page.getByLabel(importing.file.label, { exact: true }).setInputFiles(file);
  });
}

test('the legs of a tour are imported from a file, previewed by the server first', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the tour
  const now = Date.now();
  await page.goto('/staff/tours/new');
  await expect(page.getByRole('heading', { name: flightops.tours.create })).toBeVisible();

  await choose(page, flightops.tours.fields.kind, flightops.tours.options.kind.Free);
  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.title, 'title', tourName);
  await page.locator('[id="slug"]').fill(slug);
  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.summary, 'summary', summary);
  await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 24 * 3600 * 1000)));
  await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
  await page.locator('[id="dailyLegLimit"]').fill('5');

  await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
  const tourUrl = page.url();

  await page.getByRole('tab', { name: flightops.tours.tabs.legs }).click();
  await page.getByRole('button', { name: legs.actions.import }).click();
  const panel = page.getByRole('region', { name: importing.title });
  await expect(panel).toBeVisible();

  // ---------------------------------------------------------------- a file with an unknown airport
  const response = page.waitForResponse((candidate) => candidate.url().includes('/import/preview'));
  await panel
    .getByLabel(importing.file.label, { exact: true })
    .setInputFiles(
      csv([benchAirports.rome, benchAirports.milan, 'BCH1/2'], [benchAirports.milan, 'ZZZ9', '']),
    );
  expect((await response).status()).toBe(400);
  await expect(panel.getByText(/^Row 3, Arrival:/)).toBeVisible();

  // ---------------------------------------------------------------- merged, two legs added
  await chooseFile(
    page,
    csv([benchAirports.rome, benchAirports.milan, 'BCH1/2'], [benchAirports.milan, benchAirports.bari, '']),
  );
  await expect(panel.getByText(outcome('Added', 2))).toBeVisible();
  await whileWaitingFor(page, 'POST', '/legs/import', async () => {
    await panel.getByRole('button', { name: importing.apply }).click();
  });
  await expect(panel).toBeHidden();
  await expect(page.getByRole('row', { name: legs.row.replace('{{number}}', '2') })).toBeVisible();
  await expect(
    page.getByRole('row', { name: legs.row.replace('{{number}}', '1') }).getByLabel(legs.fields.callsigns),
  ).toHaveValue('BCH1, BCH2');

  // ---------------------------------------------------------------- replaced, one leg deleted
  await page.getByRole('button', { name: legs.actions.import }).click();
  await choose(page, importing.mode.label, importing.mode.Replace);
  await chooseFile(page, csv([benchAirports.rome, benchAirports.milan, 'BCH1/2']));
  await expect(panel.getByText(outcome('Unchanged', 1))).toBeVisible();
  await expect(panel.getByText(outcome('Deleted', 1))).toBeVisible();
  await whileWaitingFor(page, 'POST', '/legs/import', async () => {
    await panel.getByRole('button', { name: importing.apply }).click();
  });
  await expect(page.getByRole('row', { name: legs.row.replace('{{number}}', '2') })).toHaveCount(0);

  // ---------------------------------------------------------------- deleted, with its legs
  await page.goto(tourUrl);
  await page.getByRole('button', { name: englishCommon.common.delete }).first().click();
  await whileWaitingFor(page, 'DELETE', '/api/flightops/tours/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      create: string;
      tabs: { legs: string };
      fields: { title: string; summary: string; kind: string };
      options: { kind: { Free: string } };
    };
    legs: {
      row: string;
      fields: { callsigns: string };
      actions: { import: string };
      import: {
        title: string;
        apply: string;
        file: { label: string };
        mode: { label: string; Replace: string };
        outcome: Record<string, string>;
      };
    };
  };
}
