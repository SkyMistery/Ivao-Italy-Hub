import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor } from './bench';

/**
 * The "done when" of A8 (M3), through the real screens against the real server: the trainer proposes two dates for the bench's
 * trainee, the first on the day of another training's session and warned of it, and confirms them; the trainee chooses that
 * one among the tiles of the page of their training; its session is in the public calendar with nobody's name; and the mails
 * of the date fixed reach both. The staff then closes the training with a reason: its session leaves the calendar, and the
 * trainee reads why.
 *
 * ⚠️ The reminder is not waited for here: its job runs every quarter of an hour, and the bench has no way of starting a job on
 * demand — an endpoint of the bench is the core's (A8a, «Trovato» 4). `TrainingDatesTests` proves it through the job, once.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`), the trainer `?as=trainer`, the staff the bench's web master
 * (every permission, not a trainer). ⚠️ It takes the ATC training `training-staff.spec.ts` leaves assigned to the bench's trainer
 * — this file's name sorts after that one's, and Playwright runs the files in the order of their names, one worker —, or asks
 * for one of its own when there is none, as when it runs alone. The other session of that day is a pilot training of the same
 * trainee, asked for, accepted, assigned and dated here through the API. Both are closed at the end.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const BENCH_TRAINER = 999004;
const TRAINEE_ADDRESS = 'bench-pilot@bench.test';
const TRAINER_ADDRESS = 'bench-trainer@bench.test';
const REASON = 'trn-test: closed by the round of the dates.';

interface MyTraining {
  readonly asksTheory: boolean;
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly asksPosition: boolean;
    readonly positions: readonly { readonly callsign: string }[];
    readonly refusal: string | null;
  }[];
  readonly trainings: readonly {
    readonly id: number;
    readonly kind: string;
    readonly state: string;
    readonly position: string | null;
    readonly ratingShortName: string | null;
    readonly trainer: { readonly vid: number } | null;
    readonly rowVersion: string;
  }[];
}

interface StaffTraining {
  readonly state: string;
  readonly rowVersion: string;
}

test('the trainer proposes two dates, the trainee chooses the one warned of another training, and the calendar shows it without a name', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(300_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  const trainer = await browser.newContext({ baseURL: benchUrl });
  const visitor = await browser.newContext({ baseURL: benchUrl });
  for (const reader of [trainee, trainer, visitor]) {
    await readInEnglish(reader);
  }

  // What this run opens, so that the end closes it whatever happens in between.
  const opened: number[] = [];

  try {
    await signIn(context, null);
    // In the roster before anybody assigns them a training: the staff of the training is whoever signed in once.
    await signIn(trainer, 'trainer');
    await signIn(trainee, 'pilot');

    const settings = await context.request.get('/api/modules/training/settings');
    expect(settings.status()).toBe(200);
    expect(
      ((await settings.json()) as { conflictPolicy: string }).conflictPolicy,
      'the division warns of what a date meets, as it does by default',
    ).toBe('Warn');

    // ---------------------------------------------------------------- the ATC training, assigned to the bench's trainer
    const atc = await assignedAtc(trainee.request, context.request, opened);

    // ---------------------------------------------------------------- another session on the day of the first date
    const day = daysAhead(4);
    const nextDay = daysAhead(5);
    const pilot = await acceptedAndAssigned(trainee.request, context.request, 'Pilot', opened);
    await step(context.request, pilot, 'date', { startsAtUtc: `${day}T10:00:00Z`, confirmed: true });

    // Mails already there, so that the ones of this run are told apart.
    const known = [
      ...(await mailsTo(context.request, TRAINEE_ADDRESS)),
      ...(await mailsTo(context.request, TRAINER_ADDRESS)),
    ].map((mail) => mail.ID);

    // ---------------------------------------------------------------- the trainer proposes two dates, and confirms the warning
    // In the session they signed in with at the start: an assignment changes nothing of theirs (A7b).
    const trainerPage = await trainer.newPage();
    const complaints: string[] = [];
    for (const watched of [page, trainerPage]) {
      watched.on('console', (message) => {
        if (message.type() === 'error' && !message.text().includes('favicon')) {
          complaints.push(message.text());
        }
      });
      watched.on('pageerror', (error) => {
        throw new Error(`The page threw: ${error.message}`);
      });
    }

    await trainerPage.goto(`/staff/training/${String(atc.id)}`);
    await expect(
      trainerPage.getByRole('heading', { level: 2, name: words.staff.sections.dates }),
    ).toBeVisible();

    const fields = words.staff.dates.propose.fields;
    const starts = trainerPage.getByLabel(fields['slots.startsAtUtc'], { exact: true });
    const ends = trainerPage.getByLabel(fields['slots.endsAtUtc'], { exact: true });
    await starts.first().fill(`${day}T16:00`);
    await ends.first().fill(`${day}T18:00`);
    await trainerPage.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();
    await starts.nth(1).fill(`${nextDay}T16:00`);
    await ends.nth(1).fill(`${nextDay}T18:00`);
    await trainerPage.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }).click();

    // Warned of the other session that day, by what the public calendar shows of it and with its page; nothing written yet.
    await expect(trainerPage.getByText(words.staff.dates.met.confirmTitle)).toBeVisible();
    await expect(trainerPage.locator(`a[href="/staff/training/${String(pilot)}"]`).first()).toBeVisible();

    await whileWaitingFor(
      trainerPage,
      'POST',
      `/api/training/trainings/${String(atc.id)}/slots`,
      async () => {
        await trainerPage
          .getByRole('button', { name: words.staff.dates.propose.confirm, exact: true })
          .click();
      },
    );
    await expect(
      trainerPage.getByText(filled(words.staff.dates.propose.done_other, { count: '2' }), { exact: true }),
    ).toBeVisible();

    // ---------------------------------------------------------------- the trainee chooses the first, the one warned
    const traineePage = await trainee.newPage();
    traineePage.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });

    await traineePage.goto('/training/mine');
    await traineePage.getByRole('link', { name: words.mine.chooseDate, exact: true }).click();
    await expect(traineePage).toHaveURL(new RegExp(`/training/mine/${String(atc.id)}$`));

    const choose = traineePage.getByRole('button', { name: words.detail.dates.choose, exact: true });
    await expect(choose).toHaveCount(2);
    await choose.first().click();
    await whileWaitingFor(traineePage, 'POST', `/api/training/mine/${String(atc.id)}/choose`, async () => {
      await traineePage
        .getByRole('alertdialog')
        .getByRole('button', { name: words.detail.dates.confirm, exact: true })
        .click();
    });
    await expect(traineePage.getByRole('heading', { level: 2, name: words.detail.session })).toBeVisible();
    await expect(traineePage.getByText(words.states.Scheduled, { exact: true })).toBeVisible();

    // ---------------------------------------------------------------- in the public calendar, with nobody's name
    const visitorPage = await visitor.newPage();
    await visitorPage.goto(`/calendar?view=weekList&on=${day}`);
    const entry = visitorPage.locator(`a[href="/training/sessions/${String(atc.id)}"]`);
    await expect(entry).toHaveText(`${atc.ratingShortName} · ${atc.position}`);

    const shown = await visitorPage.locator('body').innerText();
    for (const somebody of ['Bench Pilot', 'Bench Trainer', '999002', '999004']) {
      expect(shown, `the calendar of a visitor names nobody: ${somebody}`).not.toContain(somebody);
    }

    // ---------------------------------------------------------------- the mails of the date fixed, to both
    const session = `${day} 16:00`;
    for (const address of [TRAINEE_ADDRESS, TRAINER_ADDRESS]) {
      await expect
        .poll(
          async () =>
            (await mailsTo(context.request, address)).filter(
              (mail) => !known.includes(mail.ID) && mail.Subject.includes(session),
            ).length,
          { message: `the mail of the date fixed to ${address}`, timeout: 150_000, intervals: [5_000] },
        )
        .toBe(1);
    }

    // ---------------------------------------------------------------- closed by the staff, with a reason
    await page.goto(`/staff/training/${String(atc.id)}`);
    await page.getByRole('button', { name: words.staff.close.button, exact: true }).click();
    const dialog = page.getByRole('alertdialog');
    await dialog.getByLabel(words.staff.close.fields.reason, { exact: true }).fill(REASON);
    await whileWaitingFor(page, 'POST', `/api/training/trainings/${String(atc.id)}/close`, async () => {
      await dialog.getByRole('button', { name: words.staff.close.button, exact: true }).click();
    });
    await expect(page.getByText(words.staff.close.done, { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.closing })).toBeVisible();

    // Its session leaves the calendar, and the trainee reads why.
    await visitorPage.reload();
    await expect(visitorPage.locator(`a[href="/training/sessions/${String(pilot)}"]`)).toHaveCount(1);
    await expect(entry).toHaveCount(0);
    await traineePage.reload();
    await expect(traineePage.getByText(filled(words.mine.closedByStaff, { reason: REASON }))).toBeVisible();

    expect(complaints).toEqual([]);
  } finally {
    // Whatever this run left open is closed by the staff, or taken back by the trainee while nobody accepted it.
    for (const id of opened) {
      await closeIfOpen(context.request, trainee.request, id);
    }

    await trainee.close();
    await trainer.close();
    await visitor.close();
  }
});

async function signIn(context: BrowserContext, as: 'pilot' | 'trainer' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

/** A day some days ahead, in UTC, as `YYYY-MM-DD`: far enough that no date of the run goes by while it runs. */
function daysAhead(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);
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

/** A training of the trainee's asked for, accepted and assigned to the bench's trainer, through the API: A6's and A7's steps. */
async function acceptedAndAssigned(
  trainee: APIRequestContext,
  staff: APIRequestContext,
  kind: 'Atc' | 'Pilot',
  opened: number[],
): Promise<number> {
  const standing = await mine(trainee);
  const path = standing.paths.find((candidate) => candidate.kind === kind)!;
  expect(path.refusal, `the trainee may ask for a ${kind} training`).toBeNull();

  const asked = await trainee.post('/api/training/mine', {
    headers: asTheClientDoes,
    data: {
      kind,
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
 * The ATC training assigned to the bench's trainer: the one `training-staff.spec.ts` leaves, or one of this run's when there is
 * none. Its rating and position are the server's, as the calendar names its session.
 */
async function assignedAtc(
  trainee: APIRequestContext,
  staff: APIRequestContext,
  opened: number[],
): Promise<{ id: number; ratingShortName: string; position: string }> {
  let found = (await mine(trainee)).trainings.find(
    (training) => training.kind === 'Atc' && training.state === 'Assigned',
  );

  if (found === undefined) {
    const id = await acceptedAndAssigned(trainee, staff, 'Atc', opened);
    found = (await mine(trainee)).trainings.find((training) => training.id === id);
  } else {
    opened.push(found.id);
  }

  expect(found?.trainer?.vid, 'the ATC training is the bench trainer’s to conduct').toBe(BENCH_TRAINER);
  return { id: found!.id, ratingShortName: found!.ratingShortName!, position: found!.position! };
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

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    states: { Scheduled: string };
    mine: { chooseDate: string; closedByStaff: string };
    detail: { session: string; dates: { choose: string; confirm: string } };
    staff: {
      sections: { dates: string; closing: string };
      dates: {
        met: { confirmTitle: string };
        propose: {
          submit: string;
          confirm: string;
          done_other: string;
          fields: { 'slots.startsAtUtc': string; 'slots.endsAtUtc': string };
        };
      };
      close: { button: string; done: string; fields: { reason: string } };
    };
  };
}
