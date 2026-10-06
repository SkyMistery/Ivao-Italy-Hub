import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type BrowserContext, type Locator } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, readInEnglish, test } from './bench';

/**
 * The "done when" of A10c (M3), through the real screens against the real server: an exam is entered from the form of the staff — its
 * rating, its position, when, the candidate by VID, and its examiner, the one who enters it —, the list says it is theirs, and a
 * trainer, who puts no exam in the calendar, reads it and may neither change it nor take it off; a visitor reads it on `/training` by
 * its rating, position and time, with nobody's VID or name, and in the calendar, whose entry points at `/training`; signed in, the
 * VIDs of the candidate and of the examiner; taken off the calendar from its form, it leaves `/training`.
 *
 * Who is who: the examiner is the bench's web master, who holds every permission of the training — the bench has no advisor of the
 * training department, and that an advisor enters and changes only their own exams is proved by `TrainingExamTests` (integration) —;
 * the candidate is the pilot of the tours (999002), the trainer `?as=trainer`. It writes no training, and takes its exam back at the
 * end, so it runs alone too and leaves the bench as it found it.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const EXAMINER = 999001;
const CANDIDATE = 999002;

/** What names the candidate and the examiner: their names and their VIDs, which a visitor never reads. */
const PEOPLE = ['Bench Pilot', 'Bench Coordinator', String(CANDIDATE), String(EXAMINER)];

interface Rating {
  readonly kind: 'Atc' | 'Pilot';
  readonly number: number;
  readonly shortName: string;
}

interface Choices {
  readonly examiners: readonly { readonly vid: number; readonly name: string | null }[];
  readonly positions: readonly {
    readonly callsign: string;
    readonly name: string;
    readonly ratingShortName: string;
  }[];
}

interface ExamRow {
  readonly id: number;
  readonly candidateVid: number;
  readonly examinerVid: number;
  readonly mine: boolean;
  readonly mayEdit: boolean;
  readonly rowVersion: string;
}

test('an exam is entered by whoever examines it, a trainer may not touch it, a visitor reads it without VIDs, and it leaves', async ({
  context,
  browser,
  afterwards,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);

  const trainer = await browser.newContext({ baseURL: benchUrl });
  const visitor = await browser.newContext({ baseURL: benchUrl });
  for (const reader of [trainer, visitor]) {
    await readInEnglish(reader);
  }

  afterwards(async () => {
    await removeTheExams(context.request);
    await trainer.close();
    await visitor.close();
  });

  await signIn(context, null);
  await signIn(trainer, 'trainer');

  // What a run that failed half way left: the exams of this candidate entered by this examiner.
  await removeTheExams(context.request);

  // ---------------------------------------------------------------- the rating and the position, as the server offers them
  const ratings = await get<Rating[]>(context.request, '/api/training/ratings');
  const rating = ratings.find((candidate) => candidate.kind === 'Atc')!;
  const choices = await get<Choices>(context.request, '/api/training/exam-choices');
  const position = choices.positions.find((candidate) => candidate.ratingShortName === rating.shortName)!;
  expect(position, 'a position of the division for the rating').toBeDefined();
  expect(choices.examiners.map((examiner) => examiner.vid)).toContain(EXAMINER);
  expect(
    choices.examiners.map((examiner) => examiner.vid),
    'a trainer examines nobody',
  ).not.toContain(999004);

  // ---------------------------------------------------------------- entered from the form, by its examiner
  const day = daysAhead(4);
  const title = `${rating.shortName} · ${position.callsign}`;
  const staffPage = await context.newPage();
  staffPage.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await staffPage.goto('/staff/training/exams/new');
  await expect(staffPage.getByRole('heading', { level: 1, name: words.exams.create })).toBeVisible();
  const ratingField = staffPage.getByText(words.exams.fields.rating, { exact: true }).locator('..');
  await ratingField.getByRole('combobox').click();
  await staffPage.getByRole('option', { name: new RegExp(` · ${rating.shortName} — `) }).click();
  await staffPage.getByLabel(words.exams.fields.position, { exact: true }).click();
  await staffPage.getByRole('option', { name: new RegExp(`^${position.callsign} — `) }).click();
  await staffPage.getByLabel(words.exams.fields.startsAtUtc, { exact: true }).fill(`${day}T17:00`);
  await staffPage.getByLabel(words.exams.fields.candidateVid, { exact: true }).fill(String(CANDIDATE));
  const examinerField = staffPage.getByText(words.exams.fields.examinerVid, { exact: true }).locator('..');
  await expect(examinerField.getByRole('combobox')).toHaveText(`Bench Coordinator (${String(EXAMINER)})`);

  const saved = staffPage.waitForResponse(
    (response) => response.request().method() === 'POST' && response.url().endsWith('/api/training/exams'),
  );
  await staffPage.getByRole('button', { name: englishCommon.common.save, exact: true }).click();
  expect((await saved).status()).toBe(201);
  await expect(staffPage).toHaveURL(/\/staff\/training\/exams(\?.*)?$/);

  // The list: theirs, and a step on it.
  const exam = (await examsOf(context.request))[0]!;
  expect(exam).toMatchObject({ candidateVid: CANDIDATE, examinerVid: EXAMINER, mine: true, mayEdit: true });
  const row = staffPage.getByRole('row').filter({ hasText: String(CANDIDATE) });
  await expect(row.getByRole('link', { name: englishCommon.common.edit })).toHaveAttribute(
    'href',
    `/staff/training/exams/${String(exam.id)}`,
  );

  // ---------------------------------------------------------------- a trainer reads it, and may neither change it nor take it off
  const theirs = (await examsOf(trainer.request)).find((candidate) => candidate.id === exam.id)!;
  expect(theirs).toMatchObject({ mine: false, mayEdit: false });
  const changed = await trainer.request.put(`/api/training/exams/${String(exam.id)}`, {
    headers: asTheClientDoes,
    data: {
      kind: rating.kind,
      rating: rating.number,
      position: position.callsign,
      startsAtUtc: `${day}T18:00:00Z`,
      candidateVid: CANDIDATE,
      examinerVid: EXAMINER,
      rowVersion: exam.rowVersion,
    },
  });
  expect(changed.status(), await changed.text()).toBe(403);
  const removed = await trainer.request.delete(`/api/training/exams/${String(exam.id)}`, {
    headers: asTheClientDoes,
  });
  expect(removed.status(), await removed.text()).toBe(403);

  // ---------------------------------------------------------------- /training, to a visitor: where and when, and nobody
  const visitorPage = await visitor.newPage();
  visitorPage.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await visitorPage.goto('/training');
  await expect(visitorPage.getByRole('heading', { level: 2, name: words.public.upcoming })).toBeVisible();
  await expect(visitorPage.getByText(filled(words.public.exam, { title }), { exact: true })).toBeVisible();
  await assertNobody(visitorPage.locator('body'));

  // The calendar: a public entry, titled without anybody, pointing at /training.
  await visitorPage.goto(`/calendar?view=weekList&on=${day}`);
  await expect(visitorPage.locator('a[href="/training"]').filter({ hasText: title })).toBeVisible();
  await assertNobody(visitorPage.locator('body'));

  // ---------------------------------------------------------------- signed in: the two VIDs, and nothing else of them
  const trainerPage = await trainer.newPage();
  await trainerPage.goto('/training');
  await expect(
    trainerPage.getByText(
      filled(words.public.examPeople, { candidate: String(CANDIDATE), examiner: String(EXAMINER) }),
      { exact: true },
    ),
  ).toBeVisible();

  // ---------------------------------------------------------------- taken off the calendar, asked first: it leaves /training
  await staffPage.goto(`/staff/training/exams/${String(exam.id)}`);
  await expect(staffPage.getByRole('heading', { level: 1, name: words.exams.edit })).toBeVisible();
  await staffPage.getByRole('button', { name: englishCommon.common.delete, exact: true }).click();
  await staffPage
    .getByRole('alertdialog')
    .getByRole('button', { name: englishCommon.common.delete, exact: true })
    .click();
  await expect(staffPage).toHaveURL(/\/staff\/training\/exams(\?.*)?$/);
  expect(await examsOf(context.request)).toEqual([]);

  await visitorPage.goto('/training');
  await expect(visitorPage.getByRole('heading', { level: 2, name: words.public.upcoming })).toBeVisible();
  await expect(visitorPage.getByText(filled(words.public.exam, { title }), { exact: true })).toHaveCount(0);
});

/** What a visitor reads names neither the candidate nor the examiner, by name or by VID. */
async function assertNobody(where: Locator): Promise<void> {
  const shown = await where.innerText();
  for (const somebody of PEOPLE) {
    expect(shown, `a visitor reads nobody's name nor VID: ${somebody}`).not.toContain(somebody);
  }
}

async function signIn(context: BrowserContext, as: 'trainer' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

async function get<T>(request: APIRequestContext, url: string): Promise<T> {
  const response = await request.get(url);
  expect(response.status(), `${url}: ${await response.text()}`).toBe(200);
  return (await response.json()) as T;
}

/** The exams of this run: of the bench's pilot, examined by the bench's web master. */
async function examsOf(request: APIRequestContext): Promise<ExamRow[]> {
  const page = await get<{ items: ExamRow[] }>(
    request,
    `/api/training/exams?pageSize=100&filter[examinerVid]=${String(EXAMINER)}`,
  );
  return page.items.filter((exam) => exam.candidateVid === CANDIDATE);
}

/** The exams of this run taken off the calendar, through the API: a run leaves the bench as it found it. */
async function removeTheExams(staff: APIRequestContext): Promise<void> {
  for (const exam of await examsOf(staff)) {
    const response = await staff.delete(`/api/training/exams/${String(exam.id)}`, {
      headers: asTheClientDoes,
    });
    expect(response.status(), await response.text()).toBe(204);
  }
}

/** A day some days ahead, in UTC, as `YYYY-MM-DD`: far enough that no date of the run goes by while it runs. */
function daysAhead(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
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
    public: { upcoming: string; exam: string; examPeople: string };
    exams: {
      create: string;
      edit: string;
      fields: {
        rating: string;
        position: string;
        startsAtUtc: string;
        candidateVid: string;
        examinerVid: string;
      };
    };
  };
}
