import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, request as playwrightRequest, test, type APIRequestContext } from '@playwright/test';

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
 * The "done when" of T19 (M2, T19b; note 2026-09-24-il-contratto-dell-agente), through the real screens against the real
 * server: the coordinator makes a token for the validator's agent on their own page, a program with nothing but that token
 * — what `curl` would do — reads the queue and the report, and sends that the semicircular levels failed; the validation
 * page shows the agent's result, whose agent and which version, and the error it suggests.
 *
 * Two people, because nobody validates their own reports: the bench's pilot sends the report of the recorded flight, the
 * bench's coordinator validates. The token is revoked at the end; the tour is hidden, as every round with a report leaves it.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  public: { sendReport: string; reportStatus: { Queued: string } };
  report: { send: string };
  review: { errors: { byCheck: string }; checks: { byAgentOf: string } };
  tokenAudiences: { agent: string };
};
const common = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/common.json', import.meta.url)), 'utf8'),
) as {
  tokens: { create: string; fields: { name: string; audience: string }; issued: { label: string } };
};

const stamp = Date.now().toString(36);
const slug = `bench-agent-${stamp}`;
const tourName = { en: `Bench agent ${stamp}`, it: `Agente del banco ${stamp}` };
const errorName = { en: `Bench semicircular ${stamp}`, it: `Semicircolari del banco ${stamp}` };
const tokenName = `bench agent ${stamp}`;
const evidence = `DCT ELB to SRN, magnetic track 332, FL360 even: wrong (${stamp})`;

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const NEW_ROW = '0001-01-01T00:00:00';

test('an agent with a token from the member’s page reads a report and its result reaches the validation page', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(180_000);
  await readInEnglish(context);
  await signIn(context);

  const pilotContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(pilotContext);
  const signedIn = await pilotContext.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  const pilotVid = ((await signedIn.json()) as { vid: number }).vid;

  const flight = replayFlight({ vid: pilotVid, departure: benchAirports.rome, arrival: benchAirports.milan });
  const made: {
    rules: number[];
    errors: number[];
    tokens: number[];
    programs: APIRequestContext[];
    report: { id: number } | null;
  } = { rules: [], errors: [], tokens: [], programs: [], report: null };

  await removeBenchTours(context, 'bench-agent-');
  try {
    await run();
  } finally {
    for (const program of made.programs) {
      await program.dispose();
    }
    flight.remove();
    // The pilot takes the report back: a queued one counts towards the division's daily limit of the bench's pilot, and
    // the other rounds report the same day's flight.
    if (made.report !== null) {
      const report = `/api/flightops/reports/${made.report.id}`;
      const mine = await pilotContext.request.get(report);
      if (mine.ok()) {
        const rowVersion = ((await mine.json()) as { rowVersion: string }).rowVersion;
        await pilotContext.request.post(`${report}/withdraw`, {
          headers: asTheClientDoes,
          data: { rowVersion },
        });
      }
    }
    for (const token of made.tokens) {
      await context.request.post(`/api/me/tokens/${token}/revoke`, { headers: asTheClientDoes });
    }
    for (const rule of made.rules) {
      await context.request.delete(`/api/flightops/rules/${rule}`, { headers: asTheClientDoes });
    }
    for (const error of made.errors) {
      await context.request.delete(`/api/flightops/errors/${error}`, { headers: asTheClientDoes });
    }
    await removeBenchTours(context, 'bench-agent-');
    await pilotContext.close();
  }

  async function run() {
    // ---------------------------------------------------------------- a tour whose rule asks for the agent's check
    const tourId = await releasedTourWithOneLeg(page, context, { name: tourName, slug });
    const error = await created(context.request, '/api/flightops/errors', {
      name: errorName,
      description: {
        en: 'Cruised at a level of the wrong parity.',
        it: 'Livello di crociera di parità sbagliata.',
      },
      examples: null,
      category: 'Warning',
      yearlyMax: 3,
      checkKey: 'semicircularLevels',
      isPublic: false,
      retired: false,
      rowVersion: NEW_ROW,
    });
    made.errors.push(error);
    made.rules.push(
      await created(context.request, '/api/flightops/rules', {
        tourId,
        code: `BA${stamp.slice(-6).toUpperCase()}`,
        title: { en: 'Semicircular levels', it: 'Livelli semicircolari' },
        text: { en: 'Odd eastbound, even westbound.', it: 'Dispari verso est, pari verso ovest.' },
        amendsRuleId: null,
        checkKey: 'semicircularLevels',
        parameters: null,
        errorIds: [error],
        sort: 0,
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

    // ---------------------------------------------------------------- the token, made on the coordinator's own page
    await page.goto('/me/tokens');
    await page.getByLabel(common.tokens.fields.name, { exact: true }).fill(tokenName);
    await choose(page, common.tokens.fields.audience, words.tokenAudiences.agent);
    await whileWaitingFor(page, 'POST', '/api/me/tokens', async () => {
      await page.getByRole('button', { name: common.tokens.create }).click();
    });
    const text = await page.getByLabel(common.tokens.issued.label).inputValue();
    expect(text).toMatch(/^hubpat_/);

    const listed = await context.request.get(`/api/me/tokens?pageSize=100`);
    const mine = ((await listed.json()) as { items: { id: number; name: string }[] }).items;
    made.tokens.push(mine.find((row) => row.name === tokenName)!.id);

    // ---------------------------------------------------------------- the program: nothing but the token, as curl
    const agent = await playwrightRequest.newContext({
      baseURL: benchUrl,
      extraHTTPHeaders: { Authorization: `Bearer ${text}`, 'Hub-Agent-Contract': '1' },
    });
    made.programs.push(agent);

    const queue = await agent.get('/api/flightops/agent/pireps?pending=true');
    expect(queue.status(), await queue.text()).toBe(200);
    const item = ((await queue.json()) as { id: number; tourId: number; agentChecks: string[] }[]).find(
      (row) => row.tourId === tourId,
    );
    expect(item?.agentChecks).toEqual(['semicircularLevels']);
    made.report = { id: item!.id };

    const read = await agent.get(`/api/flightops/agent/pireps/${item!.id}`);
    expect(read.status(), await read.text()).toBe(200);
    const report = (await read.json()) as { flights: { plans: unknown[]; track: unknown[] | null }[] };
    expect(report.flights[0]!.plans.length).toBeGreaterThan(0);
    expect(report.flights[0]!.track?.length ?? 0).toBeGreaterThan(0);

    const sent = await agent.post(`/api/flightops/agent/pireps/${item!.id}/checks`, {
      data: {
        agentVersion: 'e2e',
        results: [{ checkKey: 'semicircularLevels', outcome: 'Failed', evidence: [evidence] }],
      },
    });
    expect(sent.status(), await sent.text()).toBe(200);
    expect(((await sent.json()) as { suggestedErrorIds: number[] }).suggestedErrorIds).toContain(error);

    // ---------------------------------------------------------------- the validation page
    await page.goto(`/staff/tours/review/${item!.id}`);
    await expect(page.getByText(evidence)).toBeVisible();
    await expect(
      page.getByText(
        words.review.checks.byAgentOf.replace('{{name}}', 'Bench Coordinator').replace('{{version}}', 'e2e'),
      ),
    ).toBeVisible();
    const suggested = page.getByRole('row', { name: new RegExp(errorName.en) });
    await expect(suggested).toContainText(words.review.errors.byCheck);
    await expect(suggested.getByRole('checkbox')).not.toBeChecked();
  }
});

async function created(request: APIRequestContext, path: string, data: unknown): Promise<number> {
  const response = await request.post(path, { headers: asTheClientDoes, data });
  expect(response.status(), await response.text()).toBe(201);
  return ((await response.json()) as { id: number }).id;
}
