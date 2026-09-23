import { expect, test } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, choose, mailFor, readInEnglish, signIn, whileWaitingFor } from './bench';

/**
 * The threads of the contacts (M2, T14a), through the real screens against the real server: a member writes to a
 * department from the form and finds the message among their own; the staff answers it from the back office; the
 * member receives the mail in Mailpit — the answer, the link to the thread, never who wrote it — and answers from
 * `/me/contacts`, which puts the message back on top of the queue.
 *
 * Two people, as in the validation's round: the bench's web master, who reaches every department, and its pilot, a
 * member with no position (`/e2e/signin?as=pilot`). A message is never deleted, so each run leaves one behind, named
 * after the run.
 */

const words = englishCommon as unknown as {
  departments: { FOD: string };
  contacts: {
    send: string;
    open: string;
    fields: { department: string; subject: string; body: string };
    sent: { open: string };
    options: { status: { New: string; Answered: string } };
    thread: { send: string; fields: { body: string }; department: string };
  };
};

const pilotAddress = 'bench-pilot@bench.test';
const stamp = Date.now().toString(36);
const subject = `Bench question ${stamp}`;

test('a member writes, the department answers from the back office, and the member answers back from their messages', async ({
  page,
  context,
  browser,
}) => {
  test.setTimeout(240_000);
  await readInEnglish(context);
  await signIn(context);

  const pilotContext = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(pilotContext);
  const signedIn = await pilotContext.request.post('/e2e/signin?as=pilot');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  const pilot = await pilotContext.newPage();

  // ------------------------------------------------------------------ the member writes
  await pilot.goto('/contact');
  await choose(pilot, words.contacts.fields.department, words.departments.FOD);
  await pilot.getByLabel(words.contacts.fields.subject, { exact: true }).fill(subject);
  await pilot
    .getByLabel(words.contacts.fields.body, { exact: true })
    .fill('Which alternate is right for the third leg?');
  await whileWaitingFor(pilot, 'POST', '/api/contacts', async () => {
    await pilot.getByRole('button', { name: words.contacts.send, exact: true }).click();
  });

  // The sent message says where the answers will be, and there it is.
  await pilot.getByRole('link', { name: words.contacts.sent.open, exact: true }).click();
  await expect(pilot).toHaveURL(/\/me\/contacts(\?|$)/);
  const row = pilot.getByRole('row').filter({ hasText: subject });
  await expect(row).toBeVisible();
  await row.getByRole('link', { name: words.contacts.open, exact: true }).click();
  await expect(pilot).toHaveURL(/\/me\/contacts\/\d+$/);
  const id = /\/me\/contacts\/(\d+)$/.exec(pilot.url())![1]!;

  // ------------------------------------------------------------------ the department answers
  await page.goto(`/staff/fod/contacts/${id}`);
  await expect(page.getByText('Which alternate is right for the third leg?')).toBeVisible();
  await page
    .getByLabel(words.contacts.thread.fields.body, { exact: true })
    .fill(`LIML, as the briefing says. ${stamp}`);
  await whileWaitingFor(page, 'POST', `/api/contacts/${id}/replies`, async () => {
    await page.getByRole('button', { name: words.contacts.thread.send, exact: true }).click();
  });
  await expect(page.getByText(`LIML, as the briefing says. ${stamp}`)).toBeVisible();

  // ------------------------------------------------------------------ the mail, in Mailpit
  let mail: { Subject: string; Text: string } | null = null;
  await expect
    .poll(
      async () => {
        mail = await mailFor(pilotContext.request, pilotAddress, `Re: ${subject}`);
        return mail !== null;
      },
      { timeout: 150_000, intervals: [5_000] },
    )
    .toBe(true);

  const text = mail!.Text;
  expect(text).toContain(`LIML, as the briefing says. ${stamp}`);
  expect(text).toContain(`/me/contacts/${id}`);
  // Never who answered (design M2 §3.5): not the bench's name, not its VID.
  expect(text).not.toContain('999001');

  // ------------------------------------------------------------------ the member reads and answers back
  await pilot.goto(`/me/contacts/${id}`);
  await expect(pilot.getByText(`LIML, as the briefing says. ${stamp}`)).toBeVisible();
  const department = words.contacts.thread.department.replace('{{department}}', words.departments.FOD);
  await expect(pilot.getByText(department).first()).toBeVisible();
  await expect(pilot.getByText('999001')).toHaveCount(0);

  await pilot.getByLabel(words.contacts.thread.fields.body, { exact: true }).fill('Thank you!');
  await whileWaitingFor(pilot, 'POST', `/api/contacts/${id}/replies`, async () => {
    await pilot.getByRole('button', { name: words.contacts.thread.send, exact: true }).click();
  });
  await expect(pilot.getByText('Thank you!')).toBeVisible();

  // Back on top of the department's queue.
  await page.goto('/staff/fod/contacts');
  await expect(
    page.getByRole('row').filter({ hasText: subject }).getByText(words.contacts.options.status.New),
  ).toBeVisible();

  await pilotContext.close();
});
