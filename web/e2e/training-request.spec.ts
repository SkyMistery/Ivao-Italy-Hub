import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { anonymousBootstrap, stubTheApi } from './fixtures';

/**
 * The trainee's pages in a browser, with the API stubbed (M3, A6b): the request with the question on the theory — a «yes»
 * that goes and lands in `/training/mine`, a «no» that is sent, recorded and said on the screen —, each refusal where it
 * belongs, a ladder the server refuses with what it says beside it, and a request cancelled. What the server decides is
 * proved by `TrainingRequestTests` (A6a); the round against the real server is `full/training-request.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  kinds: Record<string, string>;
  theoryExam: string;
  mockExam: string;
  states: Record<string, string>;
  request: {
    title: string;
    send: string;
    sent: string;
    fields: Record<string, string>;
    theory: { question: string; yes: string; no: string; confirm: string };
    declined: { title: string };
  };
  mine: {
    title: string;
    canAsk: string;
    cancel: string;
    cancelConfirm: string;
    theoryRefusal: string;
    readyForExam: string;
  };
  errors: Record<string, string>;
};

const EXAM = 'https://exam.example.org/theory';

const traineeBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: 790099,
    firstName: 'Test',
    lastName: 'Trainee',
    positions: [],
    isStaff: false,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: [],
    firs: [],
    tokenAudiences: [],
  },
};

const atc = {
  kind: 'Atc',
  ratingShortName: 'AS3',
  hours: 120,
  next: { kind: 'Atc', number: 5, shortName: 'ADC', nameKey: 'ratings.Atc.ADC' },
  isMockExam: false,
  asksPosition: true,
  positions: [
    { callsign: 'XXAA_TWR', name: 'Example Tower', ratingShortName: 'ADC' },
    { callsign: 'XXBB_TWR', name: 'Other Tower', ratingShortName: 'ADC' },
  ],
  refusal: null,
  bannedUntil: null,
  openTrainingId: null,
  waitUntil: null,
  minimumHours: null,
};

const pilot = {
  ...atc,
  kind: 'Pilot',
  ratingShortName: 'FS3',
  hours: 150,
  next: { kind: 'Pilot', number: 5, shortName: 'PP', nameKey: 'ratings.Pilot.PP' },
  asksPosition: false,
  positions: [],
};

function training(id: number, state: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    state,
    rejection: null,
    rejectionReason: null,
    availabilityText: 'Evenings after 18 UTC.',
    notesText: null,
    requestedAt: '2026-09-26T10:00:00Z',
    decidedAt: null,
    scheduledStartUtc: null,
    completedAt: null,
    closedAt: null,
    readyForMockExam: false,
    readyForExam: false,
    rowVersion: '2026-09-26T10:00:00.123456Z',
    ...overrides,
  };
}

function mine(paths: unknown[], trainings: unknown[] = []) {
  return {
    vid: 790099,
    name: 'Test Trainee',
    asksTheory: true,
    theoryExamUrl: EXAM,
    paths,
    trainings,
  };
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/**
 * The trainee's side of the API: their page as `answer` says it at the moment it is asked — a request or a cancellation
 * changes it —, and what is sent kept for the test to read.
 */
async function stubTheTrainee(
  page: Page,
  {
    answer,
    request = () => ({ status: 500, body: {} }),
    cancel = () => ({ status: 500, body: {} }),
  }: {
    answer: () => unknown;
    request?: (body: Record<string, unknown>) => { status: number; body: unknown };
    cancel?: (id: string, body: Record<string, unknown>) => { status: number; body: unknown };
  },
): Promise<void> {
  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(traineeBootstrap)));
  await page.route('**/api/training/mine', (route) => {
    if (route.request().method() === 'POST') {
      const sent = request(route.request().postDataJSON() as Record<string, unknown>);
      return route.fulfill(json(sent.body, sent.status));
    }

    return route.fulfill(json(answer()));
  });
  await page.route('**/api/training/mine/*/cancel', (route) => {
    const id = /\/mine\/(\d+)\/cancel/.exec(route.request().url())![1]!;
    const sent = cancel(id, route.request().postDataJSON() as Record<string, unknown>);
    return route.fulfill(json(sent.body, sent.status));
  });
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a member asks for a training: a position, the question on the theory, «yes», and it is in their trainings', async ({
  page,
}) => {
  const requested = training(41, 'Requested');
  let asked = false;
  const sent: Record<string, unknown>[] = [];

  await stubTheTrainee(page, {
    answer: () => mine([atc, pilot], asked ? [requested] : []),
    request: (body) => {
      sent.push(body);
      asked = true;
      return { status: 201, body: requested };
    },
  });

  await page.goto('/training/request');
  await expect(page.getByRole('heading', { level: 1, name: words.request.title })).toBeVisible();

  // Who asks, read only: the VID, the name, and the rating and hours of the ladder chosen — the first one, ATC.
  const article = page.getByRole('article');
  await expect(article.getByText('790099', { exact: true })).toBeVisible();
  await expect(article.getByText('Test Trainee', { exact: true })).toBeVisible();
  await expect(article.getByText('AS3', { exact: true })).toBeVisible();
  await expect(article.getByText('120', { exact: true })).toBeVisible();

  // A position, chosen from the ones offered. ⚠️ From the list, and not by typing part of it first: the closed suggestion
  // of the core loses a choice clicked after typing (found in A6b, reported for a change of the core of its own).
  await page.getByLabel(words.request.fields.position!, { exact: true }).click();
  await page.getByRole('option', { name: /Example Tower/ }).click();
  await page
    .getByLabel(words.request.fields.availabilityText!, { exact: true })
    .fill('Evenings after 18 UTC.');

  // «Request training» asks the question first, with the site of the exam; nothing is sent without an answer.
  await page.getByRole('button', { name: words.request.send, exact: true }).click();
  const question = page.getByRole('alertdialog');
  await expect(question.getByText(words.request.theory.question.replace('{{rating}}', 'ADC'))).toBeVisible();
  await expect(question.getByRole('link', { name: words.theoryExam })).toHaveAttribute('href', EXAM);
  await expect(question.getByRole('button', { name: words.request.theory.confirm })).toBeDisabled();

  await question.getByRole('radio', { name: words.request.theory.yes }).check();
  await question.getByRole('button', { name: words.request.theory.confirm }).click();

  // Sent with the rating the page proposed and the answer; then the trainee's trainings, the request on top.
  await expect(page).toHaveURL(/\/training\/mine$/);
  expect(sent).toEqual([
    {
      kind: 'Atc',
      rating: 5,
      position: 'XXAA_TWR',
      availabilityText: 'Evenings after 18 UTC.',
      notesText: null,
      theoryPassed: true,
    },
  ]);
  await expect(page.getByText(words.request.sent.replace('{{rating}}', 'ADC'))).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: words.mine.title })).toBeVisible();
  await expect(page.getByText(words.states.Requested!, { exact: true })).toBeVisible();
});

test('«no» to the theory is sent all the same, and the screen says why the request is refused', async ({
  page,
}) => {
  const sent: Record<string, unknown>[] = [];

  await stubTheTrainee(page, {
    answer: () => mine([atc, pilot]),
    request: (body) => {
      sent.push(body);
      return {
        status: 201,
        body: training(42, 'Rejected', { rejection: 'TheoryNotPassed', decidedAt: '2026-09-26T10:00:00Z' }),
      };
    },
  });

  await page.goto('/training/request');
  // A position written out whole is one of the ones offered, and stays when the box is left.
  await page.getByLabel(words.request.fields.position!, { exact: true }).fill('XXBB_TWR');

  await page.getByRole('button', { name: words.request.send, exact: true }).click();
  const question = page.getByRole('alertdialog');
  await question.getByRole('radio', { name: words.request.theory.no }).check();
  await question.getByRole('button', { name: words.request.theory.confirm }).click();

  // The hub's own refusal, on the screen and nowhere else: no mail goes for it.
  await expect(page.getByText(words.request.declined.title.replace('{{rating}}', 'ADC'))).toBeVisible();
  await expect(page.getByRole('link', { name: words.theoryExam })).toHaveAttribute('href', EXAM);
  await expect(page).toHaveURL(/\/training\/request$/);
  expect(sent).toHaveLength(1);
  expect(sent[0]).toMatchObject({ kind: 'Atc', position: 'XXBB_TWR', theoryPassed: false });
});

test('each refusal of the request lands where it belongs: a field on its field, the request above the form', async ({
  page,
}) => {
  await stubTheTrainee(page, {
    answer: () => mine([atc, pilot]),
    request: () => ({
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          position: ['training:errors.requestPositionUnknown'],
          rating: ['training:errors.requestRatingNotNext'],
        },
      },
    }),
  });

  await page.goto('/training/request');
  await page.getByRole('button', { name: words.request.send, exact: true }).click();
  const question = page.getByRole('alertdialog');
  await question.getByRole('radio', { name: words.request.theory.yes }).check();
  await question.getByRole('button', { name: words.request.theory.confirm }).click();

  await expect(page.getByText(words.errors.requestPositionUnknown!)).toBeVisible();
  await expect(page.getByText(words.errors.requestRatingNotNext!)).toBeVisible();
  await expect(page).toHaveURL(/\/training\/request$/);
});

test('a ladder the server refuses says why and until when; the other offers its training, a mock exam', async ({
  page,
}) => {
  await stubTheTrainee(page, {
    answer: () =>
      mine([
        { ...atc, refusal: 'training:errors.requestWaiting', waitUntil: '2036-10-01T12:00:00Z' },
        { ...pilot, isMockExam: true },
      ]),
  });

  // With no ladder in the address, the first one a request may be made on: the pilot's, with no position to choose.
  await page.goto('/training/request');
  await expect(page.getByText(words.mockExam)).toBeVisible();
  await expect(page.getByText('FS3', { exact: true })).toBeVisible();
  await expect(page.getByLabel(words.request.fields.position!, { exact: true })).toHaveCount(0);

  // The ATC ladder: the waiting, with its end from the same answer, and no form.
  await page.getByRole('radio', { name: new RegExp(words.kinds.Atc!) }).check();
  await expect(page).toHaveURL(/\/training\/request\?kind=Atc$/);
  const refused = page.getByRole('status').filter({ hasText: words.errors.requestWaiting! });
  await expect(refused).toBeVisible();
  await expect(refused).toContainText('Oct 1, 2036');
  await expect(page.getByRole('button', { name: words.request.send, exact: true })).toHaveCount(0);
});

test('the trainee reads their ladders and their trainings, and cancels a request nobody has accepted', async ({
  page,
}) => {
  const open = training(41, 'Requested');
  const declined = training(40, 'Rejected', {
    rejection: 'TheoryNotPassed',
    decidedAt: '2026-09-20T10:00:00Z',
  });
  const reported = training(39, 'Completed', {
    kind: 'Pilot',
    ratingShortName: 'PP',
    position: null,
    completedAt: '2026-09-01T20:00:00Z',
    readyForExam: true,
  });
  let cancelled = false;
  const sent: { id: string; body: Record<string, unknown> }[] = [];

  await stubTheTrainee(page, {
    answer: () =>
      mine(
        [cancelled ? atc : { ...atc, refusal: 'training:errors.requestOpen', openTrainingId: 41 }, pilot],
        [
          cancelled ? { ...open, state: 'Cancelled', closedAt: '2026-09-26T11:00:00Z' } : open,
          declined,
          reported,
        ],
      ),
    cancel: (id, body) => {
      sent.push({ id, body });
      cancelled = true;
      return { status: 200, body: { ...open, state: 'Cancelled' } };
    },
  });

  await page.goto('/training/mine');
  await expect(page.getByRole('heading', { level: 1, name: words.mine.title })).toBeVisible();

  // The ATC ladder has its request open; the pilot's may ask for its next training, on its own address.
  await expect(page.getByText(words.errors.requestOpen!)).toBeVisible();
  await expect(page.getByText(words.mine.canAsk, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.request.send })).toHaveAttribute(
    'href',
    '/training/request?kind=Pilot',
  );

  // The trainings, newest first: the request, the hub's refusal, a report with the trainer's box.
  await expect(page.getByText(words.states.Requested!, { exact: true })).toBeVisible();
  await expect(page.getByText(words.mine.theoryRefusal)).toBeVisible();
  await expect(page.getByText(words.mine.readyForExam, { exact: true })).toBeVisible();

  // «Cancel», asked first, with the version the trainee saw.
  await page.getByRole('button', { name: words.mine.cancel, exact: true }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: words.mine.cancelConfirm }).click();

  await expect(page.getByText(words.states.Cancelled!, { exact: true })).toBeVisible();
  expect(sent).toEqual([{ id: '41', body: { rowVersion: open.rowVersion } }]);
  await expect(page.getByRole('button', { name: words.mine.cancel, exact: true })).toHaveCount(0);
});
