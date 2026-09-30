import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import {
  anonymousBootstrap,
  staffBootstrap,
  stubTheApi,
  stubTheBlockData,
  stubThePublishedPage,
} from './fixtures';
import { englishCommon } from './locales';

/**
 * The exams in a browser, with the API stubbed (M3, A10c): the list of the staff says which exams are the reader's and offers a step
 * only on the ones the server says they may change, and narrows to their own; the form offers the examiners the server gives the
 * reader, sends the exam, and puts a refusal of the server under its field; whoever edits the area changes and takes off the calendar
 * an exam of somebody else's; a reader who enters no exams is offered no form; `/training` and the block draw the exams still to come
 * beside the sessions, with the VIDs only for a signed in reader; an examiner whose data was erased is a deleted person (A12b). What
 * the server decides is proved by `TrainingExamTests` and `TrainingTraineeTests` (integration); the round against the real server is
 * `full/training-exams.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  public: {
    upcoming: string;
    none: string;
    signIn: string;
    exam: string;
    examPeople: string;
    examsUnread: string;
  };
  exams: {
    title: string;
    create: string;
    edit: string;
    fields: Record<string, string>;
    options: { whose: { Mine: string; Other: string } };
    filters: { mine: string };
    delete: { title: string };
  };
  errors: Record<string, string>;
};

/** The core's word for a person whose data was erased (A12a), which the lists and the pages say in the place of the pseudonym. */
const deleted = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
  ) as { people: { deleted: string } }
).people.deleted;

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const advisor = { vid: 790097, name: 'Test Advisor' };
const otherAdvisor = { vid: 790096, name: 'Other Advisor' };

/** An advisor of the training department: reads the training and enters their own exams, and holds no Edit. */
const advisorBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: advisor.vid,
    lastName: 'Advisor',
    positions: ['XX-TA1'],
    departments: ['TD'],
  },
  permissions: [
    { name: 'Training.View', department: 'TD' },
    { name: 'Training.Approve', department: 'TD' },
    { name: 'Training.ManageExams', department: 'TD' },
  ],
};

/** The coordinator: every exam, and the examiner of one entered for somebody else. */
const coordinatorBootstrap = {
  ...advisorBootstrap,
  user: { ...advisorBootstrap.user, vid: 790095, lastName: 'Coordinator', positions: ['XX-TC'] },
  permissions: [...advisorBootstrap.permissions, { name: 'Training.Edit', department: 'TD' }],
};

/** A trainer: reads the exams and enters none. */
const trainerBootstrap = {
  ...advisorBootstrap,
  user: { ...advisorBootstrap.user, vid: 790094, lastName: 'Trainer', positions: ['XX-T01'] },
  permissions: [{ name: 'Training.View', department: 'TD' }],
};

/** A member who is not of the staff: reads who is in a session or an exam, and nothing of the back office. */
const memberBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: 790093,
    lastName: 'Member',
    isStaff: false,
    positions: [],
    departments: [],
  },
  permissions: [],
};

/** An hour of a day some days ahead, in UTC, as the API writes an instant: the dates of a test never go by. */
function at(days: number, hour: number): string {
  const moment = new Date(Date.now() + days * 86_400_000);
  moment.setUTCHours(hour, 0, 0, 0);
  return moment.toISOString().replace('.000Z', 'Z');
}

/** What a `datetime-local` box holds for an instant: the UTC wall clock, to the minute. */
const box = (instant: string) => instant.slice(0, 16);

function row(id: number, overrides: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    position: 'XXAA_TWR',
    startsAtUtc: at(3, 18),
    candidateVid: 790099,
    examinerVid: advisor.vid,
    mine: true,
    mayEdit: true,
    rowVersion: '2026-09-28T10:00:00.123456Z',
    ...overrides,
  };
}

function detail(id: number, overrides: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    rating: 5,
    position: 'XXAA_TWR',
    startsAtUtc: at(3, 18),
    candidateVid: 790099,
    examinerVid: otherAdvisor.vid,
    rowVersion: '2026-09-28T10:00:00.123456Z',
    ...overrides,
  };
}

/** The ratings the other forms of the training offer, the trained ones; an exam takes any rating of the path, the eighth too (#178). */
const ratings = [{ kind: 'Atc', number: 5, shortName: 'ADC', nameKey: 'ratings.Atc.ADC' }];
const examined = [...ratings, { kind: 'Atc', number: 8, shortName: 'SEC', nameKey: 'ratings.Atc.SEC' }];
const positions = [{ callsign: 'XXAA_TWR', name: 'Example Tower', ratingShortName: 'ADC' }];

interface Seen {
  lists: string[];
  sent: { method: string; url: string; body: unknown }[];
}

/**
 * The API of the exams: the list as `rows` says it, one exam, what the form chooses from, and what the server answers to an exam
 * entered, changed or taken off — what is sent kept for the test to read.
 */
async function stubTheExams(
  page: Page,
  {
    bootstrap = advisorBootstrap,
    rows = [row(61)],
    one = detail(61),
    examiners = [advisor],
    write = () => ({ status: 201, body: detail(62, { examinerVid: advisor.vid }) }),
  }: {
    bootstrap?: unknown;
    rows?: unknown[];
    one?: unknown;
    examiners?: readonly { vid: number; name: string }[];
    write?: (method: string, body: unknown) => { status: number; body: unknown };
  } = {},
): Promise<Seen> {
  const seen: Seen = { lists: [], sent: [] };

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route('**/api/training/ratings', (route) => route.fulfill(json(ratings)));
  await page.route('**/api/training/exam-choices', (route) =>
    route.fulfill(json({ ratings: examined, examiners, positions })),
  );
  await page.route('**/api/training/exams?**', (route) => {
    seen.lists.push(decodeURIComponent(route.request().url()));
    return route.fulfill(json({ items: rows, page: 1, pageSize: 25, total: rows.length }));
  });
  await page.route(/\/api\/training\/exams(\/\d+)?$/, (route) => {
    const request = route.request();
    if (request.method() === 'GET') {
      return route.fulfill(json(one));
    }

    const body = request.method() === 'DELETE' ? null : (request.postDataJSON() as unknown);
    seen.sent.push({ method: request.method(), url: request.url(), body });
    const answer = write(request.method(), body);
    return route.fulfill(
      answer.status === 204 ? { status: 204, body: '' } : json(answer.body, answer.status),
    );
  });

  return seen;
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('an advisor sees which exams are theirs, is offered a step only on those, and narrows the list to them', async ({
  page,
}) => {
  const seen = await stubTheExams(page, {
    rows: [
      row(61),
      row(62, {
        examinerVid: otherAdvisor.vid,
        candidateVid: 790098,
        mine: false,
        mayEdit: false,
        startsAtUtc: at(4, 9),
      }),
    ],
  });

  await page.goto('/staff/training/exams');
  await expect(page.getByRole('heading', { level: 1, name: words.exams.title })).toBeVisible();
  await expect(page.getByRole('link', { name: words.exams.create })).toHaveAttribute(
    'href',
    '/staff/training/exams/new',
  );

  // Both exams, by the VIDs of the candidate and the examiner; a step only on the reader's own, the latest first.
  const mine = page.getByRole('row').filter({ hasText: '790099' });
  const theirs = page.getByRole('row').filter({ hasText: '790098' });
  await expect(mine.getByText(String(advisor.vid), { exact: true })).toBeVisible();
  await expect(theirs.getByText(String(otherAdvisor.vid), { exact: true })).toBeVisible();
  await expect(mine.getByRole('link', { name: englishCommon.common.edit })).toHaveAttribute(
    'href',
    '/staff/training/exams/61',
  );
  await expect(theirs.getByRole('link', { name: englishCommon.common.edit })).toHaveCount(0);
  expect(seen.lists.at(-1)).toContain('dir=desc');

  // Whose, in the module's words — the core's yes and no of a column are a switch's, «active» and «inactive».
  await expect(mine.getByText(words.exams.options.whose.Mine, { exact: true })).toBeVisible();
  await expect(theirs.getByText(words.exams.options.whose.Other, { exact: true })).toBeVisible();

  // «Me»: the exams assigned to the reader.
  await page.locator('#exams-examiner').click();
  await page.getByRole('option', { name: words.exams.filters.mine, exact: true }).click();
  await expect(page).toHaveURL(/mine=true/);
  await expect.poll(() => seen.lists.at(-1)).toContain(`filter[examinerVid]=${String(advisor.vid)}`);
});

test('an advisor enters an exam of their own, and a refusal of the server lands under its field', async ({
  page,
}) => {
  let refuse = true;
  const seen = await stubTheExams(page, {
    write: () =>
      refuse
        ? {
            status: 400,
            body: {
              title: 'One or more validation errors occurred.',
              status: 400,
              errors: { examinerVid: [words.errors.examinerIsCandidate] },
            },
          }
        : { status: 201, body: detail(62, { examinerVid: advisor.vid }) },
  });
  const starts = at(5, 18);

  await page.goto('/staff/training/exams/new');
  await expect(page.getByRole('heading', { level: 1, name: words.exams.create })).toBeVisible();

  const rating = page.getByText(words.exams.fields.rating!, { exact: true }).locator('..');
  await rating.getByRole('combobox').click();
  // Every rating of the path the server offers for an exam, the eighth too, which nobody trains for (#178).
  await expect(page.getByRole('option', { name: /SEC/ })).toBeVisible();
  await page.getByRole('option', { name: /ADC/ }).click();
  await page.getByLabel(words.exams.fields.position!, { exact: true }).click();
  await page.getByRole('option', { name: /Example Tower/ }).click();
  await page.getByLabel(words.exams.fields.startsAtUtc!, { exact: true }).fill(box(starts));
  await page.getByLabel(words.exams.fields.candidateVid!, { exact: true }).fill('790099');

  // The examiner is the advisor, the only one the server offers them, already chosen.
  const examiner = page.getByText(words.exams.fields.examinerVid!, { exact: true }).locator('..');
  await expect(examiner.getByRole('combobox')).toHaveText(/Test Advisor \(790097\)/);

  await page.getByRole('button', { name: englishCommon.common.save, exact: true }).click();
  await expect(page.getByText(words.errors.examinerIsCandidate!, { exact: true })).toBeVisible();
  expect(seen.sent).toEqual([
    {
      method: 'POST',
      url: expect.stringContaining('/api/training/exams') as unknown as string,
      body: {
        kind: 'Atc',
        rating: 5,
        position: 'XXAA_TWR',
        startsAtUtc: expect.stringMatching(new RegExp(`^${box(starts)}`)) as unknown as string,
        candidateVid: 790099,
        examinerVid: advisor.vid,
        rowVersion: expect.any(String) as unknown as string,
      },
    },
  ]);

  refuse = false;
  await page.getByRole('button', { name: englishCommon.common.save, exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/training\/exams(\?.*)?$/);
});

test('the coordinator changes an exam somebody else examines, and takes it off the calendar asked first', async ({
  page,
}) => {
  const seen = await stubTheExams(page, {
    bootstrap: coordinatorBootstrap,
    rows: [row(61, { examinerVid: otherAdvisor.vid, mine: false, mayEdit: true })],
    examiners: [otherAdvisor, advisor],
    write: (method) =>
      method === 'DELETE'
        ? { status: 204, body: null }
        : { status: 200, body: detail(61, { examinerVid: advisor.vid }) },
  });

  await page.goto('/staff/training/exams/61');
  await expect(page.getByRole('heading', { level: 1, name: words.exams.edit })).toBeVisible();

  // The examiner of the exam, and the one it is handed to.
  const examiner = page.getByText(words.exams.fields.examinerVid!, { exact: true }).locator('..');
  await expect(examiner.getByRole('combobox')).toHaveText(/Other Advisor \(790096\)/);
  await examiner.getByRole('combobox').click();
  await page.getByRole('option', { name: 'Test Advisor (790097)', exact: true }).click();
  await page.getByRole('button', { name: englishCommon.common.save, exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/training\/exams(\?.*)?$/);
  expect(seen.sent[0]).toMatchObject({
    method: 'PUT',
    body: { examinerVid: advisor.vid, rowVersion: detail(61).rowVersion },
  });

  // Taken off the calendar, asked first.
  await page.goto('/staff/training/exams/61');
  await page.getByRole('button', { name: englishCommon.common.delete, exact: true }).click();
  const dialog = page.getByRole('alertdialog');
  await expect(dialog.getByText(words.exams.delete.title, { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: englishCommon.common.delete, exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/training\/exams(\?.*)?$/);
  expect(seen.sent.map((sent) => sent.method)).toEqual(['PUT', 'DELETE']);
});

test('a reader who enters no exams reads the list with no step, and is offered no form', async ({ page }) => {
  await stubTheExams(page, {
    bootstrap: trainerBootstrap,
    rows: [row(61, { examinerVid: advisor.vid, mine: false, mayEdit: false })],
  });

  await page.goto('/staff/training/exams');
  await expect(page.getByRole('row').filter({ hasText: '790099' })).toBeVisible();
  await expect(page.getByRole('link', { name: words.exams.create })).toHaveCount(0);
  await expect(page.getByRole('link', { name: englishCommon.common.edit })).toHaveCount(0);

  await page.goto('/staff/training/exams/new');
  await expect(page.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
});

/** A session still to be held, as `/api/training/sessions` answers it. */
function session(id: number, startsAtUtc: string, people: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    ratingShortName: 'ADC',
    position: 'XXAA_TWR',
    startsAtUtc,
    held: false,
    trainee: null,
    trainer: null,
    ...people,
  };
}

/** An exam still to come, as `/api/training/sessions/exams` answers it. */
function exam(
  id: number,
  startsAtUtc: string,
  vids: { candidateVid: number; examinerVid: number } | null = null,
) {
  return {
    id,
    kind: 'Pilot',
    ratingShortName: 'PP',
    position: null,
    startsAtUtc,
    candidateVid: vids?.candidateVid ?? null,
    examinerVid: vids?.examinerVid ?? null,
  };
}

async function stubTheSite(
  page: Page,
  sessions: unknown[],
  exams: unknown[],
  bootstrap: unknown = anonymousBootstrap,
) {
  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route(
    (url) => url.pathname === '/api/training/sessions',
    (route) => route.fulfill(json(sessions)),
  );
  await page.route(
    (url) => url.pathname === '/api/training/sessions/exams',
    (route) => route.fulfill(json(exams)),
  );
}

test('/training shows a visitor the exams to come beside the sessions, in their order and with nobody named', async ({
  page,
}) => {
  await stubTheSite(page, [session(41, at(2, 18))], [exam(51, at(1, 9)), exam(52, at(3, 9))]);

  await page.goto('/training');
  await expect(page.getByRole('heading', { level: 2, name: words.public.upcoming })).toBeVisible();

  const lines = page.locator('main li');
  await expect(lines).toHaveCount(3);
  await expect(lines.nth(0)).toContainText(filled(words.public.exam, { title: 'PP' }));
  await expect(lines.nth(1).getByRole('link', { name: 'ADC · XXAA_TWR', exact: true })).toHaveAttribute(
    'href',
    '/training/sessions/41',
  );
  await expect(lines.nth(2)).toContainText(filled(words.public.exam, { title: 'PP' }));

  // An exam has no page of its own, and names nobody to a visitor.
  await expect(lines.nth(0).getByRole('link')).toHaveCount(0);
  await expect(page.locator('main')).not.toContainText('7900');
  await expect(page.getByRole('link', { name: words.public.signIn })).toBeVisible();
});

test('a signed in member reads the candidate and the examiner of an exam by VID', async ({ page }) => {
  await stubTheSite(
    page,
    [],
    [exam(51, at(1, 9), { candidateVid: 790099, examinerVid: 790097 })],
    memberBootstrap,
  );

  await page.goto('/training');
  await expect(
    page.getByText(filled(words.public.examPeople, { candidate: '790099', examiner: '790097' }), {
      exact: true,
    }),
  ).toBeVisible();
  await expect(page.getByText(words.public.none, { exact: true })).toHaveCount(0);
});

test('an examiner whose data was erased is a deleted person in the list of the staff, never the number (A12b)', async ({
  page,
}) => {
  // What the server sends once the examiner is erased: the pseudonym in their place.
  await stubTheExams(page, {
    bootstrap: coordinatorBootstrap,
    rows: [row(63, { examinerVid: -5, mine: false, mayEdit: true })],
  });

  await page.goto('/staff/training/exams');
  const erased = page.getByRole('row').filter({ hasText: '790099' });
  await expect(erased.getByRole('cell', { name: deleted, exact: true })).toBeVisible();
  await expect(page.getByText('-5', { exact: true })).toHaveCount(0);
});

test('a signed in member reads an examiner whose data was erased as a deleted person on /training (A12b)', async ({
  page,
}) => {
  await stubTheSite(
    page,
    [],
    [exam(53, at(1, 9), { candidateVid: 790099, examinerVid: -5 })],
    memberBootstrap,
  );

  await page.goto('/training');
  await expect(
    page.getByText(filled(words.public.examPeople, { candidate: '790099', examiner: deleted }), {
      exact: true,
    }),
  ).toBeVisible();
});

test('when the exams cannot be read, /training still shows the sessions and says so', async ({ page }) => {
  await stubTheApi(page);
  await page.route(
    (url) => url.pathname === '/api/training/sessions',
    (route) => route.fulfill(json([session(41, at(2, 18))])),
  );
  await page.route(
    (url) => url.pathname === '/api/training/sessions/exams',
    (route) => route.fulfill(json({ title: 'Boom', status: 500 }, 500)),
  );

  await page.goto('/training');
  await expect(page.getByRole('link', { name: 'ADC · XXAA_TWR', exact: true })).toBeVisible();
  await expect(page.getByText(words.public.examsUnread, { exact: true })).toBeVisible();
});

test('on a page of the site the block draws the exams with the sessions, as many of the two as it asks for', async ({
  page,
}) => {
  await stubThePublishedPage(page, 'training-days', {
    schemaVersion: 1,
    sections: [
      {
        id: 's_main',
        layout: 'stacked',
        blocks: [{ id: 'b_sessions', type: 'training.upcomingSessions', version: 1, props: { limit: 2 } }],
      },
    ],
  });
  await stubTheBlockData(page, 'training.upcomingSessions', {
    signedIn: false,
    items: [session(41, at(2, 18)), session(42, at(4, 18))],
    exams: [exam(51, at(1, 9))],
  });

  await page.goto('/training-days');

  const lines = page.locator('main li');
  await expect(lines).toHaveCount(2);
  await expect(lines.nth(0)).toContainText(filled(words.public.exam, { title: 'PP' }));
  await expect(lines.nth(1).getByRole('link', { name: 'ADC · XXAA_TWR', exact: true })).toBeVisible();
});
