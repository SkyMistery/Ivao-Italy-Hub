import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor, test } from './bench';

/**
 * The full round of M3 (A12d; design M3 §0.1, §10): one ATC training of the bench's trainee, from the request to the report, through
 * the real screens against the real server — every step taken by whoever takes it, on their own page, and none through the API. The
 * trainee asks for the next training on a position of the division, answering «yes» to the question on the theory; the staff finds
 * the request among those to approve, reads it with the reminder of the theory, accepts it and assigns it to the bench's trainer;
 * the trainer proposes two dates from the page of the training; the trainee chooses the first among the tiles of their page, and
 * its session is in the public calendar with nobody's name; once the session has started, the trainer marks the sheet and
 * publishes the report; the trainee reads it without anything the staff wrote for itself; the staff finds the training in the
 * history, and on its page every step of the round with who took it (A13b); and the mail of every step reached whoever it is for, once.
 *
 * The rounds of the phases prove each step on its own, and set up the steps before theirs through the API (A6b–A10c); this one is
 * the chain, as the training department will live it. What each step refuses, and the other roads — a refusal, a reschedule, a
 * no-show, a closing, a ban —, are theirs.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`, AS3), the staff the bench's web master (every permission, and not
 * a trainer), the trainer `?as=trainer` (a trainer's position, SEC), signed in at the start: the staff of the training is whoever
 * signed in once. The sheet of the rating is written through the API — composing it is the round of A5 — and switched off at the
 * end: an item a report marked is never deleted (A9a).
 *
 * Nobody dates a training in the past, and a session is reported once it has started (the maintainer's answers on #149): the
 * trainer proposes a first date a minute or two ahead, and the run waits for it, as the round of the report does.
 *
 * ⚠️ A report is for good: the training stays completed in the register, its session in the calendar. So that the rounds after this
 * one — and this one again, on a bench that survived — find the ATC ladder free, the report takes the waiting away and marks
 * nothing for the mock exam. The position is the second the division offers, which no other round asks for: the subjects of the
 * mails name it, and so tell this run's mails from the ones other rounds left on their way. Whatever a run stopped half way left
 * going on the ATC ladder is taken back at the start, and this run's training at the end, if it did not reach its report.
 */

const words = englishTraining();
const italian = italianSubjects();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const TRAINEE = 999002;
const BENCH_TRAINER = 999004;
const TRAINEE_ADDRESS = 'bench-pilot@bench.test';
const TRAINER_ADDRESS = 'bench-trainer@bench.test';

/** What names the bench's trainee and trainer: their names and their VIDs, which a visitor never reads. */
const PEOPLE = ['Bench Pilot', 'Bench Trainer', String(TRAINEE), String(BENCH_TRAINER)];

/** The states of a training still going on (§2.1): what a run that stopped half way can leave on the ladder. */
const GOING = ['Requested', 'Accepted', 'Assigned', 'Scheduled'];

const REASON = 'trn-test: closed by the full round.';

/** In the title of every item of this file, in both languages: what finds them again, whatever language the reader has. */
const marker = 'trn-round';
const stamp = Date.now().toString(36);

/** The sheet of the ATC rating, as this run writes it: an item of practice and one of theory, after any the bench has. */
const items = [
  {
    section: 'Practice',
    en: `${marker} ${stamp} Phraseology on the frequency`,
    it: `${marker} ${stamp} Fraseologia in frequenza`,
  },
  {
    section: 'Theory',
    en: `${marker} ${stamp} Separation minima`,
    it: `${marker} ${stamp} Minime di separazione`,
  },
] as const;

/** What the trainee writes when they ask, and what the trainer writes in the report: each marked, so that it is found by its words. */
const AVAILABILITY = `trn-test ${stamp}: weekday evenings, from 18 UTC.`;
const NOTES = `trn-test ${stamp}: my first training on this position.`;
const TRAINEE_COMMENT = `trn-test ${stamp}: clean readbacks.`;
const GENERAL_COMMENT = `trn-test ${stamp}: a good first session on the position.`;

/** What the staff writes for itself, which the trainee must never read. */
const STAFF_NOTE = `trn-test ${stamp} STAFF NOTE: slow on the handoffs.`;
const STAFF_COMMENT = `trn-test ${stamp} STAFF COMMENT: ready for a second session.`;

type MailType =
  | 'requestReceived'
  | 'requestAccepted'
  | 'trainerAssigned'
  | 'datesProposed'
  | 'dateConfirmed'
  | 'reportPublished';

interface MyTraining {
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly isMockExam: boolean;
    readonly positions: readonly { readonly callsign: string; readonly name: string }[];
    readonly refusal: string | null;
  }[];
  readonly trainings: readonly {
    readonly id: number;
    readonly kind: string;
    readonly state: string;
    readonly position: string | null;
    readonly rowVersion: string;
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

interface Message {
  readonly ID: string;
  readonly Subject: string;
  readonly To: readonly { readonly Address: string }[];
}

test('the full round: asked for, accepted, assigned, dates proposed and one chosen, the report published and read, a mail at every step', async ({
  page,
  context,
  browser,
  afterwards,
}) => {
  test.setTimeout(600_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  const trainer = await browser.newContext({ baseURL: benchUrl });
  const visitor = await browser.newContext({ baseURL: benchUrl });
  for (const reader of [trainee, trainer, visitor]) {
    await readInEnglish(reader);
  }

  // The training of this run, once asked for: the end takes it back if it did not reach its report.
  let id: number | null = null;

  afterwards(async () => {
    if (id !== null) {
      await takeBack(context.request, trainee.request, id);
    }
    await retireItems(context.request);

    await trainee.close();
    await trainer.close();
    await visitor.close();
  });

  await signIn(context, null);
  // In the roster before anybody assigns them a training: the staff of the training is whoever signed in once.
  await signIn(trainer, 'trainer');
  await signIn(trainee, 'pilot');
  await takeBackLeftovers(context.request, trainee.request);
  await retireItems(context.request);

  // ---------------------------------------------------------------- the ATC ladder, free, and the sheet of its rating
  const standing = await mine(trainee.request);
  const atc = standing.paths.find((path) => path.kind === 'Atc')!;
  expect(
    atc.refusal,
    'the trainee may ask for an ATC training: what an earlier run left going on the ladder was taken back above',
  ).toBeNull();
  expect(atc.isMockExam, 'the next ATC training is no mock exam: no round marks one on this ladder').toBe(
    false,
  );
  expect(atc.positions.length, 'the division offers a second position for the ATC training').toBeGreaterThan(
    1,
  );
  const rating = atc.next!;
  const position = atc.positions[1]!;
  // What the mails say the training is: its ladder, rating and position, alike in both languages of the bench.
  const training = `${words.kinds.Atc} · ${rating.shortName} · ${position.callsign}`;

  for (const [index, item] of items.entries()) {
    const written = await context.request.post('/api/training/sheet-items', {
      headers: asTheClientDoes,
      data: {
        kind: 'Atc',
        rating: rating.number,
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

  // Mails already there, so that the ones of this run are told apart.
  const known = (await messages(context.request)).map((message) => message.ID);

  const complaints = [watch(page)];

  // ---------------------------------------------------------------- the trainee asks, from the page of the request
  const traineePage = await trainee.newPage();
  complaints.push(watch(traineePage));

  await traineePage.goto('/training/request?kind=Atc');
  await expect(traineePage.getByRole('heading', { level: 1, name: words.request.title })).toBeVisible();
  await traineePage.getByLabel(words.request.fields.position, { exact: true }).click();
  await traineePage
    .getByRole('option', {
      name: filled(words.positionChoice, { callsign: position.callsign, name: position.name }),
    })
    .click();
  await traineePage.getByLabel(words.request.fields.availabilityText, { exact: true }).fill(AVAILABILITY);
  await traineePage.getByLabel(words.request.fields.notesText, { exact: true }).fill(NOTES);

  await traineePage.getByRole('button', { name: words.request.send, exact: true }).click();
  const question = traineePage.getByRole('alertdialog');
  await expect(
    question.getByText(filled(words.request.theory.question, { rating: rating.shortName })),
  ).toBeVisible();
  await question.getByRole('radio', { name: words.request.theory.yes }).check();
  await whileWaitingFor(traineePage, 'POST', '/api/training/mine', async () => {
    await question.getByRole('button', { name: words.request.theory.confirm }).click();
  });

  await expect(traineePage).toHaveURL(/\/training\/mine$/);
  await expect(
    traineePage
      .getByRole('listitem')
      .filter({ hasText: position.callsign })
      .filter({ hasText: words.states.Requested }),
  ).toHaveCount(1);

  const asked = (await mine(trainee.request)).trainings.filter(
    (row) => row.kind === 'Atc' && row.state === 'Requested' && row.position === position.callsign,
  );
  expect(asked, 'one request, on the position chosen').toHaveLength(1);
  const trainingId = asked[0]!.id;
  id = trainingId;

  // ---------------------------------------------------------------- the staff accepts it, with the reminder of the theory in sight
  await page.goto('/staff/training?queue=toApprove');
  await expect(page.getByRole('heading', { level: 1, name: words.staff.title })).toBeVisible();
  const waiting = page
    .getByRole('row')
    .filter({ hasText: `(${String(TRAINEE)})` })
    .filter({ hasText: position.callsign });
  await expect(waiting).toHaveCount(1);
  await waiting.getByRole('link', { name: words.staff.open }).click();
  await expect(page).toHaveURL(new RegExp(`/staff/training/${String(trainingId)}$`));

  await expect(
    page.getByText(
      filled(words.staff.theoryReminder.title, {
        trainee: `Bench Pilot (${String(TRAINEE)})`,
        rating: rating.shortName,
      }),
      { exact: true },
    ),
  ).toBeVisible();
  // What the trainee wrote, as they wrote it.
  await expect(page.getByText(AVAILABILITY, { exact: true })).toBeVisible();
  await expect(page.getByText(NOTES, { exact: true })).toBeVisible();

  await page.getByRole('button', { name: words.staff.accept.button, exact: true }).click();
  await whileWaitingFor(page, 'POST', `/api/training/trainings/${String(trainingId)}/accept`, async () => {
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: words.staff.accept.button, exact: true })
      .click();
  });
  await expect(page.getByText(words.staff.accept.done, { exact: true })).toBeVisible();
  await expect(page.getByText(words.states.Accepted, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- and assigns it to the bench's trainer, who has the rating
  const trainerField = page.getByText(words.staff.assign.fields.trainerVid, { exact: true }).locator('..');
  await trainerField.getByRole('combobox').click();
  await page.getByRole('option', { name: new RegExp(`\\(${String(BENCH_TRAINER)}\\)`) }).click();
  await whileWaitingFor(page, 'POST', `/api/training/trainings/${String(trainingId)}/assign`, async () => {
    await page.getByRole('button', { name: words.staff.assign.submit, exact: true }).click();
  });
  await expect(page.getByText(words.states.Assigned, { exact: true })).toBeVisible();
  await expect(page.getByText(new RegExp(`\\(${String(BENCH_TRAINER)}\\), `))).toBeVisible();

  // ---------------------------------------------------------------- the trainer proposes two dates, from the page of the training
  // In the session they signed in with at the start: an assignment changes nothing of theirs (A7b).
  const trainerPage = await trainer.newPage();
  complaints.push(watch(trainerPage));

  await trainerPage.goto(`/staff/training/${String(trainingId)}`);
  await expect(
    trainerPage.getByRole('heading', { level: 2, name: words.staff.sections.dates }),
  ).toBeVisible();

  // The first a minute or two ahead, on a whole minute as the field writes it: the trainee chooses it before it comes, and the
  // run waits for it. The second two days on.
  const soon = new Date(Math.ceil((Date.now() + 90_000) / 60_000) * 60_000);
  const later = new Date(`${daysAhead(2)}T17:00:00Z`);
  const fields = words.staff.dates.propose.fields;
  const starts = trainerPage.getByLabel(fields['slots.startsAtUtc'], { exact: true });
  const ends = trainerPage.getByLabel(fields['slots.endsAtUtc'], { exact: true });
  await starts.first().fill(wallClock(soon));
  await ends.first().fill(wallClock(new Date(soon.getTime() + 3_600_000)));
  await trainerPage.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();
  await starts.nth(1).fill(wallClock(later));
  await ends.nth(1).fill(wallClock(new Date(later.getTime() + 2 * 3_600_000)));
  await trainerPage.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }).click();

  // The page asks to confirm only when the days meet something already — on a bench made anew nothing does —, and the trainer
  // confirms: what a date meets is the round of A8b.
  const proposed = trainerPage.getByText(filled(words.staff.dates.propose.done_other, { count: '2' }), {
    exact: true,
  });
  const confirm = trainerPage.getByRole('button', { name: words.staff.dates.propose.confirm, exact: true });
  await expect(proposed.or(confirm)).toBeVisible();
  if (await confirm.isVisible()) {
    await confirm.click();
  }
  await expect(proposed).toBeVisible();

  // ---------------------------------------------------------------- the trainee chooses the first, among the tiles of their page
  await traineePage.goto('/training/mine');
  await traineePage
    .getByRole('listitem')
    .filter({ hasText: position.callsign })
    .getByRole('link', { name: words.mine.chooseDate, exact: true })
    .click();
  await expect(traineePage).toHaveURL(new RegExp(`/training/mine/${String(trainingId)}$`));

  const choose = traineePage.getByRole('button', { name: words.detail.dates.choose, exact: true });
  await expect(choose).toHaveCount(2);
  await choose.first().click();
  await whileWaitingFor(traineePage, 'POST', `/api/training/mine/${String(trainingId)}/choose`, async () => {
    await traineePage
      .getByRole('alertdialog')
      .getByRole('button', { name: words.detail.dates.confirm, exact: true })
      .click();
  });
  await expect(traineePage.getByRole('heading', { level: 2, name: words.detail.session })).toBeVisible();
  await expect(traineePage.getByText(words.states.Scheduled, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- in the public calendar, with nobody's name
  // The calendar's weeks are made of days in UTC: the one of the session holds it.
  const visitorPage = await visitor.newPage();
  visitorPage.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await visitorPage.goto(`/calendar?view=weekList&on=${soon.toISOString().slice(0, 10)}`);
  await expect(visitorPage.locator(`a[href="/training/sessions/${String(trainingId)}"]`)).toHaveText(
    `${rating.shortName} · ${position.callsign}`,
  );
  const shown = await visitorPage.locator('body').innerText();
  for (const somebody of PEOPLE) {
    expect(shown, `the calendar of a visitor names nobody: ${somebody}`).not.toContain(somebody);
  }

  // ---------------------------------------------------------------- the session starts
  await expect
    .poll(async () => (await staffTraining(trainer.request, trainingId)).actions.canRecordOutcome, {
      message: 'the session has started',
      timeout: 240_000,
      intervals: [5_000],
    })
    .toBe(true);

  // ---------------------------------------------------------------- the trainer marks the sheet and publishes the report
  await trainerPage.reload();
  await expect(
    trainerPage.getByRole('heading', { level: 2, name: words.staff.sections.report }),
  ).toBeVisible();

  const [practice, theory] = items.map((item) => rowOf(trainerPage, item.en));
  await practice!.getByRole('radio', { name: '4', exact: true }).click();
  await practice!.getByLabel(words.staff.report.traineeComment, { exact: true }).fill(TRAINEE_COMMENT);
  await practice!.getByLabel(words.staff.report.staffNote, { exact: true }).fill(STAFF_NOTE);
  await theory!.getByRole('radio', { name: words.marks.Done, exact: true }).click();

  const report = words.staff.report.fields;
  await trainerPage.getByLabel(report.generalComment, { exact: true }).fill(GENERAL_COMMENT);
  await trainerPage.getByLabel(report.staffComment, { exact: true }).fill(STAFF_COMMENT);
  // No waiting after it, and nothing for the mock exam: the ATC ladder stays free for the rounds after this one.
  await trainerPage.getByRole('switch', { name: report.cooldownWaived, exact: true }).click();

  await trainerPage.getByRole('button', { name: words.staff.report.publish, exact: true }).click();
  await whileWaitingFor(
    trainerPage,
    'POST',
    `/api/training/trainings/${String(trainingId)}/report`,
    async () => {
      await trainerPage
        .getByRole('alertdialog')
        .getByRole('button', { name: words.staff.report.publish, exact: true })
        .click();
    },
  );
  await expect(trainerPage.getByText(words.staff.report.done, { exact: true })).toBeVisible();
  await expect(trainerPage.getByText(words.states.Completed, { exact: true })).toBeVisible();
  // The staff reads its own note on the report published.
  await expect(rowOf(trainerPage, items[0].en)).toContainText(
    filled(words.report.forStaff, { note: STAFF_NOTE }),
  );

  // ---------------------------------------------------------------- the trainee reads it, and nothing of the staff's
  await traineePage.goto('/training/mine');
  // Newest first: this run's training, where earlier runs of this round left theirs on the same position.
  await traineePage
    .getByRole('listitem')
    .filter({ hasText: position.callsign })
    .getByRole('link', { name: words.mine.readReport, exact: true })
    .first()
    .click();
  await expect(traineePage).toHaveURL(new RegExp(`/training/mine/${String(trainingId)}$`));

  await expect(traineePage.getByRole('heading', { level: 2, name: words.detail.report })).toBeVisible();
  await expect(rowOf(traineePage, items[0].en)).toContainText(filled(words.report.grade, { grade: '4' }));
  await expect(rowOf(traineePage, items[0].en)).toContainText(TRAINEE_COMMENT);
  await expect(rowOf(traineePage, items[1].en)).toContainText(words.marks.Done);
  await expect(traineePage.getByText(GENERAL_COMMENT, { exact: true })).toBeVisible();
  await expect(traineePage.getByText(words.mine.cooldownWaived, { exact: true })).toBeVisible();

  const read = await traineePage.locator('body').innerText();
  for (const reserved of [STAFF_NOTE, STAFF_COMMENT]) {
    expect(read, `the trainee never reads what the staff wrote for itself: ${reserved}`).not.toContain(
      reserved,
    );
  }

  // ---------------------------------------------------------------- the staff finds it in the history
  await page.goto('/staff/training?queue=history');
  await expect(page.getByRole('heading', { level: 1, name: words.staff.title })).toBeVisible();
  // Newest first, as every view but the queues of work.
  const over = page
    .getByRole('row')
    .filter({ hasText: `(${String(TRAINEE)})` })
    .filter({ hasText: position.callsign })
    .first();
  await expect(over).toContainText(words.staff.options.state.Completed);
  await expect(over).toContainText(`(${String(BENCH_TRAINER)})`);
  await over.getByRole('link', { name: words.staff.open }).click();
  await expect(page).toHaveURL(new RegExp(`/staff/training/${String(trainingId)}$`));

  // ---------------------------------------------------------------- and its history: every step, who took it (A13b)
  // Read by the server from the core's audit log: the request, the staff's acceptance and assignment, the dates proposed, the
  // trainee's choice, the report — each with who took it. The staff is the bench's web master, whose name this file does not write.
  const history = page
    .locator('section', {
      has: page.getByRole('heading', { level: 2, name: words.staff.sections.history, exact: true }),
    })
    .getByRole('listitem');
  const pilot = `Bench Pilot (${String(TRAINEE)})`;
  const benchTrainer = `Bench Trainer (${String(BENCH_TRAINER)})`;
  const before = (sentence: string, value: string) => sentence.split(`{{${value}}}`)[0]!;
  await expect(history).toHaveCount(6);
  await expect(history.nth(0)).toContainText(filled(words.staff.history.requested, { name: pilot }));
  await expect(history.nth(1)).toContainText(filled(words.staff.history.accepted, { name: '' }).trim());
  await expect(history.nth(2)).toContainText(
    filled(words.staff.history.assigned, { name: '', trainer: benchTrainer }).trim(),
  );
  await expect(history.nth(3)).toContainText(
    filled(words.staff.history.datesChanged, { name: benchTrainer }),
  );
  await expect(history.nth(4)).toContainText(
    filled(before(words.staff.history.dateChosen, 'date'), { name: pilot }),
  );
  await expect(history.nth(5)).toContainText(filled(words.staff.history.completed, { name: benchTrainer }));

  // ---------------------------------------------------------------- the mail of every step, once, to whoever it is for
  const session = `${training}, ${wallClock(soon).replace('T', ' ')} UTC`;
  const expected: readonly (readonly [MailType, string, string])[] = [
    ['requestReceived', TRAINEE_ADDRESS, training],
    ['requestAccepted', TRAINEE_ADDRESS, training],
    ['trainerAssigned', TRAINEE_ADDRESS, training],
    ['trainerAssigned', TRAINER_ADDRESS, training],
    ['datesProposed', TRAINEE_ADDRESS, training],
    ['dateConfirmed', TRAINEE_ADDRESS, session],
    ['dateConfirmed', TRAINER_ADDRESS, session],
    ['reportPublished', TRAINEE_ADDRESS, training],
  ];

  await expect
    .poll(
      async () => {
        const arrived = (await messages(context.request)).filter((message) => !known.includes(message.ID));
        return expected
          .filter(
            ([type, address, about]) =>
              arrived.filter(
                (message) =>
                  message.To.some((recipient) => recipient.Address === address) &&
                  subjectsOf(type).some((subject) => message.Subject.startsWith(subject)) &&
                  message.Subject.includes(about),
              ).length !== 1,
          )
          .map(([type, address]) => `${type} to ${address}`);
      },
      { message: 'the mail of every step, once each', timeout: 150_000, intervals: [5_000] },
    )
    .toEqual([]);

  expect(complaints.flat()).toEqual([]);
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

/** A day some days ahead, in UTC, as `YYYY-MM-DD`: far enough that no date of the run goes by while it runs. */
function daysAhead(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
}

/** An instant as a field of a date writes it: the day and the minute, in UTC as the field's label says. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
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

/** A training as the staff's page reads it. */
async function staffTraining(request: APIRequestContext, id: number): Promise<StaffTraining> {
  const response = await request.get(`/api/training/trainings/${String(id)}`);
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as StaffTraining;
}

/** A step of the staff on a training, through the API, at the version there is now. */
async function step(
  staff: APIRequestContext,
  id: number,
  verb: string,
  body: Record<string, unknown>,
): Promise<void> {
  const { rowVersion } = await staffTraining(staff, id);
  const answer = await staff.post(`/api/training/trainings/${String(id)}/${verb}`, {
    headers: asTheClientDoes,
    data: { ...body, rowVersion },
  });
  expect(answer.status(), `${verb}: ${await answer.text()}`).toBe(200);
}

/**
 * A training that is still going on, taken back so that it makes nobody wait: cancelled by the trainee while nobody accepted it,
 * closed by the staff with a reason once accepted. One whose session has started is not closed over (the maintainer's answer on
 * #149): it is rescheduled first — which, unlike a no-show, makes the trainee wait for nothing — and then closed.
 */
async function takeBack(staff: APIRequestContext, trainee: APIRequestContext, id: number): Promise<void> {
  const training = await staffTraining(staff, id);

  if (training.state === 'Requested') {
    const cancelled = await trainee.post(`/api/training/mine/${String(id)}/cancel`, {
      headers: asTheClientDoes,
      data: { rowVersion: training.rowVersion },
    });
    expect(cancelled.status(), await cancelled.text()).toBe(200);
  } else if (GOING.includes(training.state)) {
    if (training.state === 'Scheduled' && training.actions.canRecordOutcome) {
      await step(staff, id, 'reschedule', { notes: null });
    }
    await step(staff, id, 'close', { reason: REASON });
  }
}

/** The trainee's ATC trainings a run stopped half way left going — this round's or another's —, taken back. */
async function takeBackLeftovers(staff: APIRequestContext, trainee: APIRequestContext): Promise<void> {
  for (const row of (await mine(trainee)).trainings.filter(
    (candidate) => candidate.kind === 'Atc' && GOING.includes(candidate.state),
  )) {
    await takeBack(staff, trainee, row.id);
  }
}

/**
 * The items of this file on the sheet, recognised by the marker: deleted when no report used them, switched off when one did — a
 * report keeps its copy, and an item it marked is never deleted (A9a).
 */
async function retireItems(staff: APIRequestContext): Promise<void> {
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

/** The messages Mailpit holds, newest first. */
async function messages(request: APIRequestContext): Promise<Message[]> {
  const list = await request.get(`${mailpit}/api/v1/messages?limit=200`);
  expect(list.status(), `Mailpit at ${mailpit}`).toBe(200);
  return ((await list.json()) as { messages: Message[] }).messages;
}

/**
 * How the subject of a mail of that type begins, in either language of the bench: a member reads their mails in their own, which the
 * bench does not fix.
 */
function subjectsOf(type: MailType): string[] {
  return [words.mail.training[type].subject, italian[type]].map((subject) => subject.split('{{')[0]!);
}

/** A sentence of the language file with its values in. */
function filled(sentence: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);
}

/** The subjects of the mails in Italian, read from the copy `pnpm i18n:sync` keeps at the root. */
function italianSubjects(): Record<MailType, string> {
  const mail = (
    JSON.parse(
      readFileSync(fileURLToPath(new URL('../../../locales/it/training.json', import.meta.url)), 'utf8'),
    ) as { mail: { training: Record<MailType, { subject: string }> } }
  ).mail.training;

  return {
    requestReceived: mail.requestReceived.subject,
    requestAccepted: mail.requestAccepted.subject,
    trainerAssigned: mail.trainerAssigned.subject,
    datesProposed: mail.datesProposed.subject,
    dateConfirmed: mail.dateConfirmed.subject,
    reportPublished: mail.reportPublished.subject,
  };
}

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    kinds: { Atc: string };
    positionChoice: string;
    states: { Requested: string; Accepted: string; Assigned: string; Scheduled: string; Completed: string };
    marks: { Done: string };
    report: { grade: string; forStaff: string };
    request: {
      title: string;
      send: string;
      fields: { position: string; availabilityText: string; notesText: string };
      theory: { question: string; yes: string; confirm: string };
    };
    mine: { chooseDate: string; readReport: string; cooldownWaived: string };
    detail: { session: string; report: string; dates: { choose: string; confirm: string } };
    staff: {
      title: string;
      open: string;
      options: { state: { Completed: string } };
      sections: { dates: string; report: string; history: string };
      history: {
        requested: string;
        accepted: string;
        assigned: string;
        datesChanged: string;
        dateChosen: string;
        completed: string;
      };
      theoryReminder: { title: string };
      accept: { button: string; done: string };
      assign: { submit: string; fields: { trainerVid: string } };
      dates: {
        propose: {
          submit: string;
          confirm: string;
          done_other: string;
          fields: { 'slots.startsAtUtc': string; 'slots.endsAtUtc': string };
        };
      };
      report: {
        traineeComment: string;
        staffNote: string;
        publish: string;
        done: string;
        fields: { generalComment: string; staffComment: string; cooldownWaived: string };
      };
    };
    mail: { training: Record<MailType, { subject: string }> };
  };
}
