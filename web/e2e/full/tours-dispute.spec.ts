import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext } from '@playwright/test';

import { englishCommon } from '../locales';

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
 * The "done when" of T14 (M2), through the real screens against the real server, and the round the plan asks for —
 * «disputed and reopened»: the coordinator rejects a pilot's report; the pilot disputes it from the tour's page; the
 * coordinator, who decided it and so takes part in the thread, answers from the back office; the pilot receives the answer in
 * Mailpit, without the name of who wrote it, and answers back from `/me/contacts`; the assistant coordinator of the FOD —
 * the bench's third person, `?as=assistant`, because whoever decided a report does not judge its dispute — upholds it, and
 * the report is back in the queue.
 *
 * The report is taken and rejected through the API: the screens of the validation are T13b's round. Like the other rounds
 * about reports it leaves one hidden tour behind (§1.2.2); the rule and its error are deleted.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  public: {
    sendReport: string;
    disputeAction: string;
    disputeThread: string;
    disputed: string;
    fields: { text: string };
    reportStatus: { Queued: string };
    dispute: { Upheld: string };
  };
  report: { send: string };
  review: {
    decideDispute: string;
    fields: { outcome: string; answer: string };
    options: { outcome: { Upheld: string }; status: { Queued: string }; disputeStatus: { Open: string } };
  };
};

const common = englishCommon as unknown as {
  contacts: { thread: { send: string; fields: { body: string } } };
};

const pilotAddress = 'bench-pilot@bench.test';

const stamp = Date.now().toString(36);
const slug = `bench-dispute-${stamp}`;
const tourName = { en: `Bench dispute ${stamp}`, it: `Contestazione del banco ${stamp}` };
const errorName = { en: `Bench wrong SID ${stamp}`, it: `SID sbagliata del banco ${stamp}` };
const ruleCode = `BD${stamp.slice(-6).toUpperCase()}`;

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const NEW_ROW = '0001-01-01T00:00:00';

test('a pilot disputes a rejection, the department answers in the thread, and the dispute upheld sends it back to the queue', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(300_000);
  await readInEnglish(context);
  await signIn(context);

  const pilotContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(pilotContext);
  const signedIn = await pilotContext.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  const pilotVid = ((await signedIn.json()) as { vid: number }).vid;

  const assistantContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(assistantContext);
  const assistantIn = await assistantContext.request.post('/e2e/signin?as=assistant');
  expect(assistantIn.status(), await assistantIn.text()).toBe(200);

  const flight = replayFlight({ vid: pilotVid, departure: benchAirports.rome, arrival: benchAirports.milan });
  const made: { rule?: number; error?: number } = {};

  await removeBenchTours(context, 'bench-dispute-');
  try {
    await dispute();
  } finally {
    flight.remove();
    if (made.rule !== undefined) {
      await context.request.delete(`/api/flightops/rules/${made.rule}`, { headers: asTheClientDoes });
    }
    if (made.error !== undefined) {
      await context.request.delete(`/api/flightops/errors/${made.error}`, { headers: asTheClientDoes });
    }
    await removeBenchTours(context, 'bench-dispute-');
    await assistantContext.close();
    await pilotContext.close();
  }

  async function dispute() {
    // ---------------------------------------------------------------- a tour with one leg and a rule with a dangerous error
    const tourId = await releasedTourWithOneLeg(page, context, { name: tourName, slug });
    made.error = await created(context.request, '/api/flightops/errors', {
      name: errorName,
      description: {
        en: 'Flew a SID the chart does not have.',
        it: 'Ha volato una SID che la carta non ha.',
      },
      examples: null,
      category: 'Dangerous',
      yearlyMax: null,
      checkKey: null,
      isPublic: false,
      retired: false,
      rowVersion: NEW_ROW,
    });
    made.rule = await created(context.request, '/api/flightops/rules', {
      tourId,
      code: ruleCode,
      title: { en: 'Departures', it: 'Partenze' },
      text: { en: 'Fly the SID you are cleared for.', it: 'Vola la SID autorizzata.' },
      amendsRuleId: null,
      checkKey: null,
      parameters: null,
      errorIds: [made.error],
      sort: 0,
      retired: false,
      rowVersion: NEW_ROW,
    });

    // ---------------------------------------------------------------- the pilot sends the report, the coordinator rejects it
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

    const mine = await pilotContext.request.get(`/api/flightops/tours/${tourId}/reports/mine`);
    const reportId = ((await mine.json()) as { reports: { id: number }[] }).reports[0]!.id;
    await reject(context.request, reportId, made.error);

    // ---------------------------------------------------------------- the pilot disputes it from the tour's page
    await pilotPage.goto(`/tours/${slug}`);
    const disputeButton = pilotPage.getByRole('button', { name: words.public.disputeAction, exact: true });
    await expect(disputeButton).toBeVisible();
    await disputeButton.click();
    const dialog = pilotPage.getByRole('alertdialog');
    await expect(dialog.getByLabel(words.public.fields.text, { exact: true })).toBeVisible();
    await dialog
      .getByLabel(words.public.fields.text, { exact: true })
      .fill(`The chart had that SID. ${stamp}`);
    await whileWaitingFor(pilotPage, 'POST', `/api/flightops/reports/${reportId}/dispute`, async () => {
      await dialog.getByRole('button', { name: words.public.disputeAction, exact: true }).click();
    });
    await expect(pilotPage.getByText(words.public.disputed)).toBeVisible();

    // The thread it opened, from the report.
    await pilotPage.getByRole('link', { name: words.public.disputeThread, exact: true }).click();
    await expect(pilotPage).toHaveURL(/\/me\/contacts\/\d+$/);
    const threadId = /\/me\/contacts\/(\d+)$/.exec(pilotPage.url())![1]!;
    await expect(pilotPage.getByText(`The chart had that SID. ${stamp}`)).toBeVisible();

    // ---------------------------------------------------------------- the coordinator, who decided it, answers
    await page.goto(`/staff/tours/review/${reportId}`);
    await expect(page.getByText(words.review.options.disputeStatus.Open, { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: words.review.decideDispute })).toHaveCount(0);

    await page.goto(`/staff/fod/contacts/${threadId}`);
    await page
      .getByLabel(common.contacts.thread.fields.body, { exact: true })
      .fill(`We are looking at the chart of that day. ${stamp}`);
    await whileWaitingFor(page, 'POST', `/api/contacts/${threadId}/replies`, async () => {
      await page.getByRole('button', { name: common.contacts.thread.send, exact: true }).click();
    });

    // ---------------------------------------------------------------- the mail, in Mailpit, without who wrote it
    let mail: { Subject: string; Text: string } | null = null;
    await expect
      .poll(
        async () => {
          // The answer, not the rejection: its subject is the thread's, in the pilot's language, behind a «Re:».
          mail =
            (await mailFor(pilotContext.request, pilotAddress, `Re: Dispute: ${tourName.en}`)) ??
            (await mailFor(pilotContext.request, pilotAddress, `Re: Contestazione: ${tourName.it}`));
          return mail !== null;
        },
        { timeout: 150_000, intervals: [5_000] },
      )
      .toBe(true);
    expect(mail!.Text).toContain(`We are looking at the chart of that day. ${stamp}`);
    expect(mail!.Text).toContain(`/me/contacts/${threadId}`);
    expect(mail!.Text).not.toContain('999001');

    // ---------------------------------------------------------------- the pilot answers back from their messages
    await pilotPage.goto(`/me/contacts/${threadId}`);
    await expect(pilotPage.getByText(`We are looking at the chart of that day. ${stamp}`)).toBeVisible();
    await pilotPage
      .getByLabel(common.contacts.thread.fields.body, { exact: true })
      .fill('Thank you, here is the chart.');
    await whileWaitingFor(pilotPage, 'POST', `/api/contacts/${threadId}/replies`, async () => {
      await pilotPage.getByRole('button', { name: common.contacts.thread.send, exact: true }).click();
    });
    await expect(pilotPage.getByText('Thank you, here is the chart.')).toBeVisible();

    // ---------------------------------------------------------------- the assistant upholds it: back in the queue
    const assistant = await assistantContext.newPage();
    await assistant.goto(`/staff/tours/review/${reportId}`);
    await choose(assistant, words.review.fields.outcome, words.review.options.outcome.Upheld);
    await assistant
      .getByLabel(words.review.fields.answer, { exact: true })
      .fill(`You were right: it goes back to the queue. ${stamp}`);
    await whileWaitingFor(assistant, 'POST', `/api/flightops/review/${reportId}/dispute`, async () => {
      await assistant.getByRole('button', { name: words.review.decideDispute }).click();
    });
    await expect(
      assistant.getByText(words.review.options.status.Queued, { exact: true }).first(),
    ).toBeVisible();

    await pilotPage.goto(`/tours/${slug}`);
    await expect(pilotPage.getByText(words.public.dispute.Upheld)).toBeVisible();
    await expect(pilotPage.getByText(words.public.reportStatus.Queued, { exact: true })).toBeVisible();
  }
});

async function created(request: APIRequestContext, path: string, data: unknown): Promise<number> {
  const response = await request.post(path, { headers: asTheClientDoes, data });
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: number }).id;
}

/** The coordinator takes the report and rejects it on the dangerous error, as T13b's round does through the screens. */
async function reject(request: APIRequestContext, id: number, error: number): Promise<void> {
  const read = await request.get(`/api/flightops/review/${id}`);
  expect(read.status(), await read.text()).toBe(200);
  const taken = await request.post(`/api/flightops/review/${id}/take`, {
    headers: asTheClientDoes,
    data: { rowVersion: ((await read.json()) as { rowVersion: string }).rowVersion },
  });
  expect(taken.status(), await taken.text()).toBe(200);
  const decided = await request.post(`/api/flightops/review/${id}/decide`, {
    headers: asTheClientDoes,
    data: {
      outcome: 'Rejected',
      errorIds: [error],
      noteToPilot: 'Not the SID of the chart.',
      staffNote: null,
      overrideReason: null,
      rowVersion: ((await taken.json()) as { rowVersion: string }).rowVersion,
    },
  });
  expect(decided.status(), await decided.text()).toBe(200);
}
