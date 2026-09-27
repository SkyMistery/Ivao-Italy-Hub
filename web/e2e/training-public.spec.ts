import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import {
  staffBootstrap,
  stubTheApi,
  stubTheApiAsStaff,
  stubTheBlockData,
  stubThePublishedPage,
} from './fixtures';
import { englishCommon } from './locales';

/**
 * The public side of the training and its four blocks in a browser, with the API stubbed (M3, A10b): a visitor reads the sessions
 * still to be held on `/training` and the page of one — position, rating, date and time, and nobody's name — and is offered the
 * login; a signed in member reads who is in them; a session with no page is not found; and the blocks draw what their providers
 * answer, on a page of the site and on the two dashboards. What the server decides — what is public, who reads the people — is
 * proved by `TrainingBlocksTests` (integration); the round against the real server is `full/training-upcoming.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  kinds: Record<string, string>;
  mockExam: string;
  request: { send: string };
  mine: { title: string; canAsk: string; chooseDate: string; datesWaiting_other: string; readReport: string };
  public: {
    title: string;
    upcoming: string;
    none: string;
    people: string;
    signIn: string;
    back: string;
    session: {
      title: string;
      position: string;
      rating: string;
      when: string;
      trainee: string;
      trainer: string;
      signIn: string;
    };
  };
  blocks: {
    signIn: string;
    trainerQueue: { waiting: string; waitingDays_other: string; toPropose: string; toReport: string };
    approvalQueue: { toApprove_other: string; toAssign_one: string };
  };
};

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const trainee = { vid: 790099, name: 'Test Trainee' };
const trainer = { vid: 790098, name: 'Test Trainer' };

/** A member who is not of the staff: the people of a session are theirs to read, nothing of the back office is. */
const memberBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: 790095,
    lastName: 'Member',
    isStaff: false,
    positions: [],
    departments: [],
  },
  permissions: [],
};

function session(id: number, overrides: Record<string, unknown> = {}) {
  return {
    id,
    kind: 'Atc',
    ratingShortName: 'ADC',
    position: 'XXAA_TWR',
    startsAtUtc: '2026-10-02T18:00:00Z',
    held: false,
    trainee: null,
    trainer: null,
    ...overrides,
  };
}

const pilotSession = session(42, {
  kind: 'Pilot',
  ratingShortName: 'P2',
  position: null,
  startsAtUtc: '2026-10-05T19:00:00Z',
});

/** The list of `/training` and one session by its address, answered on top of whichever stub the test chose. */
async function stubTheSessions(
  page: Page,
  list: unknown[],
  one: Record<number, unknown> = {},
): Promise<void> {
  await page.route(
    (url) => url.pathname === '/api/training/sessions',
    (route) => route.fulfill(json(list)),
  );
  await page.route('**/api/training/sessions/*', (route) => {
    const id = Number(new URL(route.request().url()).pathname.split('/').at(-1));
    return route.fulfill(id in one ? json(one[id]) : json({ title: 'Not Found', status: 404 }, 404));
  });
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a visitor reads the sessions still to be held, with nobody named, and may request a training', async ({
  page,
}) => {
  await stubTheApi(page);
  await stubTheSessions(page, [session(41), pilotSession]);

  await page.goto('/training');

  await expect(page.getByRole('heading', { level: 1, name: words.public.title })).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: words.public.upcoming })).toBeVisible();
  await expect(page.getByRole('link', { name: words.request.send })).toHaveAttribute(
    'href',
    '/training/request',
  );
  await expect(page.getByRole('link', { name: words.mine.title })).toHaveCount(0);

  // Each session by its rating and position — a pilot's by its rating —, a link to its page, in UTC and where the division lives.
  await expect(page.getByRole('link', { name: 'ADC · XXAA_TWR', exact: true })).toHaveAttribute(
    'href',
    '/training/sessions/41',
  );
  await expect(page.getByRole('link', { name: 'P2', exact: true })).toHaveAttribute(
    'href',
    '/training/sessions/42',
  );
  await expect(page.getByText(/UTC$/).first()).toBeVisible();
  await expect(page.getByText(/\(Europe\/Rome\)$/).first()).toBeVisible();

  // Nobody, and the login instead.
  await expect(page.locator('main')).not.toContainText('Trainee:');
  await expect(page.getByRole('link', { name: words.public.signIn })).toHaveAttribute(
    'href',
    '/auth/login?returnUrl=%2Ftraining',
  );
});

test('with nothing to come, /training says so', async ({ page }) => {
  await stubTheApi(page);
  await stubTheSessions(page, []);

  await page.goto('/training');

  await expect(page.getByText(words.public.none)).toBeVisible();
});

test('the page of a session tells a visitor where and when, and offers the login for who', async ({
  page,
}) => {
  await stubTheApi(page);
  await stubTheSessions(page, [], { 41: session(41) });

  await page.goto('/training/sessions/41');

  await expect(
    page.getByRole('heading', {
      level: 1,
      name: filled(words.public.session.title, { title: 'ADC · XXAA_TWR' }),
    }),
  ).toBeVisible();
  const details = page.locator('dl');
  await expect(details.getByText(words.public.session.position, { exact: true })).toBeVisible();
  await expect(details.getByText('XXAA_TWR', { exact: true })).toBeVisible();
  await expect(details.getByText(words.public.session.rating, { exact: true })).toBeVisible();
  await expect(details.getByText(/UTC$/)).toBeVisible();
  await expect(details.getByText(words.public.session.trainee, { exact: true })).toHaveCount(0);
  await expect(details.getByText(words.public.session.trainer, { exact: true })).toHaveCount(0);

  await expect(page.getByRole('link', { name: words.public.session.signIn })).toHaveAttribute(
    'href',
    '/auth/login?returnUrl=%2Ftraining%2Fsessions%2F41',
  );
  await expect(page.getByRole('link', { name: words.public.back })).toHaveAttribute('href', '/training');
});

test('a signed in member reads who is in a session, on its page and on /training', async ({ page }) => {
  await stubTheApiAsStaff(page, memberBootstrap);
  const named = session(41, { trainee, trainer });
  await stubTheSessions(page, [named], { 41: named });

  await page.goto('/training/sessions/41');

  const details = page.locator('dl');
  await expect(details.getByText(words.public.session.trainee, { exact: true })).toBeVisible();
  await expect(details.getByText('Test Trainee (790099)', { exact: true })).toBeVisible();
  await expect(details.getByText('Test Trainer (790098)', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.public.session.signIn })).toHaveCount(0);

  await page.goto('/training');

  await expect(
    page.getByText(
      filled(words.public.people, { trainee: 'Test Trainee (790099)', trainer: 'Test Trainer (790098)' }),
    ),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: words.mine.title })).toHaveAttribute('href', '/training/mine');
  await expect(page.getByRole('link', { name: words.public.signIn })).toHaveCount(0);
});

test('a session with no page is not found', async ({ page }) => {
  await stubTheApi(page);
  await stubTheSessions(page, [], {});

  await page.goto('/training/sessions/43');

  await expect(page.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
});

test('on a page of the site the sessions draw as on /training, and a personal block asks a visitor to sign in', async ({
  page,
}) => {
  await stubThePublishedPage(page, 'training-days', {
    schemaVersion: 1,
    sections: [
      {
        id: 's_main',
        layout: 'stacked',
        blocks: [
          { id: 'b_sessions', type: 'training.upcomingSessions', version: 1, props: { limit: 5 } },
          { id: 'b_mine', type: 'training.myTraining', version: 1, props: {} },
        ],
      },
    ],
  });
  await stubTheBlockData(page, 'training.upcomingSessions', { signedIn: false, items: [session(41)] });
  await stubTheBlockData(page, 'training.myTraining', { signedIn: false });

  await page.goto('/training-days');

  await expect(page.getByRole('link', { name: 'ADC · XXAA_TWR', exact: true })).toHaveAttribute(
    'href',
    '/training/sessions/41',
  );
  await expect(page.getByRole('link', { name: words.public.signIn })).toBeVisible();
  await expect(page.getByText(words.blocks.signIn, { exact: true })).toBeVisible();
});

/** A published dashboard of the site's department, with one section of tiles. */
function dashboard(slug: string, title: string, blocks: readonly unknown[]) {
  return {
    id: slug === 'staff' ? 42 : 43,
    kind: 'Dashboard',
    slug,
    path: slug,
    ownerDepartment: 'WD',
    title: { en: title, it: title },
    summary: null,
    seo: null,
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_tiles',
          key: 'tiles',
          layout: 'stacked',
          background: 'none',
          padding: 'sm',
          width: 'full',
          blocks,
        },
      ],
    },
    schemaVersion: 1,
    collections: [],
    coverMediaId: null,
    fileMediaId: null,
    version: 1,
    publishedAt: '2026-09-13T20:00:00.000Z',
    effectiveOn: null,
    reviewOn: null,
    retiredAt: null,
    supersededBySlug: null,
    supersededByTitle: null,
    showFooter: true,
    publishedByName: null,
    media: {},
  };
}

async function stubTheDashboard(page: Page, slug: string, answer: unknown): Promise<void> {
  await page.route(`**/api/content/public/Dashboard/${slug}`, (route) => route.fulfill(json(answer)));
}

function traineeTraining(id: number, state: string, overrides: Record<string, unknown> = {}) {
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
    availabilityText: null,
    notesText: null,
    requestedAt: '2026-09-20T18:00:00Z',
    decidedAt: '2026-09-21T09:00:00Z',
    trainer,
    slots: [],
    scheduledStartUtc: null,
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
    rowVersion: '2026-09-21T09:00:00.000001Z',
    ...overrides,
  };
}

function ladder(kind: 'Atc' | 'Pilot', overrides: Record<string, unknown> = {}) {
  return {
    kind,
    ratingShortName: kind === 'Atc' ? 'AS3' : 'P1',
    hours: 120,
    next: { kind, number: 5, shortName: kind === 'Atc' ? 'ADC' : 'P2', nameKey: `ratings.${kind}.X` },
    isMockExam: false,
    asksPosition: kind === 'Atc',
    positions: [],
    refusal: null,
    bannedUntil: null,
    openTrainingId: null,
    waitUntil: null,
    minimumHours: null,
    ...overrides,
  };
}

test("on /me the trainee's block says where each ladder stands, and what to do next", async ({ page }) => {
  await stubTheApiAsStaff(page, memberBootstrap);
  await stubTheDashboard(
    page,
    'me',
    dashboard('me', 'My dashboard', [
      { id: 'b_mine', type: 'training.myTraining', version: 1, props: {}, span: 12 },
    ]),
  );
  await page.route('**/api/me/notifications', (route) => route.fulfill(json([])));
  await stubTheBlockData(page, 'training.myTraining', {
    signedIn: true,
    vid: trainee.vid,
    name: trainee.name,
    asksTheory: true,
    theoryExamUrl: null,
    paths: [
      ladder('Atc', { refusal: 'training:errors.requestOpen', openTrainingId: 7 }),
      ladder('Pilot', { isMockExam: true }),
    ],
    trainings: [
      traineeTraining(7, 'Assigned', {
        slots: [
          { id: 1, startsAtUtc: '2099-10-02T18:00:00Z', endsAtUtc: '2099-10-02T20:00:00Z' },
          { id: 2, startsAtUtc: '2099-10-04T18:00:00Z', endsAtUtc: '2099-10-04T20:00:00Z' },
        ],
      }),
      traineeTraining(5, 'Completed', {
        kind: 'Pilot',
        rating: 6,
        ratingShortName: 'P2',
        position: null,
        completedAt: '2026-09-15T21:30:00Z',
        readyForMockExam: true,
      }),
    ],
  });

  await page.goto('/me');

  // The ATC ladder: the open training, whose dates are to be chosen on its page.
  await expect(page.getByText(filled(words.mine.datesWaiting_other, { count: '2' }))).toBeVisible();
  await expect(page.getByRole('link', { name: words.mine.chooseDate })).toHaveAttribute(
    'href',
    '/training/mine/7',
  );

  // The pilot ladder: free, and the next one is a mock exam; the last report is a link to its page.
  await expect(page.getByText(words.mine.canAsk)).toBeVisible();
  await expect(page.getByText(words.mockExam)).toBeVisible();
  await expect(page.getByRole('link', { name: words.request.send })).toHaveAttribute(
    'href',
    '/training/request?kind=Pilot',
  );
  await expect(page.getByRole('link', { name: words.mine.readReport })).toHaveAttribute(
    'href',
    '/training/mine/5',
  );
  await expect(page.getByRole('link', { name: words.mine.title })).toHaveAttribute('href', '/training/mine');
});

test('on /staff the trainer sees what is theirs to move, and the staff what waits to be accepted or assigned', async ({
  page,
}) => {
  await stubTheApiAsStaff(page);
  await stubTheDashboard(
    page,
    'staff',
    dashboard('staff', 'Staff dashboard', [
      { id: 'b_trainer', type: 'training.trainerQueue', version: 1, props: {}, span: 6 },
      { id: 'b_approval', type: 'training.approvalQueue', version: 1, props: {}, span: 6 },
    ]),
  );

  const row = (id: number, state: string, overrides: Record<string, unknown> = {}) => ({
    id,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    state,
    trainee,
    trainer,
    createdAt: '2026-09-20T18:00:00Z',
    scheduledStartUtc: null,
    held: false,
    ...overrides,
  });

  await stubTheBlockData(page, 'training.trainerQueue', {
    signedIn: true,
    toPropose: [],
    waiting: [{ training: row(12, 'Assigned'), days: 4 }],
    toReport: [],
  });
  await stubTheBlockData(page, 'training.approvalQueue', {
    signedIn: true,
    toApprove: { count: 3, oldest: [row(21, 'Requested', { trainer: null })] },
    toAssign: {
      count: 1,
      oldest: [row(23, 'Accepted', { trainer: null, trainee: { vid: 790096, name: 'Other Trainee' } })],
    },
  });

  await page.goto('/staff');

  // In evidence: the trainee who has not chosen, for how long, and the training's page.
  await expect(page.getByText(words.blocks.trainerQueue.waiting)).toBeVisible();
  await expect(
    page.getByText(filled(words.blocks.trainerQueue.waitingDays_other, { count: '4' })),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Test Trainee (790099)' }).first()).toHaveAttribute(
    'href',
    '/staff/training/12',
  );
  await expect(page.getByText(words.blocks.trainerQueue.toPropose)).toHaveCount(0);

  // The two queues of the staff: how many, a link to the list of each, and the oldest.
  await expect(
    page.getByRole('link', { name: filled(words.blocks.approvalQueue.toApprove_other, { count: '3' }) }),
  ).toHaveAttribute('href', '/staff/training?queue=toApprove');
  await expect(
    page.getByRole('link', { name: filled(words.blocks.approvalQueue.toAssign_one, { count: '1' }) }),
  ).toHaveAttribute('href', '/staff/training?queue=toAssign');
  await expect(page.getByRole('link', { name: 'Other Trainee (790096)' })).toHaveAttribute(
    'href',
    '/staff/training/23',
  );
});
