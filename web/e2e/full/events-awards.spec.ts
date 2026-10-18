import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import {
  benchAirports,
  benchUrl,
  choose,
  department,
  readInEnglish,
  signIn,
  test,
  whileWaitingFor,
} from './bench';

/**
 * The rules of award of E14b (M4b) and the switch of the reports the hub sends for a member, through the real screens and the real server:
 * the coordinator of the events (`?as=events`) writes a rule in the tab «Awards» of an event — the criterion chosen above its form, the
 * parameters of that criterion below —, reads it in the list in words, and deletes it; the bench's pilot (`?as=pilot`) switches «report my
 * participation in events automatically» on from `/events/mine`, finds it on after a reload, and switches it off again.
 *
 * ⚠️ The signal of an accepted report is not here: on the bench the check of a report finds no session of the pilot, so no item is verified
 * and no rule is met. `EventsAwardRulesTests` proves it — the signals of the rules met and of no other, the mail to whoever assigns, the
 * decision frozen by an award —, and the phase's bench watched it with the item made verified in SQL.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e14b';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench evening of the awards ${stamp}`, it: `La sera degli award del banco ${stamp}` };
const award = { en: `Bench support ${stamp}`, it: `Supporto del banco ${stamp}` };
const blank = '0001-01-01T00:00:00';

async function contextAs(browser: Browser, as: 'events' | 'pilot' | null): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (as === null) {
    await signIn(context);
  } else {
    const signedIn = await context.request.post(`/e2e/signin?as=${as}`);
    expect(signedIn.status(), await signedIn.text()).toBe(200);
  }

  return context;
}

/** An event of the coordinator, a draft: a rule is written whatever the event's state. */
async function anEvent(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfe',
      publicSlots: false,
      privateSlots: false,
      wholeDivision: false,
      organizer: 'Division',
      externalUrl: null,
      title,
      slug,
      summary: title,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: null,
      startsAtUtc: '2031-03-01T18:00:00Z',
      endsAtUtc: '2031-03-01T22:00:00Z',
      visibility: 'Public',
      rowVersion: blank,
    },
  });
  expect(created.status(), await created.text()).toBe(201);

  return ((await created.json()) as { id: number }).id;
}

/** An award of the catalogue, written by the web team, which reaches every department. */
async function anAward(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/awards', {
    headers: asTheClientDoes,
    data: {
      ownerDepartment: department.toUpperCase(),
      name: award,
      description: null,
      criteria: null,
      imageMediaId: null,
      isActive: true,
      rowVersion: blank,
    },
  });
  expect(created.status(), await created.text()).toBe(201);

  return ((await created.json()) as { id: number }).id;
}

test('the coordinator writes a rule of award and a pilot switches the automatic reports on', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  const master = await contextAs(browser, null);

  const id = await anEvent(coordinator.request);
  const awardId = await anAward(master.request);

  afterwards(async () => {
    await coordinator.request.delete(`/api/events/events/${id}`, { headers: asTheClientDoes });
    await master.request.delete(`/api/awards/${awardId}`, { headers: asTheClientDoes });
    await pilot.request.put('/api/me/preferences/events.autoReport', {
      headers: asTheClientDoes,
      data: { value: false },
    });
    await Promise.all([coordinator.close(), pilot.close(), master.close()]);
  });

  // The coordinator: the tab of the event, empty, says what it will hold; a new rule, the route chosen above its form.
  const page = await coordinator.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await page.goto(`/staff/events/${id}?tab=awards`);
  await expect(page.getByRole('tab', { name: events.events.tabs.awards })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByText(events.awardRules.empty)).toBeVisible();
  await page.getByRole('link', { name: events.awardRules.create }).click();

  await choose(page, events.awardRules.fields.criterion, events.awardRules.options.criterion.Route);
  await expect(page.getByText(events.awardRules.explain.Route)).toBeVisible();
  await choose(page, events.awardRules.fields.awardId, award.en);
  await page.getByLabel(events.awardRules.fields.departureIcao, { exact: true }).fill(benchAirports.rome);
  await page.getByLabel(events.awardRules.fields.arrivalIcao, { exact: true }).fill(benchAirports.milan);
  await whileWaitingFor(page, 'POST', '/api/events/award-rules', async () => {
    await page.getByRole('button', { name: 'Save' }).click();
  });

  // Back on the tab: the rule in words.
  const row = page.getByRole('row', { name: new RegExp(award.en) });
  await expect(row).toBeVisible();
  await expect(row.getByText(events.awardRules.options.criterion.Route, { exact: true })).toBeVisible();
  await expect(
    row.getByText(`${benchAirports.rome} → ${benchAirports.milan}`, { exact: true }),
  ).toBeVisible();

  // Deleted from its page.
  await row.getByRole('link', { name: 'Edit' }).click();
  await page.getByRole('button', { name: 'Delete' }).click();
  await whileWaitingFor(page, 'DELETE', '/api/events/award-rules/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: 'Delete' }).click();
  });
  await expect(page.getByText(events.awardRules.empty)).toBeVisible();

  // The pilot: the switch on their page, on, and on again after a reload.
  const mine = await pilot.newPage();
  mine.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await mine.goto('/events/mine');
  const automatic = mine.getByLabel(events.mine.reports.auto.fields.autoReport);
  await expect(automatic).not.toBeChecked();
  await whileWaitingFor(mine, 'PUT', '/api/me/preferences/events.autoReport', async () => {
    await automatic.click();
  });
  await mine.reload();
  await expect(mine.getByLabel(events.mine.reports.auto.fields.autoReport)).toBeChecked();
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { awards: string } };
    awardRules: {
      create: string;
      empty: string;
      fields: { criterion: string; awardId: string; departureIcao: string; arrivalIcao: string };
      options: { criterion: { Route: string } };
      explain: { Route: string };
    };
    mine: { reports: { auto: { fields: { autoReport: string } } } };
  };
}
