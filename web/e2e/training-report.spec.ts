import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Locator, type Page } from '@playwright/test';

import { anonymousBootstrap, staffBootstrap, stubTheApi } from './fixtures';

/**
 * After the session in a browser, with the API stubbed (M3, A9b): once the session has started, whoever conducts the training
 * marks the sheet — a grade on practice, a mark on theory, an item left «N/A» — with a comment for the trainee and a note for the
 * staff, writes the report as a whole and publishes it, asked first; a refusal lands on the row it is about, and a sheet that
 * changed meanwhile reads the page again; the session is rescheduled with its internal notes, and the dates are proposed again;
 * a no-show is asked first; a mock exam offers no «ready for the mock exam»; a trainer reading their own training is told what is
 * not shown to them. The trainee reads the report — never a note of the staff —, the sessions that are over, and what comes
 * next. What the server decides is proved by `TrainingSessionsTests` (A9a); the round against the real server is
 * `full/training-the-report.spec.ts`.
 */

/** The words of the module and of the core's errors, read from the files the browser fetches. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  states: Record<string, string>;
  mockExam: string;
  outcomes: Record<string, string>;
  marks: Record<string, string>;
  report: {
    notApplicable: string;
    grade: string;
    forTrainee: string;
    forStaff: string;
    generalComment: string;
    staffComment: string;
    publishedBy: string;
    publishedOn: string;
  };
  sessions: { internalNotes: string; recordedBy: string };
  refusal: { waitUntil_other: string };
  request: { send: string };
  mine: {
    readyForMockExam: string;
    readyForExam: string;
    cooldownWaived: string;
    reportReady: string;
    readReport: string;
  };
  detail: {
    next: Record<string, string>;
    askAgain: string;
    report: string;
    sessions: string;
  };
  staff: {
    reserved: string;
    sections: { dates: string; sessions: string; report: string };
    session: { recordable: string };
    dates: { propose: { submit: string } };
    reschedule: { button: string; done: string; fields: { notes: string } };
    noShow: { button: string; confirm: string; done: string };
    report: {
      traineeComment: string;
      staffNote: string;
      publish: string;
      done: string;
      fields: Record<string, string>;
    };
  };
  errors: Record<string, string>;
};

const coreErrors = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/errors.json', import.meta.url)), 'utf8'),
) as { errors: { text: { tooLong: string } } };

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const DAY = 86_400_000;

/** An hour of a day some days away, in UTC, as the API writes an instant. */
function at(days: number, hour: number): string {
  const moment = new Date(Date.now() + days * DAY);
  moment.setUTCHours(hour, 0, 0, 0);
  return moment.toISOString().replace('.000Z', 'Z');
}

/** A day as the pages say it (`useMoment` without the time, in English). */
const day = (instant: string) =>
  new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(instant));

const trainee = { vid: 790099, name: 'Test Trainee' };
const trainer = { vid: 790098, name: 'Test Trainer' };
const coordinator = { vid: 790097, name: 'Test Coordinator' };

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

const traineeBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: trainee.vid,
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

const nothing = {
  canDecide: false,
  canAssign: false,
  canConduct: false,
  canClose: false,
  canRecordOutcome: false,
};
const recording = { ...nothing, canConduct: true, canRecordOutcome: true };

// ---- the staff's side --------------------------------------------------------------------------------------------------

/** The sheet of the rating, as the server hands it for a dated training: the active items, nothing marked. */
const phraseology = {
  itemId: 7,
  section: 'Practice',
  title: { en: 'Radio phraseology', it: 'Fraseologia radio' },
  grade: null,
  mark: null,
  traineeComment: null,
  staffNote: null,
};
const airspace = {
  ...phraseology,
  itemId: 8,
  section: 'Theory',
  title: { en: 'Airspace classes', it: 'Classi di spazio aereo' },
};
const separation = {
  ...phraseology,
  itemId: 9,
  title: { en: 'Traffic separation', it: 'Separazione del traffico' },
};

const session = at(-1, 16);

function staffTraining(state: string, overrides: Record<string, unknown> = {}) {
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
    theoryExamUrl: null,
    availabilityText: 'Evenings after 18 UTC.',
    notesText: null,
    state,
    rejection: null,
    rejectionReason: null,
    decidedBy: coordinator,
    decidedAt: '2026-09-21T09:00:00Z',
    trainer,
    assignedBy: coordinator,
    assignedAt: '2026-09-21T10:00:00Z',
    slots: [],
    scheduledStartUtc: null,
    held: false,
    dateChosenByTrainee: true,
    completedAt: null,
    closedBy: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    cooldownWaived: false,
    generalComment: null,
    staffComment: null,
    sheet: [],
    sessions: [],
    reservedLeftOut: false,
    actions: nothing,
    rowVersion: '2026-09-26T10:00:00.123456Z',
    ...overrides,
  };
}

/** A dated training whose session has started, to a reader who conducts it: the three roads are theirs. */
const dated = (overrides: Record<string, unknown> = {}) =>
  staffTraining('Scheduled', {
    scheduledStartUtc: session,
    held: true,
    sheet: [phraseology, airspace, separation],
    actions: recording,
    ...overrides,
  });

/** A session over, as the staff reads it. */
function over(outcome: string, internalNotes: string | null = null) {
  return {
    id: outcome === 'Held' ? 2 : 1,
    startsAtUtc: session,
    outcome,
    internalNotes,
    recordedBy: trainer,
    recordedAt: '2026-09-27T09:00:00Z',
  };
}

interface Seen {
  steps: { verb: string; body: Record<string, unknown> }[];
  reads: () => number;
}

/**
 * The staff's side of the API: the page as `read` says it at the moment it is asked — a step changes it —, and the steps sent,
 * kept for the test to read, answered as `step` says.
 */
async function stubTheStaff(
  page: Page,
  {
    bootstrap = trainerBootstrap,
    read,
    step = () => ({ status: 500, body: {} }),
  }: {
    bootstrap?: unknown;
    read: () => unknown;
    step?: (verb: string, body: Record<string, unknown>) => { status: number; body: unknown };
  },
): Promise<Seen> {
  const steps: Seen['steps'] = [];
  let reads = 0;
  let answered: unknown = null;

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route('**/api/training/trainings/41', (route) => {
    reads += 1;
    return route.fulfill(json(answered ?? read()));
  });
  await page.route('**/api/training/trainings/41/**', (route) => {
    const verb = new URL(route.request().url()).pathname.replace(/^.*\/trainings\/41\//, '');
    const body = route.request().postDataJSON() as Record<string, unknown>;
    steps.push({ verb, body });
    const answer = step(verb, body);
    if (answer.status === 200) {
      answered = answer.body;
    }
    return route.fulfill(json(answer.body, answer.status));
  });

  return { steps, reads: () => reads };
}

/** The row of the sheet an item's title names, among the rows of the page. */
function rowOf(page: Page, title: string): Locator {
  return page.getByRole('listitem').filter({ hasText: title });
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('after the session the trainer marks the sheet, writes the report and publishes it, asked first', async ({
  page,
}) => {
  const before = dated();
  const published = staffTraining('Completed', {
    scheduledStartUtc: session,
    completedAt: '2026-09-27T09:00:00Z',
    sheet: [
      { ...phraseology, grade: 4, traineeComment: 'Clear readbacks.', staffNote: 'Hesitant on the handoff.' },
      { ...airspace, mark: 'ToImprove' },
      separation,
    ],
    generalComment: 'A good first session.',
    staffComment: 'Ready soon.',
    readyForMockExam: true,
    cooldownWaived: true,
    sessions: [over('Held')],
    rowVersion: '2026-09-27T09:00:00.1Z',
  });

  const seen = await stubTheStaff(page, {
    read: () => before,
    step: () => ({ status: 200, body: published }),
  });

  await page.goto('/staff/training/41');

  // Beside the session, the three roads.
  await expect(page.getByText(words.staff.session.recordable)).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.reschedule.button, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.noShow.button, exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.report })).toBeVisible();

  // The sheet: every item of the rating, «N/A» until something else is chosen.
  const first = rowOf(page, 'Radio phraseology');
  await expect(first.getByRole('radio', { name: words.report.notApplicable, exact: true })).toBeChecked();
  await first.getByRole('radio', { name: '4', exact: true }).click();
  await first.getByLabel(words.staff.report.traineeComment, { exact: true }).fill('Clear readbacks.');
  await first.getByLabel(words.staff.report.staffNote, { exact: true }).fill('Hesitant on the handoff.');

  const second = rowOf(page, 'Airspace classes');
  await expect(second.getByRole('radio', { name: '4', exact: true })).toHaveCount(0);
  await second.getByRole('radio', { name: words.marks.ToImprove!, exact: true }).click();

  // The report as a whole: the generated form.
  const fields = words.staff.report.fields;
  await page.getByLabel(fields.generalComment!, { exact: true }).fill('A good first session.');
  await page.getByLabel(fields.staffComment!, { exact: true }).fill('Ready soon.');
  await page.getByRole('switch', { name: fields.readyForMockExam!, exact: true }).click();
  await page.getByRole('switch', { name: fields.cooldownWaived!, exact: true }).click();

  // Asked first: nothing goes before the confirmation.
  await page.getByRole('button', { name: words.staff.report.publish, exact: true }).click();
  expect(seen.steps).toEqual([]);
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.staff.report.publish, exact: true })
    .click();

  await expect(page.getByText(words.staff.report.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([
    {
      verb: 'report',
      body: {
        sheet: [
          {
            itemId: 7,
            grade: 4,
            mark: null,
            traineeComment: 'Clear readbacks.',
            staffNote: 'Hesitant on the handoff.',
          },
          { itemId: 8, grade: null, mark: 'ToImprove', traineeComment: null, staffNote: null },
          // Left alone: not applicable.
          { itemId: 9, grade: null, mark: null, traineeComment: null, staffNote: null },
        ],
        generalComment: 'A good first session.',
        staffComment: 'Ready soon.',
        readyForMockExam: true,
        readyForExam: false,
        cooldownWaived: true,
        rowVersion: before.rowVersion,
      },
    },
  ]);

  // Published: who and when, the sheet read with the notes of the staff, the comments and the boxes; no road left.
  await expect(
    page.getByText(filled(words.report.publishedBy, { name: 'Test Trainer (790098)', date: 'Sep 27, 2026' })),
  ).toBeVisible();
  await expect(rowOf(page, 'Radio phraseology')).toContainText(filled(words.report.grade, { grade: '4' }));
  await expect(rowOf(page, 'Radio phraseology')).toContainText(
    filled(words.report.forTrainee, { comment: 'Clear readbacks.' }),
  );
  await expect(rowOf(page, 'Radio phraseology')).toContainText(
    filled(words.report.forStaff, { note: 'Hesitant on the handoff.' }),
  );
  await expect(rowOf(page, 'Airspace classes')).toContainText(words.marks.ToImprove!);
  await expect(rowOf(page, 'Traffic separation')).toContainText(words.report.notApplicable);
  await expect(page.getByText('A good first session.', { exact: true })).toBeVisible();
  await expect(page.getByText('Ready soon.', { exact: true })).toBeVisible();
  await expect(page.getByText(words.mine.readyForMockExam, { exact: true })).toBeVisible();
  await expect(page.getByText(words.mine.cooldownWaived, { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.sessions })).toBeVisible();
  await expect(page.getByText(words.outcomes.Held!, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.reschedule.button, exact: true })).toHaveCount(0);
});

test('a refusal lands on the row it is about, and a sheet that changed meanwhile reads the page again', async ({
  page,
}) => {
  const theory = {
    ...airspace,
    itemId: 10,
    title: { en: 'Emergency procedures', it: 'Procedure di emergenza' },
  };
  let changed = false;

  const seen = await stubTheStaff(page, {
    read: () => dated({ sheet: changed ? [phraseology, airspace, theory] : [phraseology, airspace] }),
    step: () => {
      if (seen.steps.length === 1) {
        return {
          status: 400,
          body: {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { 'sheet[1].traineeComment': ['errors.text.tooLong'] },
          },
        };
      }

      changed = true;
      return {
        status: 400,
        body: {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { sheet: ['training:errors.sheetChanged'] },
        },
      };
    },
  });

  await page.goto('/staff/training/41');
  await rowOf(page, 'Radio phraseology').getByRole('radio', { name: '3', exact: true }).click();
  await rowOf(page, 'Airspace classes')
    .getByLabel(words.staff.report.traineeComment, { exact: true })
    .fill('A text the server finds too long.');

  const publish = async () => {
    await page.getByRole('button', { name: words.staff.report.publish, exact: true }).click();
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: words.staff.report.publish, exact: true })
      .click();
  };

  // The second row's comment, refused on its own field: `sheet[1]` is the second row on screen.
  await publish();
  await expect(rowOf(page, 'Airspace classes')).toContainText(coreErrors.errors.text.tooLong);
  await expect(rowOf(page, 'Radio phraseology')).not.toContainText(coreErrors.errors.text.tooLong);

  // The sheet changed meanwhile: said above the form, the page read again with the item added, what was written kept.
  await publish();
  await expect(page.getByText(words.errors.sheetChanged!, { exact: true })).toBeVisible();
  await expect(rowOf(page, 'Emergency procedures')).toBeVisible();
  await expect(rowOf(page, 'Radio phraseology').getByRole('radio', { name: '3', exact: true })).toBeChecked();
  expect(seen.steps).toHaveLength(2);
  expect(seen.reads()).toBeGreaterThan(1);
});

test('a session rescheduled keeps its internal notes among the sessions, and the dates are proposed again', async ({
  page,
}) => {
  const seen = await stubTheStaff(page, {
    read: () => dated(),
    step: () => ({
      status: 200,
      body: staffTraining('Assigned', {
        sessions: [over('Rescheduled', 'Two aircraft in an hour.')],
        actions: { ...nothing, canConduct: true },
        rowVersion: '2026-09-27T09:00:00.1Z',
      }),
    }),
  });

  await page.goto('/staff/training/41');
  await page.getByRole('button', { name: words.staff.reschedule.button, exact: true }).click();
  const dialog = page.getByRole('alertdialog');
  await dialog
    .getByLabel(words.staff.reschedule.fields.notes, { exact: true })
    .fill('Two aircraft in an hour.');
  await dialog.getByRole('button', { name: words.staff.reschedule.button, exact: true }).click();

  await expect(page.getByText(words.staff.reschedule.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([
    { verb: 'reschedule', body: { notes: 'Two aircraft in an hour.', rowVersion: dated().rowVersion } },
  ]);

  // Back to its dates: the proposal is there again, and the session is among the ones that are over, with its notes.
  await expect(
    page.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }),
  ).toBeVisible();
  // By its badge: the notice in the corner says «rescheduled» too, in an entry of a list of its own.
  const sessions = page
    .getByRole('listitem')
    .filter({ has: page.getByText(words.outcomes.Rescheduled!, { exact: true }) });
  await expect(sessions).toContainText(
    filled(words.sessions.internalNotes, { notes: 'Two aircraft in an hour.' }),
  );
  await expect(sessions).toContainText(
    filled(words.sessions.recordedBy, { name: 'Test Trainer (790098)', date: 'Sep 27, 2026' }),
  );
});

test('a no-show is asked first, and closes the training', async ({ page }) => {
  const seen = await stubTheStaff(page, {
    read: () => dated(),
    step: () => ({
      status: 200,
      body: staffTraining('NoShow', {
        closedBy: trainer,
        closedAt: '2026-09-27T09:00:00Z',
        sessions: [over('NoShow')],
        rowVersion: '2026-09-27T09:00:00.1Z',
      }),
    }),
  });

  await page.goto('/staff/training/41');
  await page.getByRole('button', { name: words.staff.noShow.button, exact: true }).click();
  expect(seen.steps).toEqual([]);
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.staff.noShow.confirm, exact: true })
    .click();

  await expect(page.getByText(words.staff.noShow.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([{ verb: 'no-show', body: { rowVersion: dated().rowVersion } }]);
  await expect(page.getByText(words.states.NoShow!, { exact: true }).first()).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.report })).toHaveCount(0);
});

test('a mock exam offers no «ready for the mock exam», and a trainer reading their own training is told what is not shown', async ({
  page,
}) => {
  let own = false;
  await stubTheStaff(page, {
    read: () =>
      own
        ? staffTraining('Completed', {
            trainee: trainer,
            completedAt: '2026-09-27T09:00:00Z',
            // What the server leaves out for the training's trainee: the notes of the sheet, the comment, the session's notes.
            sheet: [{ ...phraseology, grade: 5, traineeComment: 'Well done.' }],
            generalComment: 'Well done.',
            sessions: [over('Held')],
            reservedLeftOut: true,
          })
        : dated({ isMockExam: true }),
  });

  await page.goto('/staff/training/41');
  const fields = words.staff.report.fields;
  await expect(page.getByRole('switch', { name: fields.readyForExam!, exact: true })).toBeVisible();
  await expect(page.getByRole('switch', { name: fields.readyForMockExam!, exact: true })).toHaveCount(0);

  own = true;
  await page.reload();
  await expect(page.getByText(words.staff.reserved, { exact: true })).toBeVisible();
  await expect(page.getByText(words.report.staffComment, { exact: true })).toHaveCount(0);
  await expect(page.getByText(filled(words.report.forStaff, { note: '' }).trim())).toHaveCount(0);
});

// ---- the trainee's side ------------------------------------------------------------------------------------------------

function mineOne(state: string, overrides: Record<string, unknown> = {}) {
  return {
    id: 41,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    state,
    rejection: null,
    rejectionReason: null,
    availabilityText: null,
    notesText: null,
    requestedAt: '2026-09-20T10:00:00Z',
    decidedAt: '2026-09-21T09:00:00Z',
    trainer,
    slots: [],
    scheduledStartUtc: session,
    held: false,
    completedAt: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    cooldownWaived: false,
    generalComment: null,
    sheet: [],
    sessions: [],
    rowVersion: '2026-09-27T09:00:00.1Z',
    ...overrides,
  };
}

/** The ATC ladder as the trainee's page answers it after the training: what they may ask for next, or until when they wait. */
function atcPath(overrides: Record<string, unknown> = {}) {
  return {
    kind: 'Atc',
    ratingShortName: 'AS3',
    hours: 120,
    next: { kind: 'Atc', number: 5, shortName: 'ADC', nameKey: 'ratings.Atc.ADC' },
    isMockExam: false,
    asksPosition: true,
    positions: [{ callsign: 'XXAA_TWR', name: 'Example Tower', ratingShortName: 'ADC' }],
    refusal: null,
    bannedUntil: null,
    openTrainingId: null,
    waitUntil: null,
    minimumHours: null,
    ...overrides,
  };
}

async function stubTheTrainee(page: Page, one: unknown, path: unknown): Promise<void> {
  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(traineeBootstrap)));
  await page.route('**/api/training/mine', (route) =>
    route.fulfill(
      json({
        vid: trainee.vid,
        name: trainee.name,
        asksTheory: false,
        theoryExamUrl: null,
        paths: [path],
        trainings: [one],
      }),
    ),
  );
  await page.route('**/api/training/mine/41', (route) => route.fulfill(json(one)));
}

test('the trainee reads the report, never a note of the staff, the sessions over, and that the next training is a mock exam', async ({
  page,
}) => {
  await stubTheTrainee(
    page,
    mineOne('Completed', {
      completedAt: '2026-09-27T09:00:00Z',
      sheet: [
        {
          section: 'Practice',
          title: phraseology.title,
          grade: 4,
          mark: null,
          traineeComment: 'Clear readbacks.',
        },
        { section: 'Theory', title: airspace.title, grade: null, mark: 'ToImprove', traineeComment: null },
        { section: 'Practice', title: separation.title, grade: null, mark: null, traineeComment: null },
      ],
      generalComment: 'A good first session.',
      readyForMockExam: true,
      cooldownWaived: true,
      sessions: [
        { startsAtUtc: at(-8, 16), outcome: 'Rescheduled' },
        { startsAtUtc: session, outcome: 'Held' },
      ],
    }),
    atcPath({ isMockExam: true }),
  );

  // From their trainings: the report is there, and its boxes.
  await page.goto('/training/mine');
  await expect(page.getByText(words.mine.reportReady, { exact: true })).toBeVisible();
  await expect(page.getByText(words.mine.readyForMockExam, { exact: true })).toBeVisible();
  await expect(page.getByText(words.mine.cooldownWaived, { exact: true })).toBeVisible();
  await page.getByRole('link', { name: words.mine.readReport, exact: true }).click();
  await expect(page).toHaveURL(/\/training\/mine\/41$/);

  // What comes next: the report below, and the next training — a mock exam, as agreed with the trainer.
  await expect(page.getByText(words.detail.next.Completed!, { exact: true })).toBeVisible();
  await expect(page.getByText(words.detail.askAgain, { exact: true })).toBeVisible();
  await expect(page.getByText(words.mockExam, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.request.send, exact: true })).toHaveAttribute(
    'href',
    '/training/request?kind=Atc',
  );

  // The report: the grade, the mark, «N/A», their comment and the general one, the boxes.
  await expect(page.getByRole('heading', { level: 2, name: words.detail.report })).toBeVisible();
  await expect(page.getByText(filled(words.report.publishedOn, { date: 'Sep 27, 2026' }))).toBeVisible();
  await expect(rowOf(page, 'Radio phraseology')).toContainText(filled(words.report.grade, { grade: '4' }));
  await expect(rowOf(page, 'Radio phraseology')).toContainText('Clear readbacks.');
  await expect(rowOf(page, 'Airspace classes')).toContainText(words.marks.ToImprove!);
  await expect(rowOf(page, 'Traffic separation')).toContainText(words.report.notApplicable);
  await expect(page.getByText('A good first session.', { exact: true })).toBeVisible();

  // The sessions that are over, rescheduled and held.
  await expect(page.getByRole('heading', { level: 2, name: words.detail.sessions })).toBeVisible();
  await expect(page.getByText(words.outcomes.Rescheduled!, { exact: true })).toBeVisible();
  await expect(page.getByText(words.outcomes.Held!, { exact: true })).toBeVisible();

  // Nothing of the staff's is drawn: not the note's label, not the session's.
  const shown = await page.locator('body').innerText();
  expect(shown).not.toContain(filled(words.report.forStaff, { note: '' }).trim());
  expect(shown).not.toContain(filled(words.sessions.internalNotes, { notes: '' }).trim());
  expect(shown).not.toContain(words.report.staffComment);
});

test('after a no-show the trainee reads why it closed, the session, and until when they wait', async ({
  page,
}) => {
  // Three days and a half away: four days to go, however long the page takes to draw.
  const until = new Date(Date.now() + 3.5 * DAY).toISOString();
  await stubTheTrainee(
    page,
    mineOne('NoShow', {
      closedAt: '2026-09-27T09:00:00Z',
      sessions: [{ startsAtUtc: session, outcome: 'NoShow' }],
    }),
    atcPath({ refusal: 'training:errors.requestWaiting', waitUntil: until }),
  );

  await page.goto('/training/mine/41');
  await expect(page.getByText(words.detail.next.NoShow!, { exact: true })).toBeVisible();
  await expect(
    page.getByText(
      filled(words.refusal.waitUntil_other, {
        date: new Intl.DateTimeFormat('en', {
          dateStyle: 'medium',
          timeStyle: 'short',
          hour12: false,
          timeZone: 'UTC',
        }).format(new Date(until)),
        count: '4',
      }),
    ),
  ).toBeVisible();
  await expect(
    page.getByRole('listitem').filter({ has: page.getByText(words.outcomes.NoShow!, { exact: true }) }),
  ).toContainText(day(session));
  await expect(page.getByRole('heading', { level: 2, name: words.detail.report })).toHaveCount(0);
});
