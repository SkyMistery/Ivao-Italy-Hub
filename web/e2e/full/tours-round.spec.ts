import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';
import * as XLSX from 'xlsx';

import { englishCommon } from '../locales';

import {
  benchAirports,
  benchUrl,
  choose,
  readInEnglish,
  removeBenchTours,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';
import { replayFlight, type ReplayedFlight } from './replay';

/**
 * The whole round of the tours (M2, T20c), on **one** tour, through the real screens against the real server — the round
 * the other specs cover a piece at a time, each on a tour of its own:
 *
 * 1. a template of a Free tour, and a tour made from it;
 * 2. its two legs imported from a workbook that opens on a summary sheet, as the division's own do;
 * 3. its dates, its summary and the award it proposes, and "ready";
 * 4. the first leg flown on the fake tracker and reported; the automatic check flags it; the coordinator rejects it on a
 *    dangerous error; the pilot disputes it; the assistant — whoever decided does not judge the dispute — upholds it; the
 *    coordinator takes it again and accepts it;
 * 5. the second leg reported; rejected by mistake, **reopened** by whoever decided it, and accepted;
 * 6. the tour completed on the pilot's card, and the award assigned from the queue of the awards.
 *
 * Three people: the bench's coordinator, its pilot (`?as=pilot`) and the assistant coordinator (`?as=assistant`). The mails
 * are not waited for: `tours-review.spec.ts` and `tours-dispute.spec.ts` read them in Mailpit. Like every round with a
 * report it leaves one hidden tour behind (a tour with reports is never deleted, design M2 §1.2.2); the template, the rules,
 * the errors and the award's assignment go.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  tourTemplates: { create: string };
  tours: {
    fromTemplate: string;
    saved: string;
    tabs: { legs: string };
    fields: { title: string; summary: string; kind: string; awardId: string };
    options: { kind: { Free: string } };
    actions: { ready: string; hide: string };
  };
  tourFromTemplate: { submit: string; fields: { templateId: string; title: string } };
  legs: {
    row: string;
    actions: { import: string };
    import: { title: string; apply: string; file: { label: string }; outcome: Record<string, string> };
  };
  public: {
    sendReport: string;
    disputeAction: string;
    disputed: string;
    fields: { text: string };
    reportStatus: { Queued: string };
    dispute: { Upheld: string };
  };
  report: { send: string };
  review: {
    take: string;
    decide: string;
    open: string;
    reopen: string;
    decideDispute: string;
    fields: { outcome: string; noteToPilot: string; reason: string; answer: string };
    options: {
      status: { Queued: string; InReview: string; Accepted: string; Rejected: string };
      outcome: { Accepted: string; Rejected: string; Upheld: string };
      checks: { Accept: string };
    };
    errors: { byCheck: string };
  };
  evidence: { flightRulesNotAdmitted: string };
  progress: { completed: string };
};

const common = englishCommon as unknown as {
  common: { save: string; delete: string };
  awardSignals: { assign: string };
  awardAssignments: { assign: string };
};

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const NEW_ROW = '0001-01-01T00:00:00';

const stamp = Date.now().toString(36);
const slug = `bench-round-${stamp}`;
const templateName = { en: `Bench round template ${stamp}`, it: `Template del giro del banco ${stamp}` };
const tourName = { en: `Bench round ${stamp}`, it: `Giro del banco ${stamp}` };
const summary = { en: `The whole round ${stamp}`, it: `Il giro completo ${stamp}` };
const awardName = { en: `Bench round award ${stamp}`, it: `Award del giro del banco ${stamp}` };
const dangerousName = { en: `Bench round incursion ${stamp}`, it: `Incursione del giro del banco ${stamp}` };
const checkedName = {
  en: `Bench round flight rules ${stamp}`,
  it: `Regole di volo del giro del banco ${stamp}`,
};
const ruleCode = `BG${stamp.slice(-6).toUpperCase()}`;

/** `datetime-local` wants the UTC wall clock without seconds, which is what the form stores it as. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/**
 * The division's workbooks of tours open on a summary and keep the legs on a sheet of their own (T8): the editor finds the
 * sheet with a header of legs by itself. Written here with the library the editor reads it with.
 */
function workbook(): { name: string; mimeType: string; buffer: Buffer } {
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(
    book,
    XLSX.utils.aoa_to_sheet([['Tour'], [tourName.en], ['Two legs']]),
    'Summary',
  );
  XLSX.utils.book_append_sheet(
    book,
    XLSX.utils.aoa_to_sheet([
      ['departure', 'arrival', 'callsign'],
      [benchAirports.rome, benchAirports.milan, ''],
      [benchAirports.milan, benchAirports.bari, ''],
    ]),
    'Legs',
  );

  return {
    name: 'legs.xlsx',
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer: XLSX.write(book, { type: 'buffer', bookType: 'xlsx' }) as Buffer,
  };
}

test('a tour from a template, its legs imported, flown, checked, validated, disputed, reopened, completed and awarded', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(420_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const pilotContext = await signedInAs(browser, 'pilot');
  const pilotVid = pilotContext.vid;
  const assistantContext = await signedInAs(browser, 'assistant');

  const made: {
    template?: number;
    award?: number;
    rules: number[];
    errors: number[];
    flight?: ReplayedFlight;
  } = {
    rules: [],
    errors: [],
  };

  await removeBenchTours(context, 'bench-round-');
  try {
    await round();
  } finally {
    made.flight?.remove();
    for (const rule of made.rules) {
      await context.request.delete(`/api/flightops/rules/${rule}`, { headers: asTheClientDoes });
    }
    for (const error of made.errors) {
      await context.request.delete(`/api/flightops/errors/${error}`, { headers: asTheClientDoes });
    }
    await removeBenchTours(context, 'bench-round-');
    if (made.template !== undefined) {
      await context.request.delete(`/api/flightops/tours/${made.template}`, { headers: asTheClientDoes });
    }
    if (made.award !== undefined) {
      await revokeAndDelete(context.request, made.award);
    }
    await assistantContext.context.close();
    await pilotContext.context.close();
  }

  async function round() {
    // ---------------------------------------------------------------- 1. a template, and a tour made from it
    await page.goto('/staff/tours/new?template=true');
    await expect(page.getByRole('heading', { name: words.tourTemplates.create })).toBeVisible();
    await choose(page, words.tours.fields.kind, words.tours.options.kind.Free);
    await writeInBothLanguages(page.locator('form'), words.tours.fields.title, 'title', templateName);
    await page.locator('[id="dailyLegLimit"]').fill('5');
    await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
      await page.getByRole('button', { name: common.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
    made.template = idOf(page);

    await page.goto('/staff/tours/from-template');
    await expect(page.getByRole('heading', { name: words.tours.fromTemplate })).toBeVisible();
    await choose(page, words.tourFromTemplate.fields.templateId, templateName.en);
    await writeInBothLanguages(page.locator('form'), words.tourFromTemplate.fields.title, 'title', tourName);
    await page.locator('[id="slug"]').fill(slug);
    await whileWaitingFor(page, 'POST', '/api/flightops/tours/from-template/', async () => {
      await page.getByRole('button', { name: words.tourFromTemplate.submit }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
    await expect(page.getByRole('heading', { name: tourName.en })).toBeVisible();
    const tourId = idOf(page);
    const tourUrl = page.url();

    // ---------------------------------------------------------------- 2. the legs, from a workbook
    await page.getByRole('tab', { name: words.tours.tabs.legs }).click();
    await page.getByRole('button', { name: words.legs.actions.import }).click();
    const panel = page.getByRole('region', { name: words.legs.import.title });
    await whileWaitingFor(page, 'POST', '/import/preview', async () => {
      await panel.getByLabel(words.legs.import.file.label, { exact: true }).setInputFiles(workbook());
    });
    await expect(
      panel.getByText(words.legs.import.outcome.Added_other!.replace('{{count}}', '2')),
    ).toBeVisible();
    await whileWaitingFor(page, 'POST', '/legs/import', async () => {
      await panel.getByRole('button', { name: words.legs.import.apply }).click();
    });
    await expect(page.getByRole('row', { name: words.legs.row.replace('{{number}}', '2') })).toBeVisible();

    // ---------------------------------------------------------------- 3. dates, summary, award, ready
    made.award = await created(context.request, '/api/awards', {
      ownerDepartment: 'FOD',
      name: awardName,
      description: null,
      criteria: null,
      imageMediaId: null,
      isActive: true,
      rowVersion: NEW_ROW,
    });

    // Released three days ago: the two flights of the fake tracker took off after it.
    const now = Date.now();
    await page.goto(tourUrl);
    await writeInBothLanguages(page.locator('form'), words.tours.fields.summary, 'summary', summary);
    await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 72 * 3600 * 1000)));
    await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
    await choose(page, words.tours.fields.awardId, awardName.en);
    await whileWaitingFor(page, 'PUT', `/api/flightops/tours/${tourId}`, async () => {
      await page.getByRole('button', { name: common.common.save }).click();
    });
    await expect(page.getByText(words.tours.saved)).toBeVisible();
    await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/status`, async () => {
      await page.getByRole('button', { name: words.tours.actions.ready }).click();
    });
    await expect(page.getByRole('button', { name: words.tours.actions.hide })).toBeVisible();

    // The rules of the tour: one a validator judges, and one with an automatic check the recorded IFR flight fails.
    const dangerous = await created(context.request, '/api/flightops/errors', {
      name: dangerousName,
      description: {
        en: 'Crossed a runway without a clearance.',
        it: 'Ha attraversato una pista senza autorizzazione.',
      },
      examples: null,
      category: 'Dangerous',
      yearlyMax: null,
      checkKey: null,
      isPublic: false,
      retired: false,
      rowVersion: NEW_ROW,
    });
    made.errors.push(dangerous);
    made.rules.push(
      await created(context.request, '/api/flightops/rules', {
        tourId,
        code: ruleCode,
        title: { en: 'Ground movements', it: 'Movimenti a terra' },
        text: { en: 'Hold short unless cleared.', it: 'Attendere prima della pista se non autorizzati.' },
        amendsRuleId: null,
        checkKey: null,
        parameters: null,
        errorIds: [dangerous],
        sort: 0,
        retired: false,
        rowVersion: NEW_ROW,
      }),
    );
    const warning = await created(context.request, '/api/flightops/errors', {
      name: checkedName,
      description: {
        en: 'Flew with flight rules the tour does not admit.',
        it: 'Regole di volo non ammesse dal tour.',
      },
      examples: null,
      category: 'Warning',
      yearlyMax: 3,
      checkKey: 'flightRules',
      isPublic: false,
      retired: false,
      rowVersion: NEW_ROW,
    });
    made.errors.push(warning);
    made.rules.push(
      await created(context.request, '/api/flightops/rules', {
        tourId,
        code: `${ruleCode}F`,
        title: { en: 'Flight rules', it: 'Regole di volo' },
        text: { en: 'VFR only.', it: 'Solo VFR.' },
        amendsRuleId: null,
        checkKey: 'flightRules',
        parameters: { rules: ['V'] },
        errorIds: [warning],
        sort: 1,
        retired: false,
        rowVersion: NEW_ROW,
      }),
    );

    const legs = await legsOf(context.request, tourId);
    const pilot = await pilotContext.context.newPage();
    const assistant = await assistantContext.context.newPage();

    // ---------------------------------------------------------------- 4. the first leg: flown, checked, rejected
    made.flight = replayFlight({
      vid: pilotVid,
      departure: benchAirports.rome,
      arrival: benchAirports.milan,
      hoursAgo: 30,
    });
    const first = await report(pilot, pilotContext.context.request, tourId, legs[0]!);

    await page.goto(`/staff/tours/review?tour=${tourId}`);
    const row = page.getByRole('row', { name: new RegExp(`Bench Pilot \\(${pilotVid}\\)`) });
    await expect(row).toContainText(words.review.options.checks.Accept);
    await row.getByRole('link', { name: words.review.open }).click();
    await expect(page).toHaveURL(new RegExp(`/staff/tours/review/${first}$`));

    // What the check found, said in words, and its warning suggested — ticked by nobody.
    await expect(
      page.getByText(
        words.evidence.flightRulesNotAdmitted.replace('{{rules}}', 'I').replace('{{admitted}}', 'V'),
      ),
    ).toBeVisible();
    const suggested = page.getByRole('row', { name: new RegExp(checkedName.en) });
    await expect(suggested).toContainText(words.review.errors.byCheck);
    await expect(suggested.getByRole('checkbox')).not.toBeChecked();

    await take(page);
    await page.getByRole('checkbox', { name: dangerousName.en }).check();
    await decide(
      page,
      words.review.options.outcome.Rejected,
      words.review.options.status.Rejected,
      'Hold short next time.',
    );

    // ---------------------------------------------------------------- disputed by the pilot, upheld by the assistant
    await pilot.goto(`/tours/${slug}`);
    await pilot.getByRole('button', { name: words.public.disputeAction, exact: true }).click();
    const dialog = pilot.getByRole('alertdialog');
    await dialog
      .getByLabel(words.public.fields.text, { exact: true })
      .fill(`I held short: the video shows it. ${stamp}`);
    await whileWaitingFor(pilot, 'POST', `/api/flightops/reports/${first}/dispute`, async () => {
      await dialog.getByRole('button', { name: words.public.disputeAction, exact: true }).click();
    });
    await expect(pilot.getByText(words.public.disputed, { exact: true })).toBeVisible();

    await assistant.goto(`/staff/tours/review/${first}`);
    await choose(assistant, words.review.fields.outcome, words.review.options.outcome.Upheld);
    await assistant
      .getByLabel(words.review.fields.answer, { exact: true })
      .fill(`It goes back to the queue. ${stamp}`);
    await whileWaitingFor(assistant, 'POST', `/api/flightops/review/${first}/dispute`, async () => {
      await assistant.getByRole('button', { name: words.review.decideDispute }).click();
    });
    await expect(
      assistant.getByText(words.review.options.status.Queued, { exact: true }).first(),
    ).toBeVisible();

    await pilot.goto(`/tours/${slug}`);
    await expect(pilot.getByText(words.public.dispute.Upheld)).toBeVisible();

    // ---------------------------------------------------------------- taken again, and accepted
    await page.goto(`/staff/tours/review/${first}`);
    await take(page);
    await untick(page, dangerousName.en);
    await decide(page, words.review.options.outcome.Accepted, words.review.options.status.Accepted);

    // ---------------------------------------------------------------- 5. the second leg: rejected by mistake, reopened, accepted
    made.flight.remove();
    made.flight = replayFlight({
      vid: pilotVid,
      departure: benchAirports.milan,
      arrival: benchAirports.bari,
      hoursAgo: 20,
    });
    const second = await report(pilot, pilotContext.context.request, tourId, legs[1]!);

    await page.goto(`/staff/tours/review/${second}`);
    await take(page);
    await page.getByRole('checkbox', { name: dangerousName.en }).check();
    await decide(
      page,
      words.review.options.outcome.Rejected,
      words.review.options.status.Rejected,
      'Hold short.',
    );

    await page.getByRole('button', { name: words.review.reopen }).click();
    const reopen = page.getByRole('alertdialog');
    await reopen
      .getByLabel(words.review.fields.reason, { exact: true })
      .fill('The wrong report: this one held short.');
    await whileWaitingFor(page, 'POST', `/api/flightops/review/${second}/reopen`, async () => {
      await reopen.getByRole('button', { name: words.review.reopen }).click();
    });
    await expect(page.getByText(words.review.options.status.InReview, { exact: true }).first()).toBeVisible();

    await untick(page, dangerousName.en);
    await decide(page, words.review.options.outcome.Accepted, words.review.options.status.Accepted);

    // ---------------------------------------------------------------- 6. completed, and the award assigned
    await pilot.goto('/tours');
    const card = pilot.getByRole('article').filter({ hasText: tourName.en });
    await expect(card.getByText(words.progress.completed, { exact: true })).toBeVisible();

    await page.goto('/staff/awards/queue');
    const line = page.getByRole('row').filter({ hasText: stamp });
    await expect(line).toContainText(awardName.en);
    await line.getByRole('link', { name: common.awardSignals.assign }).click();
    await whileWaitingFor(page, 'POST', '/api/award-assignments', async () => {
      await page.getByRole('button', { name: common.awardAssignments.assign }).click();
    });
    await expect(page).toHaveURL(/\/staff\/awards\/queue(\?|$)/);
    await page.goto('/staff/awards/assignments');
    await expect(page.getByRole('row').filter({ hasText: stamp }).first()).toBeVisible();
  }
});

async function signedInAs(
  browser: { newContext: (options: { baseURL: string }) => Promise<BrowserContext> },
  as: 'pilot' | 'assistant',
): Promise<{ context: BrowserContext; vid: number }> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);
  const signedIn = await context.request.post(`/e2e/signin?as=${as}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  return { context, vid: ((await signedIn.json()) as { vid: number }).vid };
}

function idOf(page: Page): number {
  return Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);
}

async function created(request: APIRequestContext, path: string, data: unknown): Promise<number> {
  const response = await request.post(path, { headers: asTheClientDoes, data });
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: number }).id;
}

async function legsOf(request: APIRequestContext, tourId: number): Promise<number[]> {
  const response = await request.get(`/api/flightops/tours/${tourId}/legs`);
  expect(response.status(), await response.text()).toBe(200);
  return ((await response.json()) as { legs: { id: number; number: number }[] }).legs
    .sort((one, other) => one.number - other.number)
    .map((leg) => leg.id);
}

/** The pilot reports the flight the tracker has on this leg, from the report page the tour's page links; its identifier. */
async function report(
  pilot: Page,
  request: APIRequestContext,
  tourId: number,
  legId: number,
): Promise<number> {
  await pilot.goto(`/tours/${slug}/report?leg=${legId}`);
  const choice = pilot.getByRole('radio', { name: /ICARG/ });
  await expect(choice).toBeVisible();
  await choice.check();
  await whileWaitingFor(pilot, 'POST', `/api/flightops/tours/${tourId}/reports`, async () => {
    await pilot.getByRole('button', { name: words.report.send }).click();
  });
  await expect(pilot.getByText(words.public.reportStatus.Queued, { exact: true }).first()).toBeVisible();

  const mine = await request.get(`/api/flightops/tours/${tourId}/reports/mine`);
  const reports = ((await mine.json()) as { reports: { id: number; legId: number }[] }).reports;
  return reports.find((one) => one.legId === legId)!.id;
}

async function take(page: Page): Promise<void> {
  await whileWaitingFor(page, 'POST', '/take', async () => {
    await page.getByRole('button', { name: words.review.take }).click();
  });
  await expect(page.getByText(words.review.options.status.InReview, { exact: true }).first()).toBeVisible();
}

/** An error the last decision confirmed comes back ticked: this decision does not confirm it. */
async function untick(page: Page, error: string): Promise<void> {
  const box = page.getByRole('checkbox', { name: error });
  if (await box.isChecked()) {
    await box.uncheck();
  }
}

async function decide(page: Page, outcome: string, status: string, note?: string): Promise<void> {
  await choose(page, words.review.fields.outcome, outcome);
  if (note !== undefined) {
    await page.getByLabel(words.review.fields.noteToPilot, { exact: true }).fill(note);
  }
  await whileWaitingFor(page, 'POST', '/decide', async () => {
    await page.getByRole('button', { name: words.review.decide }).click();
  });
  await expect(page.getByText(status, { exact: true }).first()).toBeVisible();
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
