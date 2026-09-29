import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';

import { benchUrl, mailpit, readInEnglish, whileWaitingFor } from './bench';

/**
 * The "done when" of A10a (M3), through the real screens against the real server: the staff asks for the bench's trainee by VID,
 * reads their path — where they stand on each ladder, their trainings, their bans — and bans them from it, with a reason and no end;
 * the ban is in force on the path, the trainee's next request on either ladder is refused for the ban, and the mail of the ban reaches
 * them; the staff lifts it from the path, and the trainee may ask again.
 *
 * Who is who: the trainee is the pilot of the tours (`?as=pilot`, VID 999002), the staff the bench's web master (every permission, and
 * not the trainee). The rounds before this one leave the trainee with trainings on both ladders (A8b, A9b): the path shows them, and
 * this round asks for none. ⚠️ A ban blocks every request: this file's name sorts after every other round of the training — Playwright
 * runs the files in the order of their names, one worker —, it lifts at the start a ban an earlier run left in force, and at the end
 * any ban of its own still in force; the bench is made anew before every run.
 */

const words = englishTraining();
const asTheClientDoes = { 'X-Requested-With': 'hub' };

const TRAINEE = 999002;
const TRAINEE_ADDRESS = 'bench-pilot@bench.test';

/** In the reason of the ban of this run: what finds its mail again. */
const REASON = `trn-test ${Date.now().toString(36)}: repeated no-shows.`;

interface Ban {
  readonly id: number;
  readonly holds: boolean;
  readonly rowVersion: string;
}

interface Ladder {
  readonly kind: 'Atc' | 'Pilot';
  readonly refusal: string | null;
}

test('the staff bans the trainee from their path, whose next request is refused for the ban until it is lifted', async ({
  context,
  browser,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);

  const trainee = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(trainee);

  try {
    await signIn(context, null);
    await signIn(trainee, 'pilot');
    await liftLeftovers(context.request);

    // Nothing bans the trainee before this round: the bench is made anew, and a ban left by a run stopped half way is lifted above.
    for (const ladder of await ladders(trainee.request)) {
      expect(ladder.refusal, `the ${ladder.kind} ladder is not banned yet`).not.toBe(words.banned);
    }

    const known = (await mailsTo(context.request, TRAINEE_ADDRESS)).map((mail) => mail.ID);

    // ---------------------------------------------------------------- the path, asked by VID
    const staffPage = await context.newPage();
    const complaints = watch(staffPage);

    await staffPage.goto('/staff/training/trainees');
    await staffPage.getByLabel(words.trainees.fields.vid, { exact: true }).fill(String(TRAINEE));
    await staffPage.getByRole('button', { name: words.trainees.open, exact: true }).click();
    await expect(staffPage).toHaveURL(new RegExp(`/staff/training/trainees/${String(TRAINEE)}$`));
    await expect(
      staffPage.getByRole('heading', { level: 2, name: words.trainees.sections.ladders }),
    ).toBeVisible();
    await expect(
      staffPage.getByRole('heading', { level: 2, name: words.trainees.sections.trainings }),
    ).toBeVisible();

    // ---------------------------------------------------------------- banned from the path, with a reason and no end
    await staffPage.getByRole('link', { name: words.trainees.ban, exact: true }).click();
    await expect(staffPage).toHaveURL(/\/staff\/training\/bans\/new\?vid=999002$/);
    await expect(staffPage.getByLabel(words.bans.fields.vid, { exact: true })).toHaveValue(String(TRAINEE));
    await staffPage.getByLabel(words.bans.fields.reason, { exact: true }).fill(REASON);
    await whileWaitingFor(staffPage, 'POST', '/api/training/bans', async () => {
      await staffPage.getByRole('button', { name: words.bans.give, exact: true }).click();
    });
    await expect(staffPage.getByText(words.bans.given, { exact: true })).toBeVisible();

    // Back on the path: the ban in force, why, and both ladders banned until somebody lifts it.
    await expect(staffPage).toHaveURL(new RegExp(`/staff/training/trainees/${String(TRAINEE)}$`));
    const ban = banOf(staffPage);
    await expect(ban.getByText(words.bans.options.status.Holds, { exact: true })).toBeVisible();
    await expect(staffPage.getByText(words.trainees.standing.bannedForever, { exact: true })).toHaveCount(2);

    // ---------------------------------------------------------------- the trainee asks for nothing, and is told
    for (const ladder of await ladders(trainee.request)) {
      expect(ladder.refusal, `the ${ladder.kind} ladder is banned`).toBe(words.banned);
    }

    const traineePage = await trainee.newPage();
    const theirComplaints = watch(traineePage);
    await traineePage.goto('/training/request?kind=Atc');
    await expect(traineePage.getByText(words.errors.requestBanned, { exact: true })).toBeVisible();
    await expect(traineePage.getByText(words.refusal.bannedForever, { exact: true })).toBeVisible();

    const subjects = [words.mail.training.banned.subject, italianBanSubject()];
    await expect
      .poll(
        async () => {
          const mails = (await mailsTo(context.request, TRAINEE_ADDRESS)).filter(
            (mail) =>
              !known.includes(mail.ID) && subjects.some((subject) => mail.Subject.startsWith(subject)),
          );
          const texts = await Promise.all(mails.map((mail) => textOf(context.request, mail.ID)));
          return texts.filter((text) => text.includes(REASON)).length;
        },
        { message: `the mail of the ban to ${TRAINEE_ADDRESS}`, timeout: 150_000, intervals: [5_000] },
      )
      .toBe(1);

    // ---------------------------------------------------------------- lifted from the path, and the trainee asks again
    await ban.getByRole('button', { name: words.bans.lift, exact: true }).click();
    await whileWaitingFor(staffPage, 'POST', '/lift', async () => {
      await staffPage
        .getByRole('alertdialog')
        .getByRole('button', { name: words.bans.liftConfirm, exact: true })
        .click();
    });
    await expect(staffPage.getByText(words.bans.lifted, { exact: true })).toBeVisible();
    await expect(ban.getByText(words.bans.options.status.Lifted, { exact: true })).toBeVisible();
    await expect(ban.getByRole('button', { name: words.bans.lift, exact: true })).toHaveCount(0);
    await expect(staffPage.getByText(words.trainees.standing.bannedForever, { exact: true })).toHaveCount(0);

    for (const ladder of await ladders(trainee.request)) {
      expect(ladder.refusal, `the ${ladder.kind} ladder is not banned any more`).not.toBe(words.banned);
    }

    await traineePage.reload();
    await expect(traineePage.getByText(words.errors.requestBanned, { exact: true })).toHaveCount(0);

    expect(complaints).toEqual([]);
    expect(theirComplaints).toEqual([]);
  } finally {
    await liftLeftovers(context.request);
    await trainee.close();
  }
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

async function signIn(context: BrowserContext, as: 'pilot' | null): Promise<void> {
  const response = await context.request.post(as === null ? '/e2e/signin' : `/e2e/signin?as=${as}`);
  expect(response.status(), await response.text()).toBe(200);
}

/** The ban of this run on the path, found by its reason: a line of the list of the bans — a toast is a line too. */
function banOf(on: Page) {
  return on
    .getByRole('listitem')
    .filter({ has: on.getByText(filled(words.trainees.banReason, { reason: REASON }), { exact: true }) });
}

/** Where the trainee stands on each ladder, as their own page reads it. */
async function ladders(request: APIRequestContext): Promise<readonly Ladder[]> {
  const response = await request.get('/api/training/mine');
  expect(response.status(), await response.text()).toBe(200);
  return ((await response.json()) as { paths: readonly Ladder[] }).paths;
}

/** Every ban of the trainee still in force, lifted — a run stopped half way leaves its own. */
async function liftLeftovers(staff: APIRequestContext): Promise<void> {
  const response = await staff.get(`/api/training/bans?pageSize=100&filter[vid]=${String(TRAINEE)}`);
  expect(response.status(), await response.text()).toBe(200);

  for (const ban of ((await response.json()) as { items: Ban[] }).items.filter((each) => each.holds)) {
    const lifted = await staff.post(`/api/training/bans/${String(ban.id)}/lift`, {
      headers: asTheClientDoes,
      data: { rowVersion: ban.rowVersion },
    });
    expect(lifted.status(), await lifted.text()).toBe(200);
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

/** The text of one message Mailpit holds. */
async function textOf(request: APIRequestContext, id: string): Promise<string> {
  const read = await request.get(`${mailpit}/api/v1/message/${id}`);
  expect(read.status(), `Mailpit at ${mailpit}`).toBe(200);
  return ((await read.json()) as { Text: string }).Text;
}

/** A sentence of the language file with its values in. */
function filled(sentence: string, values: Record<string, string>): string {
  return Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);
}

/** The subject of the mail in Italian: the trainee reads their mails in their own language, which the bench does not fix. */
function italianBanSubject(): string {
  return (
    JSON.parse(
      readFileSync(fileURLToPath(new URL('../../../locales/it/training.json', import.meta.url)), 'utf8'),
    ) as { mail: { training: { banned: { subject: string } } } }
  ).mail.training.banned.subject;
}

/** The module's own English, read from the copy `pnpm i18n:sync` keeps at the root: no user facing string is written here. */
function englishTraining() {
  const training = JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/training.json', import.meta.url)), 'utf8'),
  ) as {
    errors: { requestBanned: string };
    refusal: { bannedForever: string };
    trainees: {
      open: string;
      ban: string;
      fields: { vid: string };
      sections: { ladders: string; trainings: string };
      standing: { bannedForever: string };
      banReason: string;
    };
    bans: {
      give: string;
      given: string;
      lift: string;
      liftConfirm: string;
      lifted: string;
      fields: { vid: string; reason: string };
      options: { status: { Holds: string; Lifted: string } };
    };
    mail: { training: { banned: { subject: string } } };
  };

  // The refusal as the server sends it, a bare key: the page says its sentence.
  return { ...training, banned: 'training:errors.requestBanned' };
}
