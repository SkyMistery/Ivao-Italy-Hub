import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext } from '@playwright/test';

import {
  benchAirports,
  benchUrl,
  choose,
  readInEnglish,
  releasedTourWithOneLeg,
  removeBenchTours,
  signIn,
  whileWaitingFor,
} from './bench';
import { replayFlight } from './replay';

/**
 * The "done when" of T15 (M2), through the real screens against the real server: a tour that proposes an award, a pilot who
 * flies its one leg and sees it on the card of `/tours`, the coordinator who accepts the report — and with it the tour is
 * completed and the pilot pointed out in the queue of the awards, where whoever holds `Awards.Assign` assigns it.
 *
 * And the people's pages of T15b: «add a validator» refused for a pilot and done for a member of staff, then removed; the
 * pilot's page asked by VID; a ban written from it on a tour of this run, and lifted by moving its end.
 *
 * Like the other rounds about a report it leaves one hidden tour behind (a tour with a report is never deleted, §1.2.2). The
 * award's assignment is revoked and the award deleted; the handled line of the queue stays, out of the default view.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  public: { sendReport: string; reportStatus: { Queued: string } };
  report: { send: string };
  review: {
    take: string;
    decide: string;
    open: string;
    fields: { outcome: string };
    options: { status: { Accepted: string; InReview: string }; outcome: { Accepted: string } };
  };
  tours: { fields: { awardId: string } };
  progress: { Legs: string; completed: string };
  validators: {
    fields: { vid: string; tourId: string };
    add: string;
    added: string;
    remove: string;
  };
  pilots: { fields: { vid: string }; open: string; ban: string; sections: { bans: string } };
  bans: { fields: { tourId: string; reason: string }; edit: string };
};

const common = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/common.json', import.meta.url)), 'utf8'),
) as {
  common: { save: string };
  awardSignals: { assign: string };
  awardAssignments: { assign: string };
};

// The server's refusals have a file of their own; the page shows the sentence the server resolved from it.
const refusals = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/errors.json', import.meta.url)), 'utf8'),
) as { errors: { grant: { notStaff: string } } };

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const NEW_ROW = '0001-01-01T00:00:00';

test('a tour flown to the end signals its award, and the award is assigned from the queue', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(240_000);
  const stamp = Date.now().toString(36);
  const slug = `bench-people-${stamp}`;
  const tourName = { en: `Bench completion ${stamp}`, it: `Completamento del banco ${stamp}` };
  const awardName = { en: `Bench tour award ${stamp}`, it: `Award del tour del banco ${stamp}` };

  await readInEnglish(context);
  await signIn(context);

  const pilotContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(pilotContext);
  const signedIn = await pilotContext.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  const pilotVid = ((await signedIn.json()) as { vid: number }).vid;

  const flight = replayFlight({ vid: pilotVid, departure: benchAirports.rome, arrival: benchAirports.milan });
  const made: { award?: number } = {};

  await removeBenchTours(context, 'bench-people-');
  try {
    await complete();
  } finally {
    flight.remove();
    if (made.award !== undefined) {
      await revokeAndDelete(context.request, made.award);
    }
    await removeBenchTours(context, 'bench-people-');
    await pilotContext.close();
  }

  async function complete() {
    // ---------------------------------------------------------------- a tour of one leg, proposing an award
    made.award = await created(context.request, '/api/awards', {
      ownerDepartment: 'FOD',
      name: awardName,
      description: null,
      criteria: null,
      imageMediaId: null,
      isActive: true,
      rowVersion: NEW_ROW,
    });
    const tourId = await releasedTourWithOneLeg(page, context, { name: tourName, slug });
    await page.goto(`/staff/tours/${tourId}`);
    await choose(page, words.tours.fields.awardId, awardName.en);
    await whileWaitingFor(page, 'PUT', `/api/flightops/tours/${tourId}`, async () => {
      await page.getByRole('button', { name: common.common.save }).click();
    });

    // ---------------------------------------------------------------- the pilot reports, and sees how far on the card
    const pilotPage = await pilotContext.newPage();
    await pilotPage.goto(`/tours/${slug}`);
    await pilotPage.getByRole('link', { name: words.public.sendReport }).click();
    const choice = pilotPage.getByRole('radio', { name: /ICARG/ });
    await expect(choice).toBeVisible();
    await choice.check();
    await whileWaitingFor(pilotPage, 'POST', `/api/flightops/tours/${tourId}/reports`, async () => {
      await pilotPage.getByRole('button', { name: words.report.send }).click();
    });
    await expect(pilotPage.getByText(words.public.reportStatus.Queued, { exact: true })).toBeVisible();

    await pilotPage.goto('/tours');
    const card = pilotPage.getByRole('article').filter({ hasText: tourName.en });
    await expect(
      card.getByText(words.progress.Legs.replace('{{done}}', '0').replace('{{target}}', '1')),
    ).toBeVisible();

    // ---------------------------------------------------------------- the coordinator accepts it
    await page.goto(`/staff/tours/review?tour=${tourId}`);
    await page
      .getByRole('row', { name: new RegExp(`Bench Pilot \\(${pilotVid}\\)`) })
      .getByRole('link', { name: words.review.open })
      .click();
    await whileWaitingFor(page, 'POST', '/take', async () => {
      await page.getByRole('button', { name: words.review.take }).click();
    });
    await expect(page.getByText(words.review.options.status.InReview, { exact: true }).first()).toBeVisible();
    await choose(page, words.review.fields.outcome, words.review.options.outcome.Accepted);
    await whileWaitingFor(page, 'POST', '/decide', async () => {
      await page.getByRole('button', { name: words.review.decide }).click();
    });
    await expect(page.getByText(words.review.options.status.Accepted, { exact: true }).first()).toBeVisible();

    // ---------------------------------------------------------------- completed, on the pilot's card
    await pilotPage.goto('/tours');
    await expect(card.getByText(words.progress.completed, { exact: true })).toBeVisible();

    // ---------------------------------------------------------------- the queue of the awards, and the assignment
    await page.goto('/staff/awards/queue');
    // The reason is written in the division's language (T15a): the Italian title carries the run's stamp as well.
    const line = page.getByRole('row').filter({ hasText: stamp });
    await expect(line).toBeVisible();
    await expect(line).toContainText(awardName.en);
    await line.getByRole('link', { name: common.awardSignals.assign }).click();

    await whileWaitingFor(page, 'POST', '/api/award-assignments', async () => {
      await page.getByRole('button', { name: common.awardAssignments.assign }).click();
    });
    // Assigned from the queue, the screen goes back to the queue — where the line no longer waits — and the register has it.
    await expect(page).toHaveURL(/\/staff\/awards\/queue(\?|$)/);
    await expect(page.getByRole('row').filter({ hasText: stamp })).toHaveCount(0);
    await page.goto('/staff/awards/assignments');
    await expect(page.getByRole('row').filter({ hasText: stamp }).first()).toBeVisible();
  }
});

test('a validator is added and removed, and a pilot is found by VID, banned from a tour and let back', async ({
  page,
  context,
}) => {
  test.setTimeout(180_000);
  const stamp = Date.now().toString(36);
  const slug = `bench-people-${stamp}`;
  const tourName = { en: `Bench people ${stamp}`, it: `Persone del banco ${stamp}` };

  await readInEnglish(context);
  await signIn(context);

  // Both come to life on their first sign in: the pilot is not staff, the assistant is (IT-FOAC).
  const pilotVid = await benchPerson(context.request, 'pilot');
  const assistantVid = await benchPerson(context.request, 'assistant');
  // …and the coordinator signs in again: a sign in as somebody else is a sign out of the bench's own identity.
  await signIn(context);

  await removeBenchTours(context, 'bench-people-');
  try {
    await releasedTourWithOneLeg(page, context, { name: tourName, slug });

    // ---------------------------------------------------------------- «add a validator»
    await page.goto('/staff/tours/validators');
    const form = page.locator('form');
    await form.locator('[id="vid"]').fill(String(pilotVid));
    await choose(page, words.validators.fields.tourId, tourName.en, form);
    await page.getByRole('button', { name: words.validators.add, exact: true }).click();
    await expect(page.getByText(refusals.errors.grant.notStaff)).toBeVisible();

    await form.locator('[id="vid"]').fill(String(assistantVid));
    await whileWaitingFor(page, 'POST', '/api/flightops/validators', async () => {
      await page.getByRole('button', { name: words.validators.add, exact: true }).click();
    });
    const row = page.getByRole('row', { name: new RegExp(`Bench Assistant \\(${assistantVid}\\)`) });
    await expect(row.getByText(tourName.en, { exact: true })).toBeVisible();

    // ---------------------------------------------------------------- and removed
    await row
      .getByRole('listitem')
      .filter({ hasText: tourName.en })
      .getByRole('button', { name: words.validators.remove })
      .click();
    await whileWaitingFor(page, 'DELETE', '/api/flightops/validators/', async () => {
      await page.getByRole('alertdialog').getByRole('button', { name: words.validators.remove }).click();
    });
    // Off their row — the form above still holds the tour it was given, which is not an enablement.
    await expect(row.getByText(tourName.en, { exact: true })).toHaveCount(0);

    // ---------------------------------------------------------------- the pilot, by VID
    await page.goto('/staff/tours/pilots');
    await page.locator('[id="vid"]').fill(String(pilotVid));
    await page.getByRole('button', { name: words.pilots.open, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/staff/tours/pilots/${pilotVid}(\\?|$)`));
    await expect(page.getByRole('heading', { name: `Bench Pilot (${pilotVid})` })).toBeVisible();

    // ---------------------------------------------------------------- banned from the tour of this run
    await page.getByRole('link', { name: words.pilots.ban, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/staff/tours/bans/new\\?vid=${pilotVid}`));
    await choose(page, words.bans.fields.tourId, tourName.en);
    await page.locator('[id="reason"]').fill(`Bench ban ${stamp}`);
    await whileWaitingFor(page, 'POST', '/api/flightops/bans', async () => {
      await page.getByRole('button', { name: common.common.save }).click();
    });
    await expect(page).toHaveURL(new RegExp(`/staff/tours/pilots/${pilotVid}(\\?|$)`));
    const ban = page.getByRole('listitem').filter({ hasText: `Bench ban ${stamp}` });
    await expect(ban).toBeVisible();

    // ---------------------------------------------------------------- let back: a ban is never deleted, its end is moved
    await ban.getByRole('link', { name: tourName.en }).click();
    const starts = await page.locator('[id="startsAt"]').inputValue();
    await page.locator('[id="endsAt"]').fill(minuteAfter(starts));
    await whileWaitingFor(page, 'PUT', '/api/flightops/bans/', async () => {
      await page.getByRole('button', { name: common.common.save }).click();
    });
  } finally {
    await removeBenchTours(context, 'bench-people-');
  }
});

async function created(request: APIRequestContext, path: string, data: unknown): Promise<number> {
  const response = await request.post(path, { headers: asTheClientDoes, data });
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: number }).id;
}

/** Signs in as one of the bench's other people once, so the hub knows them; their VID. */
async function benchPerson(request: APIRequestContext, as: 'pilot' | 'assistant'): Promise<number> {
  const response = await request.post(`/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
  return ((await response.json()) as { vid: number }).vid;
}

/** `2026-09-24T10:15` → `2026-09-24T10:16`, as a `datetime-local` holds it (UTC in the form generator). */
function minuteAfter(wallClock: string): string {
  return new Date(new Date(`${wallClock}:00Z`).getTime() + 60_000).toISOString().slice(0, 16);
}

/** The assignment of this run's award revoked, then the award deleted: nobody holds it any more. */
async function revokeAndDelete(request: APIRequestContext, awardId: number): Promise<void> {
  const list = await request.get(`/api/award-assignments?pageSize=100&filter[awardId]=${awardId}`);
  if (list.ok()) {
    for (const assignment of ((await list.json()) as { items: { id: number; awardId: number }[] }).items) {
      if (assignment.awardId === awardId) {
        await request.delete(`/api/award-assignments/${assignment.id}`, { headers: asTheClientDoes });
      }
    }
  }

  await request.delete(`/api/awards/${awardId}`, { headers: asTheClientDoes });
}
