import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { anonymousBootstrap, stubTheApi } from './fixtures';

/**
 * The pilot's report in a browser, with the tracker stubbed (M2, T11b): the tour page says where the pilot is, the
 * form finds the flights, sends one, and puts every refusal where it belongs. What the server decides is proved by
 * `PirepTests`; the round against the real server is `full/tours-report.spec.ts`.
 */

const SLUG = 'alpine-loop';

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  public: {
    myReports: string;
    sendReport: string;
    legProgress: Record<string, string>;
    reportStatus: Record<string, string>;
  };
  report: {
    title: string;
    send: string;
    notFlyable: string;
    fields: Record<string, string>;
    atc: { title: string; unavailable: string; proposedLead: string };
  };
  errors: Record<string, string>;
};

const pilotBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: 222222,
    firstName: 'Test',
    lastName: 'Pilot',
    positions: [],
    isStaff: false,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: [],
    firs: [],
  },
};

function leg(id: number, from: string, to: string) {
  return {
    id,
    number: id,
    kind: 'Normal',
    rotationId: null,
    departureIcao: from,
    departureIata: null,
    departureLatitude: 41.8 + id,
    departureLongitude: 12.2,
    arrivalIcao: to,
    arrivalIata: null,
    arrivalLatitude: 42.8 + id,
    arrivalLongitude: 11.2,
    distanceNm: 180,
    estimatedMinutes: 40,
    callsigns: ['ITY101'],
    flightNumbers: [],
    aircraft: { types: [], groups: [] },
    releaseAt: null,
    released: true,
  };
}

const tour = {
  id: 7,
  slug: SLUG,
  kind: 'Sequential',
  title: { en: 'Alpine loop', it: 'Anello alpino' },
  summary: { en: 'Three legs.', it: 'Tre leg.' },
  briefing: { schemaVersion: 1, sections: [] },
  coverMediaId: null,
  bannerMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  reportWindowDays: 7,
  progression: 'FlyAhead',
  hubRotationOrder: null,
  requiresProcedures: true,
  minPilotRating: null,
  referenceAircraftIcao: null,
  aircraft: { types: [], groups: [] },
  requiredNm: null,
  requiredSubtours: null,
  openGoal: null,
  openGoalParameters: null,
  openGoalValues: [],
  constraints: [],
  callsignRules: [],
  rules: [],
  errors: [],
  rotations: [],
  legs: [leg(1, 'LIRF', 'LIPZ'), leg(2, 'LIPZ', 'LIMC'), leg(3, 'LIMC', 'LIRF')],
  totalNm: 540,
  totalEstimatedMinutes: 120,
  parent: null,
  subtours: [],
};

const accepted = {
  id: 41,
  tourId: 7,
  legId: 1,
  status: 'Accepted',
  isDisputed: false,
  submittedAt: '2026-09-20T12:00:00Z',
  resubmittedAt: null,
  departureIcao: 'LIRF',
  arrivalIcao: 'LIPZ',
  distanceNm: 180,
  takeoffAt: '2026-09-20T10:00:00Z',
  flightRules: 'I',
  sid: null,
  star: null,
  approach: null,
  isDiversion: false,
  diversionIcao: null,
  diversionReason: null,
  diversionNote: null,
  pilotRemarks: null,
  flights: [],
  rowVersion: '2026-09-20T12:00:00Z',
};

const mine = {
  tourId: 7,
  legs: [
    { id: 1, progress: 'Done' },
    { id: 2, progress: 'Todo' },
    { id: 3, progress: 'Locked' },
  ],
  flyable: [2],
  next: 2,
  finished: false,
  blocked: null,
  goal: null,
  reports: [accepted],
};

const flight = {
  id: 90001,
  callsign: 'ITY101',
  startedAt: '2026-09-22T08:00:00Z',
  endedAt: '2026-09-22T09:10:00Z',
  departureIcao: 'LIPZ',
  arrivalIcao: 'LIMC',
  aircraft: 'A320',
};

/** The controllers the archive had online along the flight (T12). */
const proposal = {
  available: true,
  proposed: [
    { callsign: 'LIPZ_TWR', frequency: '120.205', origin: 'Proposed' },
    { callsign: 'LIMC_APP', frequency: '126.750', origin: 'Proposed' },
  ],
  attribution: 'Test outlines',
};

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** The tour, the pilot's place in it, and the tracker; what is sent is kept for the test to read. */
async function stubThePilot(
  page: Page,
  {
    sessions = [flight] as unknown,
    sessionsStatus = 200,
    atc = proposal,
    send,
  }: {
    sessions?: unknown;
    sessionsStatus?: number;
    atc?: unknown;
    send: (body: Record<string, unknown>) => { status: number; body: unknown };
  },
): Promise<void> {
  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(pilotBootstrap)));
  await page.route(`**/api/flightops/tours/public/${SLUG}`, (route) => route.fulfill(json(tour)));
  await page.route('**/api/flightops/tours/7/reports/mine', (route) => route.fulfill(json(mine)));
  await page.route('**/api/flightops/tours/7/reports/sessions**', (route) =>
    route.fulfill(json(sessionsStatus === 200 ? sessions : { title: 'Service unavailable' }, sessionsStatus)),
  );
  await page.route('**/api/flightops/tours/7/reports/atc**', (route) => route.fulfill(json(atc)));
  await page.route('**/api/flightops/tours/7/reports', (route) => {
    const answer = send(route.request().postDataJSON() as Record<string, unknown>);
    return route.fulfill(json(answer.body, answer.status));
  });
}

test('a pilot sees where they are, chooses the flight and sends the report', async ({ page }) => {
  const sent: Record<string, unknown>[] = [];
  await stubThePilot(page, {
    send: (body) => {
      sent.push(body);
      return { status: 201, body: { ...accepted, id: 42, legId: 2, status: 'Queued' } };
    },
  });

  await page.goto(`/tours/${SLUG}`);

  // The legs as the server says they are, and the report of the first.
  const legs = page.getByRole('table');
  await expect(legs.getByText(words.public.legProgress.Done!, { exact: true })).toBeVisible();
  await expect(legs.getByText(words.public.legProgress.Locked!, { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: words.public.myReports })).toBeVisible();
  await expect(page.getByText(words.public.reportStatus.Accepted!, { exact: true })).toBeVisible();

  // «Send the report» goes to the next leg.
  await page.getByRole('link', { name: words.public.sendReport }).click();
  await expect(page).toHaveURL(new RegExp(`/tours/${SLUG}/report\\?leg=2$`));
  await expect(page.getByRole('heading', { level: 1, name: words.report.title })).toBeVisible();

  await page.getByRole('radio', { name: /ITY101/ }).check();

  // The controllers online along the flight, ticked; the pilot did not contact the approach.
  await expect(page.getByText(words.report.atc.proposedLead)).toBeVisible();
  await expect(page.getByText('Test outlines')).toBeVisible();
  await page.getByRole('checkbox', { name: /LIMC_APP/ }).uncheck();

  await page.getByLabel(words.report.fields.star!, { exact: true }).fill('ODINA1A');
  await page.getByRole('button', { name: words.report.send }).click();

  await expect(page).toHaveURL(new RegExp(`/tours/${SLUG}$`));
  expect(sent).toHaveLength(1);
  expect(sent[0]).toMatchObject({
    legId: 2,
    sessionIds: [90001],
    isDiversion: false,
    star: 'ODINA1A',
    sid: null,
    atcContacts: [{ callsign: 'LIPZ_TWR', frequency: '120.205' }],
    exemptions: [],
  });
});

test('without an archive the controllers are not available, and the report still goes', async ({ page }) => {
  const sent: Record<string, unknown>[] = [];
  await stubThePilot(page, {
    atc: { available: false, proposed: [], attribution: 'Test outlines' },
    send: (body) => {
      sent.push(body);
      return { status: 201, body: { ...accepted, id: 43, legId: 2, status: 'Queued' } };
    },
  });

  await page.goto(`/tours/${SLUG}/report?leg=2`);
  await page.getByRole('radio', { name: /ITY101/ }).check();

  await expect(page.getByText(words.report.atc.unavailable)).toBeVisible();
  await page.getByRole('button', { name: words.report.send }).click();

  await expect(page).toHaveURL(new RegExp(`/tours/${SLUG}$`));
  expect(sent[0]).toMatchObject({ atcContacts: [], exemptions: [] });
});

test('each refusal lands where it belongs: the flight above, a field on its field', async ({ page }) => {
  await stubThePilot(page, {
    send: () => ({
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          sessionIds: ['flightops:errors.reportSessionClaimed'],
          sid: ['flightops:errors.reportProcedureRequired'],
          exemptions: ['flightops:errors.exemptionNotContacted'],
        },
      },
    }),
  });

  await page.goto(`/tours/${SLUG}/report?leg=2`);
  await page.getByRole('radio', { name: /ITY101/ }).check();
  await page.getByRole('button', { name: words.report.send }).click();

  await expect(page.getByText(words.errors.reportSessionClaimed!)).toBeVisible();
  await expect(page.getByText(words.errors.reportProcedureRequired!)).toBeVisible();
  await expect(page.getByText(words.errors.exemptionNotContacted!)).toBeVisible();
  await expect(page).toHaveURL(/\/report\?leg=2$/);
});

test('a tracker that does not answer is not «you did not fly»', async ({ page }) => {
  await stubThePilot(page, { sessionsStatus: 503, send: () => ({ status: 500, body: {} }) });

  await page.goto(`/tours/${SLUG}/report?leg=2`);

  await expect(page.getByText(words.errors.trackerUnavailable!)).toBeVisible();
});

test('a leg that cannot be flown now offers the ones that can', async ({ page }) => {
  await stubThePilot(page, { send: () => ({ status: 500, body: {} }) });

  await page.goto(`/tours/${SLUG}/report?leg=3`);

  await expect(page.getByText(words.report.notFlyable)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Leg 2 · LIPZ → LIMC' })).toBeVisible();
  await expect(page.getByRole('button', { name: words.report.send })).toHaveCount(0);
});
