import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  addLeg,
  benchAirports,
  choose,
  readInEnglish,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';
import { replayFlight } from './replay';

/**
 * The "done when" of T11 (M2), through the real screens against the real server: a pilot opens a released tour, sends
 * the report of a flight the tracker has, sees it in the queue — the leg waiting, the report «queued» — and withdraws
 * it, which frees the leg again.
 *
 * The tracker is the recorded one (`tests/fixtures/ivao/`), with one of its flights copied to have taken off yesterday
 * under the bench's VID (`replay.ts`): the recordings are from June, and no report window reaches them.
 *
 * ⚠️ A tour with a report is never deleted, even a withdrawn one (design M2 §1.2.2): the server refuses
 * (`tourHasReports`) and the spec hides it instead. So every run
 * leaves one hidden tour behind in the bench database — hidden, it is on no public page and in no other spec's way.
 */

const flightops = englishFlightOps();
const tours = flightops.tours;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench report ${stamp}`, it: `Report del banco ${stamp}` };
const slug = `bench-report-${stamp}`;

const asTheClientDoes = { 'X-Requested-With': 'hub' };

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/**
 * The tours a run of this spec left behind, recognised by their address: deleted, or — the server refuses to delete a
 * tour with reports (`tourHasReports`) — hidden. One already hidden is left as it is.
 */
async function removeLeftovers(context: BrowserContext): Promise<void> {
  const response = await context.request.get('/api/flightops/tours?pageSize=100&q=bench-report-');
  expect(response.status()).toBe(200);

  const found = ((await response.json()) as { items: { id: number; isHidden: boolean }[] }).items;
  for (const tour of found.filter((row) => !row.isHidden)) {
    const removed = await context.request.delete(`/api/flightops/tours/${tour.id}`, {
      headers: asTheClientDoes,
    });
    if (removed.status() < 300) {
      continue;
    }

    expect(await removed.text()).toContain('flightops:errors.tourHasReports');
    const hidden = await context.request.post(`/api/flightops/tours/${tour.id}/status`, {
      headers: asTheClientDoes,
      data: { action: 'Hide' },
    });
    expect(hidden.status(), await hidden.text()).toBeLessThan(300);
  }
}

test('a pilot reports a flight on a tour, sees it in the queue and withdraws it', async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  await readInEnglish(context);
  await signIn(context);

  const complaints: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error' && !message.location().url.includes('/tiles/')) {
      complaints.push(message.text());
    }
  });
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const me = (await (await context.request.get('/api/me')).json()) as { user: { vid: number } };
  const flight = replayFlight({
    vid: me.user.vid,
    departure: benchAirports.rome,
    arrival: benchAirports.milan,
  });

  await removeLeftovers(context);
  try {
    await reportAndWithdraw();
  } finally {
    flight.remove();
    await removeLeftovers(context);
  }

  async function reportAndWithdraw() {
    // ---------------------------------------------------------------- a tour released two days ago, one leg
    const now = Date.now();
    await page.goto('/staff/tours/new');
    await choose(page, tours.fields.kind, tours.options.kind.Free);
    await writeInBothLanguages(page.locator('form'), tours.fields.title, 'title', tourName);
    await page.locator('[id="slug"]').fill(slug);
    await writeInBothLanguages(page.locator('form'), tours.fields.summary, 'summary', {
      en: 'One leg to report.',
      it: 'Una leg da riportare.',
    });
    await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 48 * 3600 * 1000)));
    await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
    await page.locator('[id="dailyLegLimit"]').fill('5');
    await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);

    const tourId = Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);
    await addLeg(context, tourId, benchAirports.rome, benchAirports.milan);

    await page.goto(`/staff/tours/${tourId}`);
    await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/status`, async () => {
      await page.getByRole('button', { name: tours.actions.ready }).click();
    });

    // ---------------------------------------------------------------- the pilot's page: nothing flown yet
    await page.goto(`/tours/${slug}`);
    await expect(page.getByRole('heading', { level: 1, name: tourName.en })).toBeVisible();
    const legs = page.getByRole('table');
    await expect(legs.getByText(flightops.public.legProgress.Todo, { exact: true })).toBeVisible();

    await page.getByRole('link', { name: flightops.public.sendReport }).click();
    await expect(page).toHaveURL(new RegExp(`/tours/${slug}/report\\?leg=\\d+$`));

    // ---------------------------------------------------------------- the flight the tracker has, and the send
    const choice = page.getByRole('radio', { name: /ICARG/ });
    await expect(choice).toBeVisible();
    await choice.check();
    await page.getByLabel(flightops.report.fields.pilotRemarks, { exact: true }).fill('Smooth flight.');

    await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/reports`, async () => {
      await page.getByRole('button', { name: flightops.report.send }).click();
    });

    // ---------------------------------------------------------------- in the queue
    await expect(page).toHaveURL(new RegExp(`/tours/${slug}$`));
    await expect(page.getByText(flightops.public.reportStatus.Queued, { exact: true })).toBeVisible();
    await expect(legs.getByText(flightops.public.legProgress.Pending, { exact: true })).toBeVisible();

    // The same flight again: it is claimed, so the tracker has nothing left to offer for this leg.
    const sessions = await context.request.get(
      `/api/flightops/tours/${tourId}/reports/sessions?legId=${(await firstLeg(context, slug)).id}`,
    );
    expect(((await sessions.json()) as { id: number }[]).map((entry) => entry.id)).not.toContain(
      flight.sessionId,
    );

    // ---------------------------------------------------------------- withdrawn: the leg is to fly again
    await page.getByRole('button', { name: flightops.public.withdraw }).click();
    await whileWaitingFor(page, 'POST', '/withdraw', async () => {
      await page.getByRole('alertdialog').getByRole('button', { name: flightops.public.withdraw }).click();
    });
    await expect(page.getByText(flightops.public.reportStatus.Withdrawn, { exact: true })).toBeVisible();
    await expect(legs.getByText(flightops.public.legProgress.Todo, { exact: true })).toBeVisible();

    expect(complaints.filter((text) => !text.includes('favicon'))).toEqual([]);
  }
});

async function firstLeg(context: BrowserContext, tourSlug: string): Promise<{ id: number }> {
  const response = await context.request.get(`/api/flightops/tours/public/${tourSlug}`);
  return ((await response.json()) as { legs: { id: number }[] }).legs[0]!;
}

/** The module's own English, read from the file the module ships: no user facing string is written in a spec. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      fields: { title: string; summary: string; kind: string };
      options: { kind: { Free: string } };
      actions: { ready: string };
    };
    public: {
      sendReport: string;
      withdraw: string;
      legProgress: { Todo: string; Pending: string };
      reportStatus: { Queued: string; Withdrawn: string };
    };
    report: { send: string; fields: { pilotRemarks: string } };
  };
}
