import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheApi } from './fixtures';

/**
 * The staff's side of the trainings in a browser, with the API stubbed (M3, A7): the list narrows to a view and to a ladder,
 * asked of the server; the page of a request reminds whoever approves to check the theory exam, with its site, and the request
 * is accepted, or refused with a reason the dialog asks for first; the trainer is chosen among the ones the server offers, and
 * its refusal lands under the field; a step somebody else overtook reads the page again; a reader the server lets do nothing
 * sees no button. What the server decides is proved by `TrainingStaffTests` (integration); the round against the real server is
 * `full/training-staff.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  kinds: Record<string, string>;
  theoryExam: string;
  states: Record<string, string>;
  staff: {
    title: string;
    open: string;
    pageTitle: string;
    options: { state: Record<string, string> };
    queues: Record<string, string>;
    sections: { trainer: string };
    theoryReminder: { title: string };
    decision: { reason: string; accepted: string };
    trainer: { none: string; assigned: string };
    assign: { submit: string; change: string; assigned: string; fields: { trainerVid: string } };
    accept: { button: string; done: string };
    reject: { button: string; done: string; fields: { reason: string } };
    refused: string;
  };
  errors: Record<string, string>;
};

/** The core's sentence for a conflict, which the page says in its notice. */
const conflict = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL('../../locales/en/errors.json', import.meta.url)), 'utf8'),
  ) as { errors: { conflict: { title: string } } }
).errors.conflict.title;

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const EXAM = 'https://exam.example.org/theory';

const coordinator = { vid: 790097, name: 'Test Coordinator' };
const trainee = { vid: 790099, name: 'Test Trainee' };
const trainer = { vid: 790098, name: 'Test Trainer' };

const coordinatorBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: coordinator.vid,
    lastName: 'Coordinator',
    positions: ['XX-TC'],
    departments: ['TD'],
  },
  permissions: [
    { name: 'Training.View', department: 'TD' },
    { name: 'Training.Approve', department: 'TD' },
    { name: 'Training.Assign', department: 'TD' },
  ],
};

const trainerBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: trainer.vid,
    lastName: 'Trainer',
    positions: ['XX-T01'],
    departments: ['TD'],
  },
  permissions: [{ name: 'Training.View', department: 'TD' }],
};

function row(id: number, state: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    state,
    trainee,
    trainer: null,
    createdAt: '2026-09-20T10:00:00Z',
    scheduledStartUtc: null,
    held: false,
    ...overrides,
  };
}

function training(state: string, overrides: Record<string, unknown> = {}) {
  return {
    id: 41,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    ratingNameKey: 'ratings.Atc.ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    airportIcao: 'XXAA',
    fir: 'XXXX',
    trainee,
    traineeRatingShortName: 'AS3',
    traineeHoursAtRequest: 120,
    requestedAt: '2026-09-20T10:00:00Z',
    theoryConfirmedAt: '2026-09-20T10:00:00Z',
    theoryExamUrl: EXAM,
    availabilityText: 'Evenings after 18 UTC.',
    notesText: null,
    state,
    rejection: null,
    rejectionReason: null,
    decidedBy: null,
    decidedAt: null,
    trainer: null,
    assignedBy: null,
    assignedAt: null,
    slots: [],
    scheduledStartUtc: null,
    held: false,
    dateChosenByTrainee: false,
    completedAt: null,
    closedBy: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    actions: { canDecide: false, canAssign: false, canConduct: false, canClose: false },
    rowVersion: '2026-09-20T10:00:00.123456Z',
    ...overrides,
  };
}

const accepted = (overrides: Record<string, unknown> = {}) =>
  training('Accepted', {
    decidedBy: coordinator,
    decidedAt: '2026-09-21T09:00:00Z',
    actions: { canDecide: false, canAssign: true },
    rowVersion: '2026-09-21T09:00:00.123456Z',
    ...overrides,
  });

const candidates = [
  { vid: trainer.vid, name: trainer.name, ratingShortName: 'SEC', positions: ['XX-T01'], isCurrent: false },
  { vid: 790096, name: 'Other Trainer', ratingShortName: 'APC', positions: ['XX-TA1'], isCurrent: false },
];

interface Seen {
  queue: string[];
  steps: { verb: string; body: Record<string, unknown> }[];
}

/**
 * The staff's side of the API: the list as `rows` says it, the page as it stands — each step changes it, as `step` answers, and
 * a refused one may say what somebody else made of it meanwhile (`now`) —, the trainers the server offers, and what is sent
 * kept for the test to read.
 */
async function stubTheStaff(
  page: Page,
  {
    bootstrap = coordinatorBootstrap,
    initial = training('Requested'),
    rows = [row(41, 'Requested')],
    step = () => ({ status: 500, body: {} }),
  }: {
    bootstrap?: unknown;
    initial?: Record<string, unknown>;
    rows?: unknown[];
    step?: (verb: string, body: Record<string, unknown>) => { status: number; body: unknown; now?: unknown };
  } = {},
): Promise<Seen> {
  const seen: Seen = { queue: [], steps: [] };
  let current: unknown = initial;

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route('**/api/training/queue?**', (route) => {
    seen.queue.push(decodeURIComponent(route.request().url()));
    return route.fulfill(json({ items: rows, page: 1, pageSize: 25, total: rows.length }));
  });
  await page.route('**/api/training/trainings/41', (route) => route.fulfill(json(current)));
  await page.route('**/api/training/trainings/41/trainers', (route) => route.fulfill(json(candidates)));
  await page.route('**/api/training/trainings/41/*', (route) => {
    const verb = /\/41\/(\w+)$/.exec(route.request().url())?.[1] ?? '';
    if (verb === 'trainers') {
      return route.fallback();
    }

    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.steps.push({ verb, body });
    const answer = step(verb, body);
    if (answer.status === 200) {
      current = answer.body;
    } else if (answer.now !== undefined) {
      current = answer.now;
    }
    return route.fulfill(json(answer.body, answer.status));
  });

  return seen;
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the list holds every training and narrows to a view and to a ladder, asked of the server', async ({
  page,
}) => {
  const seen = await stubTheStaff(page, {
    rows: [
      row(41, 'Requested'),
      row(40, 'Assigned', { trainer, ratingShortName: 'APC', createdAt: '2026-09-18T10:00:00Z' }),
    ],
  });

  await page.goto('/staff/training');
  await expect(page.getByRole('heading', { level: 1, name: words.staff.title })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Test Trainee (790099)' }).first()).toBeVisible();
  await expect(
    page.getByRole('cell', { name: words.staff.options.state.Requested!, exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Test Trainer (790098)' })).toBeVisible();

  // The whole list, newest first.
  expect(seen.queue.at(-1)).toContain('dir=desc');
  expect(seen.queue.at(-1)).not.toContain('filter[queue]');

  // A queue of work: its view, the one that waited longest first.
  await page.locator('#trainings-queue').click();
  await page.getByRole('option', { name: words.staff.queues.toApprove!, exact: true }).click();
  await expect(page).toHaveURL(/queue=toApprove/);
  await expect.poll(() => seen.queue.at(-1)).toContain('filter[queue]=toApprove');
  expect(seen.queue.at(-1)).toContain('dir=asc');

  // And a ladder.
  await page.locator('#trainings-kind').click();
  await page.getByRole('option', { name: words.kinds.Pilot!, exact: true }).click();
  await expect.poll(() => seen.queue.at(-1)).toContain('filter[kind]=Pilot');
  expect(seen.queue.at(-1)).toContain('filter[queue]=toApprove');

  await expect(page.getByRole('link', { name: words.staff.open }).first()).toHaveAttribute(
    'href',
    '/staff/training/41',
  );
});

test('the page of a request reminds of the theory exam with its site, and the request is accepted', async ({
  page,
}) => {
  const seen = await stubTheStaff(page, {
    initial: training('Requested', { actions: { canDecide: true, canAssign: false } }),
    step: () => ({ status: 200, body: accepted() }),
  });

  await page.goto('/staff/training/41');
  await expect(
    page.getByRole('heading', {
      level: 1,
      name: filled(words.staff.pageTitle, { rating: 'ADC', trainee: 'Test Trainee (790099)' }),
    }),
  ).toBeVisible();

  // The reminder of whoever approves, with the site of the exam; the request as the trainee sent it.
  await expect(
    page.getByText(
      filled(words.staff.theoryReminder.title, { trainee: 'Test Trainee (790099)', rating: 'ADC' }),
    ),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: words.theoryExam })).toHaveAttribute('href', EXAM);
  await expect(page.getByText('AS3', { exact: true })).toBeVisible();
  await expect(page.getByText('Evenings after 18 UTC.')).toBeVisible();
  await expect(page.getByText('XXAA_TWR (XXAA · XXXX)')).toBeVisible();

  // Accepted, asked first, with the version the page read.
  await page.getByRole('button', { name: words.staff.accept.button, exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.staff.accept.button, exact: true })
    .click();

  // Exact: the toast is also announced, for a while, as «Notification …» in a live region of its own.
  await expect(page.getByText(words.staff.accept.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([{ verb: 'accept', body: { rowVersion: '2026-09-20T10:00:00.123456Z' } }]);
  await expect(page.getByText(words.states.Accepted!, { exact: true })).toBeVisible();
  // The day of the decision, not an hour that would have to say whose.
  await expect(
    page.getByText(
      filled(words.staff.decision.accepted, { name: 'Test Coordinator (790097)', date: 'Sep 21, 2026' }),
      { exact: true },
    ),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: words.staff.sections.trainer })).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.accept.button, exact: true })).toHaveCount(0);
});

test('a request is refused with a reason, which the dialog asks for before it lets go', async ({ page }) => {
  const seen = await stubTheStaff(page, {
    initial: training('Requested', { actions: { canDecide: true, canAssign: false } }),
    step: (_verb, body) => ({
      status: 200,
      body: training('Rejected', {
        rejection: 'Staff',
        rejectionReason: body.reason,
        decidedBy: coordinator,
        decidedAt: '2026-09-21T09:00:00Z',
        rowVersion: '2026-09-21T09:00:00.123456Z',
      }),
    }),
  });

  await page.goto('/staff/training/41');
  await page.getByRole('button', { name: words.staff.reject.button, exact: true }).click();

  const dialog = page.getByRole('alertdialog');
  const confirm = dialog.getByRole('button', { name: words.staff.reject.button, exact: true });
  await expect(confirm).toBeDisabled();
  await dialog.getByLabel(words.staff.reject.fields.reason, { exact: true }).fill('More hours first.');
  await confirm.click();

  await expect(page.getByText(words.staff.reject.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([
    { verb: 'reject', body: { reason: 'More hours first.', rowVersion: '2026-09-20T10:00:00.123456Z' } },
  ]);
  await expect(
    page.getByText(filled(words.staff.decision.reason, { reason: 'More hours first.' })),
  ).toBeVisible();
  await expect(page.getByText(words.states.Rejected!, { exact: true })).toBeVisible();
});

test('the trainer is chosen among the ones the server offers, and its refusal lands under the field', async ({
  page,
}) => {
  const seen = await stubTheStaff(page, {
    initial: accepted(),
    step: (_verb, body) =>
      body.trainerVid === 790096
        ? {
            status: 400,
            body: {
              title: 'One or more validation errors occurred.',
              status: 400,
              errors: { trainerVid: ['training:errors.trainerRatingTooLow'] },
            },
          }
        : {
            status: 200,
            body: accepted({
              state: 'Assigned',
              trainer,
              assignedBy: coordinator,
              assignedAt: '2026-09-21T10:00:00Z',
              rowVersion: '2026-09-21T10:00:00.123456Z',
            }),
          },
  });

  await page.goto('/staff/training/41');
  await expect(page.getByText(words.staff.trainer.none)).toBeVisible();

  const field = page.getByText(words.staff.assign.fields.trainerVid, { exact: true }).locator('..');
  await field.getByRole('combobox').click();
  await page.getByRole('option', { name: 'Other Trainer (790096) · APC · XX-TA1', exact: true }).click();
  await page.getByRole('button', { name: words.staff.assign.submit, exact: true }).click();
  await expect(page.getByText(words.errors.trainerRatingTooLow!)).toBeVisible();

  await field.getByRole('combobox').click();
  await page.getByRole('option', { name: 'Test Trainer (790098) · SEC · XX-T01', exact: true }).click();
  await page.getByRole('button', { name: words.staff.assign.submit, exact: true }).click();

  await expect(
    page.getByText(filled(words.staff.assign.assigned, { name: 'Test Trainer (790098)' }), { exact: true }),
  ).toBeVisible();
  expect(seen.steps).toEqual([
    { verb: 'assign', body: { trainerVid: 790096, rowVersion: '2026-09-21T09:00:00.123456Z' } },
    { verb: 'assign', body: { trainerVid: 790098, rowVersion: '2026-09-21T09:00:00.123456Z' } },
  ]);
  await expect(page.getByText(words.states.Assigned!, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.assign.change, exact: true })).toBeVisible();
});

test('a step somebody else overtook reads the page again, says so, and the next one goes from the version read', async ({
  page,
}) => {
  const read = '2026-09-21T09:00:00.123456Z';
  const moved = '2026-09-21T10:00:00.123456Z';
  const seen = await stubTheStaff(page, {
    initial: accepted(),
    step: (_verb, body) =>
      body.rowVersion === read
        ? {
            // Somebody else gave the training to another trainer in the meantime.
            status: 409,
            body: { title: 'Conflict', status: 409 },
            now: accepted({
              state: 'Assigned',
              trainer: { vid: 790096, name: 'Other Trainer' },
              assignedBy: coordinator,
              assignedAt: '2026-09-21T10:00:00Z',
              rowVersion: moved,
            }),
          }
        : {
            status: 200,
            body: accepted({
              state: 'Assigned',
              trainer,
              assignedBy: coordinator,
              assignedAt: '2026-09-21T11:00:00Z',
              rowVersion: '2026-09-21T11:00:00.123456Z',
            }),
          },
  });

  await page.goto('/staff/training/41');
  await expect(page.getByText(words.staff.trainer.none)).toBeVisible();

  const field = page.getByText(words.staff.assign.fields.trainerVid, { exact: true }).locator('..');
  const chooseTheTrainer = async () => {
    await field.getByRole('combobox').click();
    await page.getByRole('option', { name: 'Test Trainer (790098) · SEC · XX-T01', exact: true }).click();
  };

  await chooseTheTrainer();
  await page.getByRole('button', { name: words.staff.assign.submit, exact: true }).click();

  // Said where it stays, and the page as the server has it now: the other trainer, whom the button changes.
  await expect(page.getByText(words.staff.refused, { exact: true })).toBeVisible();
  await expect(page.getByText(conflict, { exact: true })).toBeVisible();
  await expect(
    page.getByText(
      filled(words.staff.trainer.assigned, {
        name: 'Other Trainer (790096)',
        by: 'Test Coordinator (790097)',
        date: 'Sep 21, 2026',
      }),
      { exact: true },
    ),
  ).toBeVisible();

  // The form drawn anew: the next assignment is sent from the version read again, not from the one refused.
  await chooseTheTrainer();
  await page.getByRole('button', { name: words.staff.assign.change, exact: true }).click();
  await expect(
    page.getByText(filled(words.staff.assign.assigned, { name: 'Test Trainer (790098)' }), { exact: true }),
  ).toBeVisible();
  expect(seen.steps).toEqual([
    { verb: 'assign', body: { trainerVid: 790098, rowVersion: read } },
    { verb: 'assign', body: { trainerVid: 790098, rowVersion: moved } },
  ]);
});

test('a reader the server lets do nothing on a training sees no button, and still the reminder', async ({
  page,
}) => {
  await stubTheStaff(page, { bootstrap: trainerBootstrap });

  await page.goto('/staff/training/41');
  await expect(
    page.getByText(
      filled(words.staff.theoryReminder.title, { trainee: 'Test Trainee (790099)', rating: 'ADC' }),
    ),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.accept.button, exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: words.staff.reject.button, exact: true })).toHaveCount(0);
});
