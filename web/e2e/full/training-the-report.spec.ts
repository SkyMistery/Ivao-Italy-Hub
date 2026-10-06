import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor, test } from './bench';

/**
 * The "done when" of A9 (M3), through the real screens against the real server: once the session of a pilot training of the
 * bench's trainee has started, its trainer reschedules it from the page with internal notes — the training waits for its date
 * again —; dated again, the trainer marks the sheet from the page — a grade, a mark, one item left «N/A» — with a comment for the
 * trainee and a note for the staff, writes the report with a comment for the staff, ticks «ready for the mock exam» and takes the
 * waiting away, and publishes it. The trainee reads the report from their trainings — the grade, the mark, «N/A», their comments,
 * the sessions over — **without any note of the staff's**, receives the mail of the report, and their next request on the rating
 * says it will be a mock exam, as agreed with the trainer.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`), the trainer `?as=trainer`, the staff the bench's web master
 * (every permission, not a trainer). The training is asked for, accepted, assigned and dated through the API — A6's, A7's and A8's
 * steps, whose pages their own rounds drive —, a few seconds ahead, and the run waits for its session to start: nobody dates a
 * training in the past (the maintainer's answer on #149). The sheet of its rating is written here, through the API, and switched off at the
 * end: an item a report marked is never deleted (A9a). ⚠️ The report leaves the training completed for good, its session in the
 * public calendar, and the trainee's next pilot request a mock exam with no waiting: this file's name sorts after every other
 * round of the training — Playwright runs the files in the order of their names, one worker —, and the bench is made anew
 * before every run.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const BENCH_TRAINER = 999004;
const TRAINEE_ADDRESS = 'bench-pilot@bench.test';
const REASON = 'trn-test: closed by the round of the report.';

/** In the title of every item of this file, in both languages: what finds them again, whatever language the reader has. */
const marker = 'trn-report';
const stamp = Date.now().toString(36);

/** The sheet of the pilot rating, as this run writes it: two items of practice and one of theory, after any the bench has. */
const items = [
  {
    section: 'Practice',
    en: `${marker} ${stamp} Radio phraseology`,
    it: `${marker} ${stamp} Fraseologia radio`,
  },
  {
    section: 'Theory',
    en: `${marker} ${stamp} Airspace classes`,
    it: `${marker} ${stamp} Classi di spazio aereo`,
  },
  {
    section: 'Practice',
    en: `${marker} ${stamp} Holding patterns`,
    it: `${marker} ${stamp} Circuiti di attesa`,
  },
] as const;

/** What the staff writes that the trainee must never read: each marked, so that a leak is found by its words. */
const STAFF_NOTE = 'trn-test STAFF NOTE: hesitant on the handoff.';
const STAFF_COMMENT = 'trn-test STAFF COMMENT: ready for the mock exam soon.';
const INTERNAL_NOTES = 'trn-test INTERNAL NOTES: two aircraft in an hour.';
const TRAINEE_COMMENT = 'trn-test: clear readbacks.';
const GENERAL_COMMENT = 'trn-test: a good session, keep training the handoffs.';

interface MyTraining {
  readonly asksTheory: boolean;
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly isMockExam: boolean;
    readonly asksPosition: boolean;
    readonly positions: readonly { readonly callsign: string }[];
    readonly refusal: string | null;
  }[];
}

interface StaffTraining {
  readonly state: string;
  readonly rowVersion: string;
  readonly actions: { readonly canRecordOutcome: boolean };
}

interface SheetItemRow {
  readonly id: number;
  readonly kind: string;
  readonly rating: number;
  readonly section: string;
  readonly title: Record<string, string>;
  readonly sort: number;
  readonly isActive: boolean;
  readonly rowVersion: string;
}

test('the trainer reschedules, then publishes a report with an item N/A and «ready for the mock exam»; the trainee reads it without a note of the staff', async ({
  context,
  browser,
  afterwards,
}) => {
  test.setTimeout(300_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  const trainer = await browser.newContext({ baseURL: benchUrl });
  for (const reader of [trainee, trainer]) {
    await readInEnglish(reader);
  }

  // What this run opens, so that the end closes whatever did not reach its report.
  const opened: number[] = [];

  afterwards(async () => {
    for (const id of opened) {
      await closeIfOpen(context.request, trainee.request, id);
    }
    await retireLeftovers(context.request);

    await trainee.close();
    await trainer.close();
  });

  await signIn(context, null);
  // In the roster before anybody assigns them a training: the staff of the training is whoever signed in once.
  await signIn(trainer, 'trainer');
  await signIn(trainee, 'pilot');
  await retireLeftovers(context.request);

  // ---------------------------------------------------------------- the pilot ladder, and the sheet of its rating
  const standing = await mine(trainee.request);
  const pilot = standing.paths.find((path) => path.kind === 'Pilot')!;
  expect(
    pilot.refusal,
    'the trainee may ask for a pilot training: the bench is made anew before a run',
  ).toBeNull();
  expect(
    pilot.isMockExam,
    'the next pilot training is no mock exam yet: the bench is made anew before a run',
  ).toBe(false);
  const rating = pilot.next!.number;

  for (const [index, item] of items.entries()) {
    const written = await context.request.post('/api/training/sheet-items', {
      headers: asTheClientDoes,
      data: {
        kind: 'Pilot',
        rating,
        section: item.section,
        title: { en: item.en, it: item.it },
        // After whatever the bench's sheet holds, in this order: the places go up to 999.
        sort: 990 + index,
        isActive: true,
        rowVersion: '0001-01-01T00:00:00',
      },
    });
    expect(written.status(), await written.text()).toBeLessThan(300);
  }

  // ---------------------------------------------------------------- asked, accepted, assigned, its session started
  const id = await acceptedAndAssigned(trainee.request, context.request, standing, opened);
  await startedInAMoment(context.request, id);

  // In the session they signed in with at the start: an assignment changes nothing of theirs (A7b).
  const trainerPage = await trainer.newPage();
  const complaints = watch(trainerPage);

  // ---------------------------------------------------------------- rescheduled from the page, with internal notes
  await trainerPage.goto(`/staff/training/${String(id)}`);
  await expect(trainerPage.getByText(words.staff.session.recordable)).toBeVisible();
  await trainerPage.getByRole('button', { name: words.staff.reschedule.button, exact: true }).click();
  const dialog = trainerPage.getByRole('alertdialog');
  await dialog.getByLabel(words.staff.reschedule.fields.notes, { exact: true }).fill(INTERNAL_NOTES);
  await whileWaitingFor(trainerPage, 'POST', `/api/training/trainings/${String(id)}/reschedule`, async () => {
    await dialog.getByRole('button', { name: words.staff.reschedule.button, exact: true }).click();
  });
  await expect(trainerPage.getByText(words.staff.reschedule.done, { exact: true })).toBeVisible();

  // Back to its dates: the proposal again, and the session among the ones over, with its notes.
  await expect(
    trainerPage.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }),
  ).toBeVisible();
  await expect(
    trainerPage.getByText(filled(words.sessions.internalNotes, { notes: INTERNAL_NOTES }), { exact: true }),
  ).toBeVisible();

  // ---------------------------------------------------------------- dated again, and the report written from the page
  await startedInAMoment(context.request, id);
  await trainerPage.reload();
  await expect(
    trainerPage.getByRole('heading', { level: 2, name: words.staff.sections.report }),
  ).toBeVisible();

  const [phraseology, airspace, holding] = items.map((item) => rowOf(trainerPage, item.en));
  await phraseology!.getByRole('radio', { name: '4', exact: true }).click();
  await phraseology!.getByLabel(words.staff.report.traineeComment, { exact: true }).fill(TRAINEE_COMMENT);
  await phraseology!.getByLabel(words.staff.report.staffNote, { exact: true }).fill(STAFF_NOTE);
  await airspace!.getByRole('radio', { name: words.marks.Done, exact: true }).click();
  // The third is left as it is: «N/A», the session did not touch it.
  await expect(holding!.getByRole('radio', { name: words.report.notApplicable, exact: true })).toBeChecked();

  const fields = words.staff.report.fields;
  await trainerPage.getByLabel(fields.generalComment, { exact: true }).fill(GENERAL_COMMENT);
  await trainerPage.getByLabel(fields.staffComment, { exact: true }).fill(STAFF_COMMENT);
  await trainerPage.getByRole('switch', { name: fields.readyForMockExam, exact: true }).click();
  await trainerPage.getByRole('switch', { name: fields.cooldownWaived, exact: true }).click();

  // Mails already there, so that the one of this report is told apart.
  const known = (await mailsTo(context.request, TRAINEE_ADDRESS)).map((mail) => mail.ID);

  await trainerPage.getByRole('button', { name: words.staff.report.publish, exact: true }).click();
  await whileWaitingFor(trainerPage, 'POST', `/api/training/trainings/${String(id)}/report`, async () => {
    await trainerPage
      .getByRole('alertdialog')
      .getByRole('button', { name: words.staff.report.publish, exact: true })
      .click();
  });
  await expect(trainerPage.getByText(words.staff.report.done, { exact: true })).toBeVisible();

  // Published: the item left alone reads «N/A», and the staff reads its own note.
  await expect(rowOf(trainerPage, items[2].en)).toContainText(words.report.notApplicable);
  await expect(rowOf(trainerPage, items[0].en)).toContainText(
    filled(words.report.forStaff, { note: STAFF_NOTE }),
  );
  await expect(trainerPage.getByText(STAFF_COMMENT, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the trainee reads it, and nothing of the staff's
  const traineePage = await trainee.newPage();
  const theirComplaints = watch(traineePage);

  await traineePage.goto('/training/mine');
  await expect(traineePage.getByText(words.mine.reportReady, { exact: true }).first()).toBeVisible();
  // Newest first: the training of this run.
  await traineePage.getByRole('link', { name: words.mine.readReport, exact: true }).first().click();
  await expect(traineePage).toHaveURL(new RegExp(`/training/mine/${String(id)}$`));

  await expect(traineePage.getByRole('heading', { level: 2, name: words.detail.report })).toBeVisible();
  await expect(rowOf(traineePage, items[0].en)).toContainText(filled(words.report.grade, { grade: '4' }));
  await expect(rowOf(traineePage, items[0].en)).toContainText(TRAINEE_COMMENT);
  await expect(rowOf(traineePage, items[1].en)).toContainText(words.marks.Done);
  await expect(rowOf(traineePage, items[2].en)).toContainText(words.report.notApplicable);
  await expect(traineePage.getByText(GENERAL_COMMENT, { exact: true })).toBeVisible();
  await expect(traineePage.getByText(words.mine.readyForMockExam, { exact: true })).toBeVisible();
  await expect(traineePage.getByText(words.mine.cooldownWaived, { exact: true })).toBeVisible();
  await expect(traineePage.getByText(words.outcomes.Rescheduled, { exact: true })).toBeVisible();
  await expect(traineePage.getByText(words.outcomes.Held, { exact: true })).toBeVisible();

  const read = await traineePage.locator('body').innerText();
  for (const reserved of [STAFF_NOTE, STAFF_COMMENT, INTERNAL_NOTES]) {
    expect(read, `the trainee never reads what the staff wrote for itself: ${reserved}`).not.toContain(
      reserved,
    );
  }

  // What comes next: no waiting, and the next training on the rating is a mock exam.
  await expect(traineePage.getByText(words.mockExam, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the next request says so
  await traineePage.goto('/training/request?kind=Pilot');
  await expect(traineePage.getByText(words.mockExam, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the mail of the report, to the trainee
  const subjects = [words.mail.training.reportPublished.subject, italianReportSubject()].map(
    (subject) => subject.split('{{')[0]!,
  );
  await expect
    .poll(
      async () =>
        (await mailsTo(context.request, TRAINEE_ADDRESS)).filter(
          (mail) => !known.includes(mail.ID) && subjects.some((subject) => mail.Subject.startsWith(subject)),
        ).length,
      { message: `the mail of the report to ${TRAINEE_ADDRESS}`, timeout: 150_000, intervals: [5_000] },
    )
    .toBe(1);

  expect(complaints).toEqual([]);
  expect(theirComplaints).toEqual([]);
});

/** The errors a page writes in its console, and a page that throws fails the run. */
function watch(watched: Page): string[] {
  const complaints: string[] = [];
  watched.on('console', (message) => {
    if (message.type() === 'error' && !message.text().includes('favicon')) {
      complaints.push(message.text());
    }
  });
  watched.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  return complaints;
}

async function signIn(context: BrowserContext, as: 'pilot' | 'trainer' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

/**
 * The session dated by hand a few seconds ahead, and waited for until the server says it may be recorded: nobody dates a training
 * in the past, and a session is recorded from its start (the maintainer's answers on #149).
 */
async function startedInAMoment(staff: APIRequestContext, id: number): Promise<void> {
  await step(staff, id, 'date', {
    startsAtUtc: new Date(Date.now() + 10_000).toISOString(),
    confirmed: true,
  });
  await expect
    .poll(
      async () => {
        const now = await staff.get(`/api/training/trainings/${String(id)}`);
        expect(now.status(), await now.text()).toBe(200);
        return ((await now.json()) as StaffTraining).actions.canRecordOutcome;
      },
      { message: 'the session has started', timeout: 60_000, intervals: [2_000] },
    )
    .toBe(true);
}

/** The row of the sheet an item's title names. */
function rowOf(on: Page, title: string) {
  return on.getByRole('listitem').filter({ hasText: title });
}

async function mine(request: APIRequestContext): Promise<MyTraining> {
  const response = await request.get('/api/training/mine');
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as MyTraining;
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

/** A pilot training of the trainee's asked for, accepted and assigned to the bench's trainer, through the API. */
async function acceptedAndAssigned(
  trainee: APIRequestContext,
  staff: APIRequestContext,
  standing: MyTraining,
  opened: number[],
): Promise<number> {
  const path = standing.paths.find((candidate) => candidate.kind === 'Pilot')!;
  const asked = await trainee.post('/api/training/mine', {
    headers: asTheClientDoes,
    data: {
      kind: 'Pilot',
      rating: path.next!.number,
      position: path.asksPosition ? path.positions[0]!.callsign : null,
      availabilityText: null,
      notesText: null,
      theoryPassed: standing.asksTheory ? true : null,
    },
  });
  expect(asked.status(), await asked.text()).toBe(201);
  const id = ((await asked.json()) as { id: number }).id;
  opened.push(id);

  await step(staff, id, 'accept', {});
  await step(staff, id, 'assign', { trainerVid: BENCH_TRAINER });
  return id;
}

/**
 * A training this run opened, closed by the staff while it goes on, or taken back by its trainee while nobody accepted it; one
 * whose session has started is recorded as a no-show instead, since nobody closes over it (#149).
 */
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
  } else if (training.state === 'Scheduled' && training.actions.canRecordOutcome) {
    await step(staff, id, 'no-show', {});
  } else if (['Accepted', 'Assigned', 'Scheduled'].includes(training.state)) {
    await step(staff, id, 'close', { reason: REASON });
  }
}

/**
 * The items of this file a run left on the sheet, recognised by the marker: deleted when no report used them, switched off when
 * one did — a report keeps its copy, and an item it marked is never deleted (A9a).
 */
async function retireLeftovers(staff: APIRequestContext): Promise<void> {
  const response = await staff.get(`/api/training/sheet-items?pageSize=100&q=${marker}`);
  expect(response.status(), await response.text()).toBe(200);

  for (const item of ((await response.json()) as { items: SheetItemRow[] }).items) {
    const removed = await staff.delete(`/api/training/sheet-items/${String(item.id)}`, {
      headers: asTheClientDoes,
    });
    if (removed.status() < 300 || !item.isActive) {
      continue;
    }

    expect(await removed.text()).toContain('training:errors.sheetItemUsed');
    const switchedOff = await staff.put(`/api/training/sheet-items/${String(item.id)}`, {
      headers: asTheClientDoes,
      data: {
        kind: item.kind,
        rating: item.rating,
        section: item.section,
        title: item.title,
        sort: item.sort,
        isActive: false,
        rowVersion: item.rowVersion,
      },
    });
    expect(switchedOff.status(), await switchedOff.text()).toBeLessThan(300);
  }
}

/** The messages Mailpit holds for one address, newest first: its ID and its subject. */
async function mailsTo(
  request: APIRequestContext,
  address: string,
): Promise<{ ID: string; Subject: string }[]> {
  const list = await request.get(`${mailpit}/api/v1/messages?limit=200`);
  expect(list.status(), `Mailpit at ${mailpit}`).toBe(200);

  const messages = (
    (await list.json()) as { messages: { ID: string; Subject: string; To: { Address: string }[] }[] }
  ).messages;
  return messages.filter((message) => message.To.some((recipient) => recipient.Address === address));
}

/** A sentence of the language file with its values in. */
function filled(sentence: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);
}

/** The subject of the mail in Italian: the trainee reads their mails in their own language, which the bench does not fix. */
function italianReportSubject(): string {
  return (
    JSON.parse(
      readFileSync(fileURLToPath(new URL('../../../locales/it/training.json', import.meta.url)), 'utf8'),
    ) as { mail: { training: { reportPublished: { subject: string } } } }
  ).mail.training.reportPublished.subject;
}

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    mockExam: string;
    outcomes: { Held: string; Rescheduled: string };
    marks: { Done: string };
    report: { notApplicable: string; grade: string; forStaff: string };
    sessions: { internalNotes: string };
    mine: { reportReady: string; readReport: string; readyForMockExam: string; cooldownWaived: string };
    detail: { report: string };
    staff: {
      sections: { report: string };
      session: { recordable: string };
      dates: { propose: { submit: string } };
      reschedule: { button: string; done: string; fields: { notes: string } };
      report: {
        traineeComment: string;
        staffNote: string;
        publish: string;
        done: string;
        fields: {
          generalComment: string;
          staffComment: string;
          readyForMockExam: string;
          cooldownWaived: string;
        };
      };
    };
    mail: { training: { reportPublished: { subject: string } } };
  };
}
