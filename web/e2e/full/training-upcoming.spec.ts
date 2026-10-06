import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type BrowserContext, type Locator } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, readInEnglish, test } from './bench';

/**
 * The "done when" of A10b (M3), through the real screens against the real server: a training of the bench's trainee is dated, and a
 * visitor reads it on `/training` — its rating, position, date and time, and nobody's VID or name —, follows its entry of the calendar
 * to the page of its session, which says the same; signed in, the page of the session and `/training` say who, by name and VID. On the
 * way the blocks answer for whoever asks, as the server answers them: the request waits in the staff's queue, the assigned training
 * in its trainer's, the trainee's own block names it, and a personal block tells a visitor only that they are not signed in. Closed at
 * the end, the session leaves `/training` and its page is not found.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`), the trainer `?as=trainer`, the staff the bench's web master. ⚠️ Its
 * name sorts after every other round of the training: Playwright runs the files in the order of their names, one worker. After the
 * round of A10a the bench's trainee has both ladders free — the pilot one with a mock exam next —, and this one asks for an ATC training
 * of its own and closes it, so it runs alone too.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const BENCH_TRAINER = 999004;
const REASON = 'trn-test: closed by the round of the sessions.';

/** What names the bench's trainee and trainer: their names and their VIDs, which a visitor never reads. */
const PEOPLE = ['Bench Pilot', 'Bench Trainer', '999002', '999004'];

interface MyTraining {
  readonly asksTheory: boolean;
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly asksPosition: boolean;
    readonly positions: readonly { readonly callsign: string }[];
    readonly refusal: string | null;
    readonly openTrainingId: number | null;
  }[];
}

interface StaffTraining {
  readonly state: string;
  readonly ratingShortName: string | null;
  readonly position: string | null;
  readonly rowVersion: string;
}

interface Row {
  readonly id: number;
}

test('a visitor reads the sessions to come without names, the calendar leads to the page of one, and signed in it says who', async ({
  context,
  browser,
  afterwards,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  const trainer = await browser.newContext({ baseURL: benchUrl });
  const visitor = await browser.newContext({ baseURL: benchUrl });
  for (const reader of [trainee, trainer, visitor]) {
    await readInEnglish(reader);
  }

  // What this run opens, so that the end closes it whatever happens in between.
  const opened: number[] = [];

  afterwards(async () => {
    // Whatever this run left open is closed by the staff, or taken back by the trainee while nobody accepted it.
    for (const opening of opened) {
      await closeIfOpen(context.request, trainee.request, opening);
    }

    await trainee.close();
    await trainer.close();
    await visitor.close();
  });

  await signIn(context, null);
  // In the roster before anybody assigns them a training: the staff of the training is whoever signed in once.
  await signIn(trainer, 'trainer');
  await signIn(trainee, 'pilot');

  // A personal block tells a visitor nothing but that they are not signed in.
  for (const type of ['training.myTraining', 'training.trainerQueue', 'training.approvalQueue']) {
    expect(await block(visitor.request, type), type).toEqual({ signedIn: false });
  }

  // ---------------------------------------------------------------- asked for: in the staff's queue
  const id = await requested(trainee.request, opened);
  const queue = (await block(context.request, 'training.approvalQueue')) as {
    toApprove: { count: number; oldest: Row[] };
  };
  expect(queue.toApprove.oldest.map((row) => row.id)).toContain(id);

  // ---------------------------------------------------------------- assigned: in the trainer's queue, dates to propose
  await step(context.request, id, 'accept', {});
  await step(context.request, id, 'assign', { trainerVid: BENCH_TRAINER });
  // In the session they signed in with at the start: an assignment changes nothing of theirs (A7b).
  const theirs = (await block(trainer.request, 'training.trainerQueue')) as { toPropose: Row[] };
  expect(theirs.toPropose.map((row) => row.id)).toContain(id);

  // ---------------------------------------------------------------- dated by hand, three days ahead
  const day = daysAhead(3);
  const dated = await step(context.request, id, 'date', {
    startsAtUtc: `${day}T17:00:00Z`,
    confirmed: true,
  });
  const title = `${dated.ratingShortName!} · ${dated.position!}`;

  const own = (await block(trainee.request, 'training.myTraining')) as MyTraining;
  expect(own.paths.find((path) => path.kind === 'Atc')?.openTrainingId).toBe(id);

  // ---------------------------------------------------------------- /training, to a visitor: where and when, and nobody
  const visitorPage = await visitor.newPage();
  visitorPage.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await visitorPage.goto('/training');
  await expect(visitorPage.getByRole('heading', { level: 1, name: words.public.title })).toBeVisible();
  const link = visitorPage.getByRole('link', { name: title, exact: true });
  await expect(link).toHaveAttribute('href', `/training/sessions/${String(id)}`);
  await expect(visitorPage.getByRole('link', { name: words.request.send })).toHaveAttribute(
    'href',
    '/training/request',
  );
  await assertNobody(visitorPage.locator('body'));

  // The block says the same, from the real server.
  const sessions = (await block(visitor.request, 'training.upcomingSessions')) as {
    signedIn: boolean;
    items: { id: number; trainee: unknown; trainer: unknown }[];
  };
  expect(sessions.signedIn).toBe(false);
  const item = sessions.items.find((entry) => entry.id === id);
  expect(item, 'the session is in the block').toBeDefined();
  expect([item?.trainee, item?.trainer]).toEqual([null, null]);

  // ---------------------------------------------------------------- the entry of the calendar leads to the page of the session
  await visitorPage.goto(`/calendar?view=weekList&on=${day}`);
  const entry = visitorPage.locator(`a[href="/training/sessions/${String(id)}"]`);
  await expect(entry).toHaveText(title);
  await entry.click();
  await expect(visitorPage).toHaveURL(new RegExp(`/training/sessions/${String(id)}$`));
  await expect(
    visitorPage.getByRole('heading', { level: 1, name: filled(words.public.session.title, { title }) }),
  ).toBeVisible();
  await expect(visitorPage.locator('dl').getByText(dated.position!, { exact: true })).toBeVisible();
  await expect(visitorPage.locator('dl').getByText(/UTC$/)).toBeVisible();
  await expect(visitorPage.getByRole('link', { name: words.public.session.signIn })).toBeVisible();
  await assertNobody(visitorPage.locator('body'));

  // ---------------------------------------------------------------- signed in: who, by name and VID
  const trainerPage = await trainer.newPage();
  await trainerPage.goto(`/training/sessions/${String(id)}`);
  const details = trainerPage.locator('dl');
  await expect(details.getByText('Bench Pilot (999002)', { exact: true })).toBeVisible();
  await expect(details.getByText('Bench Trainer (999004)', { exact: true })).toBeVisible();
  await expect(trainerPage.getByRole('link', { name: words.public.session.signIn })).toHaveCount(0);

  await trainerPage.goto('/training');
  await expect(
    trainerPage.getByText(
      filled(words.public.people, { trainee: 'Bench Pilot (999002)', trainer: 'Bench Trainer (999004)' }),
    ),
  ).toBeVisible();

  // ---------------------------------------------------------------- closed: the session leaves /training, and its page
  await step(context.request, id, 'close', { reason: REASON });
  await visitorPage.goto('/training');
  await expect(visitorPage.getByRole('heading', { level: 2, name: words.public.upcoming })).toBeVisible();
  await expect(visitorPage.getByRole('link', { name: title, exact: true })).toHaveCount(0);
  await visitorPage.goto(`/training/sessions/${String(id)}`);
  await expect(visitorPage.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
});

/** What a visitor reads names neither the trainee nor the trainer, by name or by VID. */
async function assertNobody(where: Locator): Promise<void> {
  const shown = await where.innerText();
  for (const somebody of PEOPLE) {
    expect(shown, `a visitor reads nobody's name nor VID: ${somebody}`).not.toContain(somebody);
  }
}

async function signIn(context: BrowserContext, as: 'pilot' | 'trainer' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

/** A day some days ahead, in UTC, as `YYYY-MM-DD`: far enough that no date of the run goes by while it runs. */
function daysAhead(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
}

/** What a block answers its reader, live, with no property: the answer the page it sits on would draw. */
async function block(request: APIRequestContext, type: string): Promise<unknown> {
  const response = await request.get(`/api/blocks/data/${type}`);
  expect(response.status(), `${type}: ${await response.text()}`).toBe(200);
  return (await response.json()) as unknown;
}

/** An ATC training of the trainee's asked for through the API, as A6's page sends it; its id. */
async function requested(trainee: APIRequestContext, opened: number[]): Promise<number> {
  const response = await trainee.get('/api/training/mine');
  expect(response.status(), await response.text()).toBe(200);
  const standing = (await response.json()) as MyTraining;
  const path = standing.paths.find((candidate) => candidate.kind === 'Atc')!;
  expect(path.refusal, 'the trainee may ask for an ATC training').toBeNull();

  const asked = await trainee.post('/api/training/mine', {
    headers: asTheClientDoes,
    data: {
      kind: 'Atc',
      rating: path.next!.number,
      position: path.positions[0]!.callsign,
      availabilityText: null,
      notesText: null,
      theoryPassed: standing.asksTheory ? true : null,
    },
  });
  expect(asked.status(), await asked.text()).toBe(201);
  const id = ((await asked.json()) as { id: number }).id;
  opened.push(id);
  return id;
}

/** A step of the staff on a training, through the API, at the version there is now: the page as it is afterwards. */
async function step(
  staff: APIRequestContext,
  id: number,
  verb: string,
  body: Record<string, unknown>,
): Promise<StaffTraining> {
  const now = await staff.get(`/api/training/trainings/${String(id)}`);
  expect(now.status(), await now.text()).toBe(200);
  const { rowVersion } = (await now.json()) as StaffTraining;

  const answer = await staff.post(`/api/training/trainings/${String(id)}/${verb}`, {
    headers: asTheClientDoes,
    data: { ...body, rowVersion },
  });
  expect(answer.status(), `${verb}: ${await answer.text()}`).toBe(200);
  return (await answer.json()) as StaffTraining;
}

/** A training this run opened, closed by the staff while it goes on, or taken back by its trainee while nobody accepted it. */
async function closeIfOpen(staff: APIRequestContext, trainee: APIRequestContext, id: number): Promise<void> {
  const now = await staff.get(`/api/training/trainings/${String(id)}`);
  if (now.status() !== 200) {
    return;
  }

  const training = (await now.json()) as StaffTraining;
  if (training.state === 'Requested') {
    const cancelled = await trainee.post(`/api/training/mine/${String(id)}/cancel`, {
      headers: asTheClientDoes,
      data: { rowVersion: training.rowVersion },
    });
    expect(cancelled.status(), await cancelled.text()).toBe(200);
  } else if (['Accepted', 'Assigned', 'Scheduled'].includes(training.state)) {
    await step(staff, id, 'close', { reason: REASON });
  }
}

/** A sentence of the language file with its values in. */
function filled(sentence: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);
}

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    request: { send: string };
    public: {
      title: string;
      upcoming: string;
      people: string;
      session: { title: string; signIn: string };
    };
  };
}
