import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type BrowserContext } from '@playwright/test';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor } from './bench';

/**
 * The "done when" of A7 (M3), through the real screens against the real server: the bench's trainee asks for an ATC training,
 * the staff accepts it on its page — with the reminder of the theory exam in sight — and assigns it to the bench's trainer, the
 * only one of the staff of the training with the rating; the trainer, signed in again, holds `Training.Conduct` on that
 * training alone, and finds the mail that says so. The ratings and the positions are the server's: the spec writes none.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`, AS3), the staff is the bench's web master (every permission,
 * and no rating: not a trainer), the trainer is `?as=trainer` (a trainer's position, SEC).
 *
 * ⚠️ It leaves the training assigned: a trainee cancels only a request nobody accepted, and on A7's code nothing closes an
 * accepted training — the staff's closure is A8's. So it runs after the request of A6b (`training-request.spec.ts`, which wants
 * no training open on either ladder at its start: Playwright runs the files in the order of their names, one worker), and on
 * A7's code alone a second run on a bench that survived the first finds the ATC path taken, here and in A6b's round, and says
 * so rather than working around it. What it can take back, it does: a request an earlier run left waiting, at the start, and
 * this run's, if it stopped before the acceptance. A grant of the trainer on a training that is over is the night's to take
 * back (`training-expiry`), and a bench that survives between runs may still show one: it is let be.
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

/** The states of a training still going (design M3 §2.1): a trainer's grant on one of these is a training they conduct. */
const GOING = ['Requested', 'Accepted', 'Assigned', 'Scheduled'];

test('a request is accepted and assigned, and the trainer, signed in again, conducts that training alone', async ({
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
    // A request an earlier run left waiting — it stopped before the acceptance — is taken back first.
    await cancelWaiting(trainee.request);
    const before = await mine(trainee.request);
    const atc = before.paths.find((path) => path.kind === 'Atc')!;
    expect(
      atc.refusal,
      "the trainee may ask for an ATC training: on A7's code nothing closes one an earlier run accepted (the staff's closure is A8's), so the bench is recreated before a second run",
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
    await signIn(context, null);
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

    // ---------------------------------------------------------------- the trainer, signed in again: that training, and no other
    await signIn(trainer, 'trainer');
    const me = await trainer.request.get('/api/me');
    expect(me.status()).toBe(200);
    const conduct = (
      (await me.json()) as {
        permissions: { name: string; department: string | null; resourceScope: string | null }[];
      }
    ).permissions.filter((permission) => permission.name === 'Training.Conduct');
    const ours = `training:training:${String(requested)}`;
    expect(conduct).toContainEqual({ name: 'Training.Conduct', department: 'TD', resourceScope: ours });

    // No other training still going, and never the whole department. A grant on a training that is over waits for the night
    // to take it back, and a bench that survives between runs may still hold one of an earlier run.
    for (const other of conduct.filter((permission) => permission.resourceScope !== ours)) {
      const id = /^training:training:(\d+)$/.exec(other.resourceScope ?? '')?.[1];
      expect(id, `Training.Conduct beyond one training: ${JSON.stringify(other)}`).toBeDefined();
      expect(GOING).not.toContain(await stateOf(context.request, Number(id)));
    }

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

/** The state of a training, as the staff's page of it says. */
async function stateOf(request: APIRequestContext, id: number): Promise<string> {
  const response = await request.get(`/api/training/trainings/${String(id)}`);
  expect(response.status(), await response.text()).toBe(200);
  return ((await response.json()) as { state: string }).state;
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
