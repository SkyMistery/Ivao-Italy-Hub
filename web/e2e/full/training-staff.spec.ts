import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import {
  expect,
  test,
  type APIRequestContext,
  type APIResponse,
  type BrowserContext,
} from '@playwright/test';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor } from './bench';

/**
 * The "done when" of A7 and A7b (M3), through the real screens against the real server: the bench's trainee asks for an ATC
 * training, the staff accepts it on its page — with the reminder of the theory exam in sight — and assigns it to the bench's
 * trainer, the only one of the staff of the training with the rating; the trainer, signed in before the assignment and never
 * again, conducts that training from their next request — `Training.Conduct` is theirs by position, and reaches the trainings
 * assigned to them (A7b): nothing of theirs changes, no grant is written —, and finds the mail that says so. The ratings and the
 * positions are the server's: the spec writes none.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`, AS3), the staff is the bench's web master (every permission,
 * and no rating: not a trainer), the trainer is `?as=trainer` (a trainer's position, SEC). The trainer signs in at the start: the
 * staff of the training is whoever signed in once, and so the run needs no other file before it.
 *
 * It takes back what it opens, so that a second run on a bench that survived the first finds the ATC path free, here and in
 * A6b's round (review of #146, point 2): at its end this run's training is closed by the staff with a reason — the staff's
 * closure of A8 —, or cancelled by the trainee while nobody accepted it; at its start, whatever an earlier run left going on the
 * ladder is taken back the same way, since a run stopped half way reaches no end. It runs after the request of A6b
 * (`training-request.spec.ts`, which wants no training open on either ladder at its start: Playwright runs the files in the order
 * of their names, one worker).
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

interface MyTraining {
  readonly paths: readonly {
    readonly kind: 'Atc' | 'Pilot';
    readonly next: { readonly number: number; readonly shortName: string } | null;
    readonly positions: readonly { readonly callsign: string }[];
    readonly refusal: string | null;
  }[];
  readonly trainings: readonly {
    readonly id: number;
    readonly kind: string;
    readonly state: string;
    readonly rowVersion: string;
  }[];
}

const BENCH_TRAINER = 999004;
const TRAINER_ADDRESS = 'bench-trainer@bench.test';

/** The states the staff closes a training from (§2.1, A8): accepted and still going on. */
const CLOSABLE = ['Accepted', 'Assigned', 'Scheduled'];

/** Why the staff closes what a run leaves going: the trainee reads it on their page and in the mail. */
const REASON = 'trn-test: closed by the round of the staff.';

test('a request is accepted and assigned, and the trainer conducts it in the session they had, with nothing of theirs changed', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  const trainer = await browser.newContext({ baseURL: benchUrl });
  let requested: number | null = null;

  try {
    // ---------------------------------------------------------------- the trainee asks, as A6 has them ask
    await signIn(trainee, 'pilot');
    await signIn(context, null);
    // In the roster before anybody assigns them a training, and never signed in again after.
    await signIn(trainer, 'trainer');
    // What an earlier run left going is taken back first: a request still waiting, by the trainee; an ATC training accepted,
    // assigned or dated, by the staff.
    await cancelWaiting(trainee.request);
    await closeGoing(context.request, trainee.request, (row) => row.kind === 'Atc');
    const before = await mine(trainee.request);
    const atc = before.paths.find((path) => path.kind === 'Atc')!;
    expect(
      atc.refusal,
      'the trainee may ask for an ATC training: what an earlier run left going on the ladder was taken back above',
    ).toBeNull();
    const position = atc.positions[0]!.callsign;

    const asked = await trainee.request.post('/api/training/mine', {
      headers: asTheClientDoes,
      data: {
        kind: 'Atc',
        rating: atc.next!.number,
        position,
        availabilityText: 'Weekday evenings, from 18 UTC.',
        notesText: null,
        theoryPassed: true,
      },
    });
    expect(asked.status(), await asked.text()).toBe(201);
    requested = ((await asked.json()) as { id: number }).id;
    const training = `${words.kinds.Atc} · ${atc.next!.shortName} · ${position}`;

    // ---------------------------------------------------------------- the staff finds it among the requests to approve
    const complaints: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error' && !message.text().includes('favicon')) {
        complaints.push(message.text());
      }
    });
    page.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });

    await page.goto('/staff/training?queue=toApprove');
    await expect(page.getByRole('heading', { level: 1, name: words.staff.title })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: '(999002)' }).filter({ hasText: position });
    await expect(row).toHaveCount(1);
    await row.getByRole('link', { name: words.staff.open }).click();
    await expect(page).toHaveURL(new RegExp(`/staff/training/${String(requested)}$`));

    // The reminder of whoever approves: the trainee's word on the theory is checked (§2.3).
    await expect(
      page.getByText(
        filled(words.staff.theoryReminder.title, {
          trainee: 'Bench Pilot (999002)',
          rating: atc.next!.shortName,
        }),
        { exact: true },
      ),
    ).toBeVisible();

    // ---------------------------------------------------------------- accepted
    await page.getByRole('button', { name: words.staff.accept.button, exact: true }).click();
    await whileWaitingFor(page, 'POST', `/api/training/trainings/${String(requested)}/accept`, async () => {
      await page
        .getByRole('alertdialog')
        .getByRole('button', { name: words.staff.accept.button, exact: true })
        .click();
    });
    await expect(page.getByText(words.staff.accept.done, { exact: true })).toBeVisible();
    await expect(page.getByText(words.states.Accepted, { exact: true })).toBeVisible();

    // ---------------------------------------------------------------- the trainer, before: the training is not theirs
    // Training.Conduct by their position, on the department and on no training of its own (A7b); on a training not assigned to
    // them it is worth Training.Edit, which they do not hold.
    const conductBefore = await conductHeld(trainer.request);
    expect(conductBefore).toContainEqual({ name: 'Training.Conduct', department: 'TD', resourceScope: null });
    expect((await conflicts(trainer.request, requested)).status(), 'not theirs before the assignment').toBe(
      403,
    );

    // ---------------------------------------------------------------- assigned to the bench's trainer, the one with the rating
    const known = await mailsTo(trainee.request, TRAINER_ADDRESS);
    const field = page.getByText(words.staff.assign.fields.trainerVid, { exact: true }).locator('..');
    await field.getByRole('combobox').click();
    await page.getByRole('option', { name: new RegExp(`\\(${String(BENCH_TRAINER)}\\)`) }).click();
    await whileWaitingFor(page, 'POST', `/api/training/trainings/${String(requested)}/assign`, async () => {
      await page.getByRole('button', { name: words.staff.assign.submit, exact: true }).click();
    });
    await expect(page.getByText(words.states.Assigned, { exact: true })).toBeVisible();
    await expect(page.getByText(/\(999004\), /)).toBeVisible();

    // The bench has one trainer with the rating, and now they are the one: there is nobody else to change them for.
    await expect(
      page.getByText(filled(words.staff.trainer.noOtherCandidates, { rating: atc.next!.shortName }), {
        exact: true,
      }),
    ).toBeVisible();

    // ---------------------------------------------------------------- the trainer, in the same session: theirs now
    // Nothing of theirs changed — no grant was written, so nothing signs them out —, and the training they could not touch a
    // moment ago is theirs to conduct.
    expect(await conductHeld(trainer.request)).toEqual(conductBefore);
    expect((await conflicts(trainer.request, requested)).status(), 'theirs after the assignment').toBe(200);
    const theirs = await trainer.request.get(`/api/training/trainings/${String(requested)}`);
    expect(theirs.status(), await theirs.text()).toBe(200);
    expect(((await theirs.json()) as { actions: { canConduct: boolean } }).actions.canConduct).toBe(true);

    // And the mail that told them, with the training in its subject: one of this run's.
    await expect
      .poll(
        async () =>
          (await mailsTo(trainer.request, TRAINER_ADDRESS)).filter(
            (mail) => !known.some((seen) => seen.ID === mail.ID) && mail.Subject.includes(training),
          ).length,
        {
          timeout: 150_000,
          intervals: [5_000],
        },
      )
      .toBe(1);

    expect(complaints).toEqual([]);
  } finally {
    // A request this run left waiting — it stopped before the acceptance — is taken back, as A6b's round takes back its own.
    if (requested !== null) {
      const left = (await mine(trainee.request)).trainings.find(
        (row) => row.id === requested && row.state === 'Requested',
      );
      if (left !== undefined) {
        const cancelled = await trainee.request.post(`/api/training/mine/${String(left.id)}/cancel`, {
          headers: asTheClientDoes,
          data: { rowVersion: left.rowVersion },
        });
        expect(cancelled.status(), await cancelled.text()).toBe(200);
      }

      // Accepted, assigned or dated, it is closed by the staff with a reason (A8): the next run finds the ATC path free.
      await closeGoing(context.request, trainee.request, (row) => row.id === requested);
    }

    await trainee.close();
    await trainer.close();
  }
});

async function signIn(context: BrowserContext, as: 'pilot' | 'trainer' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

async function mine(request: APIRequestContext): Promise<MyTraining> {
  const response = await request.get('/api/training/mine');
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as MyTraining;
}

/** The trainee's requests still waiting, cancelled at the version they have: the one open state a trainee closes. */
async function cancelWaiting(request: APIRequestContext): Promise<void> {
  for (const training of (await mine(request)).trainings.filter((row) => row.state === 'Requested')) {
    const cancelled = await request.post(`/api/training/mine/${String(training.id)}/cancel`, {
      headers: asTheClientDoes,
      data: { rowVersion: training.rowVersion },
    });
    expect(cancelled.status(), await cancelled.text()).toBe(200);
  }
}

/**
 * The trainee's trainings past the request and still going, those `which` picks, closed by the staff with a reason at the
 * version they have (A8): once accepted, the trainee takes them back no more.
 */
async function closeGoing(
  staff: APIRequestContext,
  trainee: APIRequestContext,
  which: (row: MyTraining['trainings'][number]) => boolean,
): Promise<void> {
  for (const training of (await mine(trainee)).trainings.filter(
    (row) => CLOSABLE.includes(row.state) && which(row),
  )) {
    const closed = await staff.post(`/api/training/trainings/${String(training.id)}/close`, {
      headers: asTheClientDoes,
      data: { reason: REASON, rowVersion: training.rowVersion },
    });
    expect(closed.status(), await closed.text()).toBe(200);
  }
}

/** Where `/api/me` says the member holds `Training.Conduct`: the department, and the one training if any. */
async function conductHeld(
  request: APIRequestContext,
): Promise<{ name: string; department: string | null; resourceScope: string | null }[]> {
  const me = await request.get('/api/me');
  expect(me.status()).toBe(200);
  return (
    (await me.json()) as {
      permissions: { name: string; department: string | null; resourceScope: string | null }[];
    }
  ).permissions.filter((permission) => permission.name === 'Training.Conduct');
}

/**
 * What a date a week from now meets on a training, as its page asks before a date is written: answered only to whoever may
 * conduct it, whatever state it is in.
 */
async function conflicts(request: APIRequestContext, id: number): Promise<APIResponse> {
  const startsAtUtc = new Date(Date.now() + 7 * 24 * 3_600_000).toISOString();
  return request.get(
    `/api/training/trainings/${String(id)}/conflicts?startsAtUtc=${encodeURIComponent(startsAtUtc)}`,
  );
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
    kinds: { Atc: string };
    states: { Accepted: string; Assigned: string };
    staff: {
      title: string;
      open: string;
      theoryReminder: { title: string };
      trainer: { noOtherCandidates: string };
      accept: { button: string; done: string };
      assign: { submit: string; fields: { trainerVid: string } };
    };
  };
}
