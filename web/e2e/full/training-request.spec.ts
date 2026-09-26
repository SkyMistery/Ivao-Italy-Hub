import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type BrowserContext, type Page } from '@playwright/test';

import { readInEnglish, whileWaitingFor } from './bench';

/**
 * The "done when" of A6 (M3), through the real screens against the real server: the bench's trainee asks for the training
 * of the rating after theirs on the ATC ladder, choosing a position among the ones the division offers, answers «yes» to the
 * question on the theory, and finds the request in their trainings; a second ATC request is refused — the page offers none,
 * and the server refuses one sent directly —, and a pilot's passes. The ratings and the positions are the server's: the spec
 * writes none of its own.
 *
 * The trainee is the pilot of the tours (`/e2e/signin?as=pilot`, AS3 and FS3, hours above any threshold); the bench has no
 * threshold of hours and no site of the exam.
 *
 * ⚠️ A training is never deleted: the requests of this run are cancelled in a `finally`, and the ones a run that stopped half
 * way left waiting are cancelled at the start. A cancellation makes nobody wait, so the next run asks again.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stamp = Date.now().toString(36);

interface MyTraining {
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly ratingShortName: string | null;
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly positions: readonly { readonly callsign: string; readonly name: string }[];
    readonly refusal: string | null;
  }[];
  readonly trainings: readonly {
    readonly id: number;
    readonly kind: string;
    readonly state: string;
    readonly position: string | null;
    readonly availabilityText: string | null;
    readonly rowVersion: string;
  }[];
}

test('the trainee asks for the next ATC training on a position and finds it; a second ATC one is refused, a pilot one passes', async ({
  page,
  context,
}) => {
  test.setTimeout(120_000);
  await readInEnglish(context);
  const signedIn = await context.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  const complaints: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error' && !message.text().includes('favicon')) {
      complaints.push(message.text());
    }
  });
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await cancelWaitingRequests(context);
  try {
    const before = await mine(context);
    const atc = before.paths.find((path) => path.kind === 'Atc')!;
    const pilot = before.paths.find((path) => path.kind === 'Pilot')!;
    expect(atc.refusal, 'the trainee may ask for an ATC training').toBeNull();
    expect(pilot.refusal, 'the trainee may ask for a pilot training').toBeNull();
    const position = atc.positions[0];
    expect(position, 'the division offers a position for the ATC training').toBeDefined();

    // ---------------------------------------------------------------- the ATC request, on a position
    await page.goto('/training/request');
    await expect(page.getByRole('heading', { level: 1, name: words.request.title })).toBeVisible();
    const article = page.getByRole('article');
    await expect(article.getByText(atc.ratingShortName!, { exact: true })).toBeVisible();

    // ⚠️ Chosen from the list: the closed suggestion of the core loses a choice clicked after typing (found in A6b).
    await page.getByLabel(words.request.fields.position, { exact: true }).click();
    await page.getByRole('option', { name: `${position!.callsign} — ${position!.name}` }).click();
    await page
      .getByLabel(words.request.fields.availabilityText, { exact: true })
      .fill(`Evenings after 18 UTC, run ${stamp}.`);
    await askAnswering(page, atc.next!.shortName, words.request.theory.yes);

    await expect(page).toHaveURL(/\/training\/mine$/);
    await expect(page.getByRole('heading', { level: 1, name: words.mine.title })).toBeVisible();
    const waiting = page.getByRole('listitem').filter({ hasText: words.states.Requested });
    await expect(waiting.filter({ hasText: position!.callsign })).toHaveCount(1);

    // ---------------------------------------------------------------- a second ATC request is refused
    await page.goto('/training/request?kind=Atc');
    await expect(page.getByText(words.errors.requestOpen)).toBeVisible();
    await expect(page.getByRole('button', { name: words.request.send, exact: true })).toHaveCount(0);

    // And the server refuses one sent past the page, on the ladder, with the key the page says.
    const second = await context.request.post('/api/training/mine', {
      headers: asTheClientDoes,
      data: {
        kind: 'Atc',
        rating: atc.next!.number,
        position: position!.callsign,
        availabilityText: null,
        notesText: null,
        theoryPassed: true,
      },
    });
    expect(second.status()).toBe(400);
    expect(((await second.json()) as { errors: Record<string, string[]> }).errors.kind).toEqual([
      'training:errors.requestOpen',
    ]);

    // ---------------------------------------------------------------- a pilot's passes: the other ladder, no position
    await page.getByRole('radio', { name: new RegExp(words.kinds.Pilot) }).check();
    await expect(page).toHaveURL(/\/training\/request\?kind=Pilot$/);
    await expect(article.getByText(pilot.ratingShortName!, { exact: true })).toBeVisible();
    await expect(page.getByLabel(words.request.fields.position, { exact: true })).toHaveCount(0);
    await askAnswering(page, pilot.next!.shortName, words.request.theory.yes);

    await expect(page).toHaveURL(/\/training\/mine$/);
    await expect(waiting).toHaveCount(2);

    // Read back from the server: one request per ladder, the ATC one on the position chosen, with what the trainee wrote.
    const after = (await mine(context)).trainings.filter((training) => training.state === 'Requested');
    expect(after.map((training) => training.kind).sort()).toEqual(['Atc', 'Pilot']);
    expect(after.find((training) => training.kind === 'Atc')).toMatchObject({
      position: position!.callsign,
      availabilityText: `Evenings after 18 UTC, run ${stamp}.`,
    });
    expect(after.find((training) => training.kind === 'Pilot')?.position).toBeNull();

    expect(complaints).toEqual([]);
  } finally {
    await cancelWaitingRequests(context);
  }
});

/** «Request training», the question on the theory for that rating, the answer, and the request it sends. */
async function askAnswering(page: Page, rating: string, answer: string): Promise<void> {
  await page.getByRole('button', { name: words.request.send, exact: true }).click();
  const question = page.getByRole('alertdialog');
  await expect(question.getByText(words.request.theory.question.replace('{{rating}}', rating))).toBeVisible();
  await question.getByRole('radio', { name: answer }).check();
  await whileWaitingFor(page, 'POST', '/api/training/mine', async () => {
    await question.getByRole('button', { name: words.request.theory.confirm }).click();
  });
}

async function mine(context: BrowserContext): Promise<MyTraining> {
  const response = await context.request.get('/api/training/mine');
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as MyTraining;
}

/**
 * The trainee's requests still waiting, cancelled at the version they have: this run's, or what a run that stopped half way
 * left. Nobody accepts a request on the bench before A7, so waiting is the only open state one can be in.
 */
async function cancelWaitingRequests(context: BrowserContext): Promise<void> {
  for (const training of (await mine(context)).trainings.filter((row) => row.state === 'Requested')) {
    const cancelled = await context.request.post(`/api/training/mine/${String(training.id)}/cancel`, {
      headers: asTheClientDoes,
      data: { rowVersion: training.rowVersion },
    });
    expect(cancelled.status(), await cancelled.text()).toBe(200);
  }
}

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    kinds: { Atc: string; Pilot: string };
    states: { Requested: string };
    request: {
      title: string;
      send: string;
      fields: { position: string; availabilityText: string };
      theory: { question: string; yes: string; confirm: string };
    };
    mine: { title: string };
    errors: { requestOpen: string };
  };
}
