import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheApi } from './fixtures';

/**
 * The validation in a browser, with the server stubbed (M2, T13b): the queue keeps its order as the validator's preference
 * and narrows to a tour; the page of a report is taken, its errors ticked — the suggestion asked of the server each time,
 * never worked out here — and decided, with the refusal of the server under its field. What the server decides is proved
 * by `PirepTests.Review`; the round against the real server is `full/tours-review.spec.ts`.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  review: {
    title: string;
    take: string;
    decide: string;
    fields: { overrideReason: string };
    order: { tour: string };
    options: { status: { Rejected: string; Accepted: string } };
    plan: { atTakeoff: string };
  };
  errors: { reviewOverrideNeedsReason: string };
};

const validatorBootstrap = {
  ...staffBootstrap,
  user: { ...staffBootstrap.user, vid: 333333, lastName: 'Validator', departments: ['FOD'] },
  permissions: [
    { name: 'Tours.View', department: 'FOD' },
    { name: 'Tours.Validate', department: 'FOD' },
  ],
};

const tours = {
  items: [
    { id: 7, title: { en: 'Alpine loop', it: 'Anello alpino' } },
    { id: 8, title: { en: 'Island hopper', it: 'Tra le isole' } },
  ],
  page: 1,
  pageSize: 100,
  total: 2,
};

const row = {
  id: 5,
  tourId: 7,
  tourTitle: { en: 'Alpine loop', it: 'Anello alpino' },
  legId: 1,
  legNumber: 1,
  departureIcao: 'LIRF',
  arrivalIcao: 'LIMC',
  pilot: { vid: 222222, name: 'Test Pilot' },
  takeoffAt: '2026-09-22T10:10:00Z',
  queuedAt: '2026-09-22T12:00:00Z',
  status: 'Queued',
  assignedTo: null,
  leaseUntil: null,
  isDisputed: false,
  isOwn: false,
  canTake: true,
};

const plan = (revision: number, level: string) => ({
  revision,
  filedAt: '2026-09-22T09:40:00Z',
  departureIcao: 'LIRF',
  arrivalIcao: 'LIMC',
  alternateIcao: 'LIML',
  secondAlternateIcao: null,
  aircraftIcao: 'A320',
  wakeTurbulence: 'M',
  equipment: 'SDFG',
  transponder: 'S',
  flightRules: 'I',
  flightType: 'S',
  level,
  speed: 'N0450',
  route: 'RAVA5A RAVAL UL995 TOP',
  remarks: null,
  departureTimeMinutes: 600,
  enrouteMinutes: 70,
});

const DANGEROUS = 31;
const WARNING = 32;

function review(overrides: Record<string, unknown> = {}) {
  return {
    id: 5,
    tourId: 7,
    tourTitle: { en: 'Alpine loop', it: 'Anello alpino' },
    tourSlug: 'alpine-loop',
    status: 'Queued',
    isDisputed: false,
    isOwn: false,
    pilot: { vid: 222222, name: 'Test Pilot' },
    submittedAt: '2026-09-22T12:00:00Z',
    resubmittedAt: null,
    queuedAt: '2026-09-22T12:00:00Z',
    leg: {
      legId: 1,
      number: 1,
      departureIcao: 'LIRF',
      arrivalIcao: 'LIMC',
      distanceNm: 319,
      callsigns: ['ITY101'],
      aircraft: { types: [], groups: [] },
    },
    airports: [
      {
        icao: 'LIRF',
        iata: 'FCO',
        name: 'Fiumicino',
        countryId: 'IT',
        latitude: 41.8,
        longitude: 12.24,
        elevationFeet: 13,
      },
      {
        icao: 'LIMC',
        iata: 'MXP',
        name: 'Malpensa',
        countryId: 'IT',
        latitude: 45.63,
        longitude: 8.72,
        elevationFeet: 768,
      },
    ],
    flightRules: 'I',
    sid: 'RAVA5A',
    star: null,
    approach: 'ILS 35R',
    isDiversion: false,
    diversionIcao: null,
    diversionReason: null,
    diversionNote: null,
    pilotRemarks: 'Smooth flight.',
    atcContacts: [{ callsign: 'LIRF_TWR', frequency: '118.700', origin: 'Proposed' }],
    exemptions: [],
    atcArchiveAvailable: true,
    flights: [
      {
        seq: 1,
        trackerSessionId: 90001,
        callsign: 'ITY101',
        aircraft: 'A320',
        departureIcao: 'LIRF',
        arrivalIcao: 'LIMC',
        takeoffAt: '2026-09-22T10:10:00Z',
        landingAt: '2026-09-22T11:20:00Z',
        flightPlans: [plan(1, 'F340'), plan(2, 'F360')],
        planAtTakeoffRevision: 2,
        hasTrack: false,
      },
    ],
    rules: [],
    errors: [
      {
        id: DANGEROUS,
        name: { en: 'Runway incursion', it: 'Incursione di pista' },
        category: 'Dangerous',
        yearlyMax: null,
        ruleCodes: ['GR2'],
        countInYear: 0,
        countEver: 0,
        marked: false,
        suggestedByCheck: false,
      },
      {
        id: WARNING,
        name: { en: 'Late plan', it: 'Piano in ritardo' },
        category: 'Warning',
        yearlyMax: 3,
        ruleCodes: ['GR5'],
        countInYear: 1,
        countEver: 4,
        marked: false,
        suggestedByCheck: false,
      },
    ],
    suggestion: { outcome: 'Accepted', reasons: [] },
    profile: {
      pilot: { vid: 222222, name: 'Test Pilot' },
      reported: 3,
      accepted: 2,
      rejected: 0,
      disputed: 0,
      bans: [],
    },
    assignedTo: null,
    leaseUntil: null,
    decidedBy: null,
    decidedAt: null,
    noteToPilot: null,
    staffNote: null,
    thresholdOverridden: false,
    overrideReason: null,
    weatherAvailable: false,
    checksAvailable: false,
    history: [
      {
        fromStatus: null,
        toStatus: 'Queued',
        by: { vid: 222222, name: 'Test Pilot' },
        at: '2026-09-22T12:00:00Z',
        note: 'flightops:events.submitted',
      },
    ],
    actions: { canTake: true, canRelease: false, canDecide: false, canReopen: false },
    rowVersion: '2026-09-22T12:00:00Z',
    ...overrides,
  };
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

interface Seen {
  queue: string[];
  preference: unknown[];
  suggestion: string[];
  decisions: Record<string, unknown>[];
}

async function stubTheValidator(page: Page, stored: unknown = null): Promise<Seen> {
  const seen: Seen = { queue: [], preference: [], suggestion: [], decisions: [] };
  let current = review();

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(validatorBootstrap)));
  await page.route('**/api/me/preferences/flightops.reviewQueueOrder', (route) => {
    if (route.request().method() === 'PUT') {
      const value = (route.request().postDataJSON() as { value: unknown }).value;
      seen.preference.push(value);
      return route.fulfill(json({ key: 'flightops.reviewQueueOrder', value }));
    }

    return route.fulfill(json({ key: 'flightops.reviewQueueOrder', value: stored }));
  });
  await page.route('**/api/flightops/tours?**', (route) => route.fulfill(json(tours)));
  await page.route('**/api/flightops/review/queue?**', (route) => {
    seen.queue.push(decodeURIComponent(route.request().url()));
    return route.fulfill(json({ items: [row], page: 1, pageSize: 25, total: 1 }));
  });
  await page.route('**/api/flightops/review/5', (route) => route.fulfill(json(current)));
  await page.route('**/api/flightops/review/5/take', (route) => {
    current = review({
      status: 'InReview',
      assignedTo: { vid: 333333, name: 'Test Validator' },
      leaseUntil: '2026-09-22T12:30:00Z',
      actions: { canTake: false, canRelease: true, canDecide: true, canReopen: false },
      rowVersion: '2026-09-22T12:01:00Z',
    });
    return route.fulfill(json(current));
  });
  await page.route('**/api/flightops/review/5/suggestion**', (route) => {
    const url = decodeURIComponent(route.request().url());
    seen.suggestion.push(url);
    return route.fulfill(
      json(
        url.includes(`errorIds=${DANGEROUS}`)
          ? {
              outcome: 'Rejected',
              reasons: [{ errorId: DANGEROUS, reason: 'dangerous', countInYear: null, yearlyMax: null }],
            }
          : { outcome: 'Accepted', reasons: [] },
      ),
    );
  });
  await page.route('**/api/flightops/review/5/decide', (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.decisions.push(body);

    // The first time, the server refuses an acceptance against the suggestion without its reason.
    if (seen.decisions.length === 1) {
      return route.fulfill(
        json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { overrideReason: ['flightops:errors.reviewOverrideNeedsReason'] },
          },
          400,
        ),
      );
    }

    current = review({
      status: body.outcome,
      decidedBy: { vid: 333333, name: 'Test Validator' },
      decidedAt: '2026-09-22T12:05:00Z',
      actions: { canTake: false, canRelease: false, canDecide: false, canReopen: true },
      rowVersion: '2026-09-22T12:05:00Z',
    });
    return route.fulfill(json(current));
  });

  return seen;
}

test('the queue keeps the order the validator chose, and narrows to one tour', async ({ page }) => {
  const seen = await stubTheValidator(page);
  await page.goto('/staff/tours/review');

  await expect(page.getByRole('heading', { level: 1, name: words.review.title })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Test Pilot (222222)' })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'LIRF → LIMC' })).toBeVisible();
  expect(seen.queue.at(-1)).toContain('sort=queuedAt');
  expect(seen.queue.at(-1)).toContain('filter[open]=true');

  // By tour: kept on the user, and asked of the server.
  await page.locator('#review-order').click();
  await page.getByRole('option', { name: words.review.order.tour }).click();
  await expect.poll(() => seen.preference).toEqual(['tour']);
  await expect.poll(() => seen.queue.at(-1)).toContain('sort=tourId');

  // One tour.
  await page.locator('#review-tour').click();
  await page.getByRole('option', { name: 'Island hopper' }).click();
  await expect(page).toHaveURL(/tour=8/);
  await expect.poll(() => seen.queue.at(-1)).toContain('filter[tourId]=8');
});

test('a stored preference is the order the queue opens with', async ({ page }) => {
  const seen = await stubTheValidator(page, 'tour');
  await page.goto('/staff/tours/review');

  await expect(page.getByRole('cell', { name: 'LIRF → LIMC' })).toBeVisible();
  expect(seen.queue.every((url) => url.includes('sort=tourId'))).toBe(true);
});

test('a report is taken, its errors ticked with the server suggesting, and decided', async ({ page }) => {
  const seen = await stubTheValidator(page);
  await page.goto('/staff/tours/review/5');

  // The plan at take-off first, and marked.
  const plans = page.getByRole('table').first();
  await expect(plans.getByRole('row').nth(1)).toContainText(words.review.plan.atTakeoff);
  await expect(plans.getByRole('row').nth(1)).toContainText('F360');

  // Nothing to tick before the report is in the reader's hands.
  const dangerous = page.getByRole('checkbox', { name: 'Runway incursion' });
  await expect(dangerous).toBeDisabled();

  await page.getByRole('button', { name: words.review.take }).click();
  await expect(dangerous).toBeEnabled();

  // Ticked: the server is asked, and says rejection.
  await dangerous.check();
  await expect.poll(() => seen.suggestion.at(-1)).toContain(`errorIds=${DANGEROUS}`);
  const suggestion = page.getByTestId('review-suggestion');
  await expect(suggestion).toContainText(words.review.options.status.Rejected);

  // Accepted anyway: the server's refusal lands under the field it is about.
  await page.getByRole('button', { name: words.review.decide }).click();
  await expect(page.getByText(words.errors.reviewOverrideNeedsReason)).toBeVisible();
  expect(seen.decisions[0]).toMatchObject({ outcome: 'Accepted', errorIds: [DANGEROUS] });

  await page.getByLabel(words.review.fields.overrideReason).fill('The tower cleared it.');
  await page.getByRole('button', { name: words.review.decide }).click();
  await expect.poll(() => seen.decisions.length).toBe(2);
  expect(seen.decisions[1]).toMatchObject({
    outcome: 'Accepted',
    errorIds: [DANGEROUS],
    overrideReason: 'The tower cleared it.',
    rowVersion: '2026-09-22T12:01:00Z',
  });
  await expect(page.getByText(words.review.options.status.Accepted).first()).toBeVisible();
});
