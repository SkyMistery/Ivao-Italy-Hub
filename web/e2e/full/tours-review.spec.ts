import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext } from '@playwright/test';

import {
  benchAirports,
  benchUrl,
  choose,
  mailFor,
  readInEnglish,
  releasedTourWithOneLeg,
  removeBenchTours,
  signIn,
  whileWaitingFor,
} from './bench';
import { replayFlight } from './replay';

/**
 * The "done when" of T13 (M2), through the real screens against the real server: a pilot sends the report of a flight the
 * tracker has; the coordinator — who may validate every tour — finds it in the queue, takes it, ticks a dangerous error of
 * the tour's rule, reads the server's suggestion turn to a rejection, rejects it; and the pilot receives the mail, in
 * Mailpit, with the rule broken and without the name of who decided (design M2 §3.5). Since T17 the tour has a second rule
 * with an automatic check that the recorded IFR flight fails — VFR only —, and the page and the queue show what it found.
 *
 * Two people, because nobody validates their own reports: the bench's coordinator, and its pilot, signed in with
 * `/e2e/signin?as=pilot` — a member with no position and a mailbox of Mailpit (T13b). The notifications are sent by a job
 * every minute, so the mail is waited for.
 *
 * Like the report's round it leaves one hidden tour behind: a tour with a report is never deleted (§1.2.2). The rule and
 * the error it made are deleted — a decision keeps what it marked in the rules it froze, not in the catalogue.
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
    fields: { outcome: string; noteToPilot: string };
    options: {
      status: { Rejected: string; InReview: string };
      outcome: { Rejected: string };
      checks: { Accept: string };
    };
    plan: { atTakeoff: string };
    errors: { byCheck: string };
  };
  evidence: { flightRulesNotAdmitted: string };
};

const pilotAddress = 'bench-pilot@bench.test';

const stamp = Date.now().toString(36);
const slug = `bench-review-${stamp}`;
const tourName = { en: `Bench review ${stamp}`, it: `Validazione del banco ${stamp}` };
const errorName = { en: `Bench incursion ${stamp}`, it: `Incursione del banco ${stamp}` };
const ruleCode = `BR${stamp.slice(-6).toUpperCase()}`;
const checkedErrorName = { en: `Bench flight rules ${stamp}`, it: `Regole di volo del banco ${stamp}` };

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const NEW_ROW = '0001-01-01T00:00:00';

test('a validator takes a pilot’s report, rejects it with an error, and the pilot is written to', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);
  await signIn(context);

  const pilotContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(pilotContext);
  const signedIn = await pilotContext.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  const pilotVid = ((await signedIn.json()) as { vid: number }).vid;

  const flight = replayFlight({ vid: pilotVid, departure: benchAirports.rome, arrival: benchAirports.milan });
  const made: { rules: number[]; errors: number[] } = { rules: [], errors: [] };

  await removeBenchTours(context, 'bench-review-');
  try {
    await validate();
  } finally {
    flight.remove();
    for (const rule of made.rules) {
      await context.request.delete(`/api/flightops/rules/${rule}`, { headers: asTheClientDoes });
    }
    for (const error of made.errors) {
      await context.request.delete(`/api/flightops/errors/${error}`, { headers: asTheClientDoes });
    }
    await removeBenchTours(context, 'bench-review-');
    await pilotContext.close();
  }

  async function validate() {
    // ---------------------------------------------------------------- a tour with one leg and a rule with a dangerous error
    const tourId = await releasedTourWithOneLeg(page, context, { name: tourName, slug });
    const dangerous = await created(context.request, '/api/flightops/errors', {
      name: errorName,
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

    // A rule with an automatic check (T17): VFR only, which the recorded IFR flight is not; its warning is suggested.
    const warning = await created(context.request, '/api/flightops/errors', {
      name: checkedErrorName,
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

    // ---------------------------------------------------------------- the pilot sends the report
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

    // ---------------------------------------------------------------- the queue of the tour, and the report
    await page.goto(`/staff/tours/review?tour=${tourId}`);
    const row = page.getByRole('row', { name: new RegExp(`Bench Pilot \\(${pilotVid}\\)`) });
    await expect(row).toBeVisible();
    await expect(row).toContainText(words.review.options.checks.Accept);
    await row.getByRole('link', { name: words.review.open }).click();
    await expect(page).toHaveURL(/\/staff\/tours\/review\/\d+$/);

    // The recorded flight: its plans with the one at take-off marked, and the map with the track.
    await expect(page.getByText(words.review.plan.atTakeoff, { exact: true })).toBeVisible();
    await expect(page.getByTestId('route-map')).toBeVisible();

    // The checks ran at the send: flight rules failed, said in words, and the warning suggested — ticked by nobody.
    await expect(
      page.getByText(
        words.evidence.flightRulesNotAdmitted.replace('{{rules}}', 'I').replace('{{admitted}}', 'V'),
      ),
    ).toBeVisible();
    const suggested = page.getByRole('row', { name: new RegExp(checkedErrorName.en) });
    await expect(suggested).toContainText(words.review.errors.byCheck);
    await expect(suggested.getByRole('checkbox')).not.toBeChecked();

    // ---------------------------------------------------------------- taken, an error ticked, rejected
    await whileWaitingFor(page, 'POST', '/take', async () => {
      await page.getByRole('button', { name: words.review.take }).click();
    });
    await expect(page.getByText(words.review.options.status.InReview, { exact: true }).first()).toBeVisible();

    await page.getByRole('checkbox', { name: errorName.en }).check();
    await expect(page.getByTestId('review-suggestion')).toContainText(words.review.options.status.Rejected);

    await choose(page, words.review.fields.outcome, words.review.options.outcome.Rejected);
    await page.getByLabel(words.review.fields.noteToPilot, { exact: true }).fill('Hold short next time.');
    await whileWaitingFor(page, 'POST', '/decide', async () => {
      await page.getByRole('button', { name: words.review.decide }).click();
    });
    await expect(page.getByText(words.review.options.status.Rejected, { exact: true }).first()).toBeVisible();

    // ---------------------------------------------------------------- the mail, in Mailpit
    let mail: { Subject: string; Text: string } | null = null;
    await expect
      .poll(
        async () => {
          mail = await mailFor(pilotContext.request, pilotAddress, stamp);
          return mail !== null;
        },
        { timeout: 150_000, intervals: [5_000] },
      )
      .toBe(true);

    const text = mail!.Text;
    expect(text).toContain(ruleCode);
    expect(text).toContain('Hold short next time.');
    // Never who decided (§3.5).
    expect(text).not.toContain('Bench Coordinator');
    expect(text).not.toContain('999001');
  }
});

async function created(request: APIRequestContext, path: string, data: unknown): Promise<number> {
  const response = await request.post(path, { headers: asTheClientDoes, data });
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: number }).id;
}
