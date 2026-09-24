import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheApi } from './fixtures';

/**
 * The validation in a browser, with the server stubbed (M2, T13b): the queue keeps its order as the validator's preference
 * and narrows to a tour; the page of a report is taken, its errors ticked — the suggestion asked of the server each time,
 * never worked out here — and decided, with the refusal of the server under its field; a dispute is decided with the answer
 * the pilot reads in its thread (T14b). What the server decides is proved by `PirepTests.Review` and `PirepTests.Disputes`;
 * the rounds against the real server are `full/tours-review.spec.ts` and `full/tours-dispute.spec.ts`.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  review: {
    title: string;
    take: string;
    decide: string;
    decideDispute: string;
    disputeThread: string;
    fields: { overrideReason: string; answer: string; outcome: string };
    order: { tour: string };
    checks: { suggests: string };
    options: {
      status: { Rejected: string; Accepted: string; Queued: string };
      checks: { Accept: string };
      outcome: { Upheld: string };
      disputeStatus: { Open: string; Upheld: string };
    };
    plan: { atTakeoff: string };
    weather: { airportNone: string };
  };
  errors: { reviewOverrideNeedsReason: string };
  evidence: { equipmentMissing: string };
};

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

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
  failedChecks: 1,
  checkSuggestion: 'Accepted',
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
        suggestedByCheck: true,
      },
    ],
    suggestion: { outcome: 'Accepted', reasons: [] },
    profile: {
      pilot: { vid: 222222, name: 'Test Pilot' },
      reported: 3,
      accepted: 2,
      rejected: 0,
      disputesOpen: 0,
      disputesUpheld: 0,
      disputesDismissed: 0,
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
    weather: [
      {
        icao: 'LIRF',
        role: 'Departure',
        from: '2026-09-22T09:10:00Z',
        to: '2026-09-22T12:20:00Z',
        metars: [
          {
            kind: 'Metar',
            issuedAt: '2026-09-22T09:50:00Z',
            raw: 'LIRF 220950Z 24008KT CAVOK 22/12 Q1015',
            source: 'noaa',
          },
        ],
        tafs: [],
      },
      {
        icao: 'LIMC',
        role: 'Arrival',
        from: '2026-09-22T09:10:00Z',
        to: '2026-09-22T12:20:00Z',
        metars: [],
        tafs: [],
      },
    ],
    checks: [
      {
        key: 'equipment',
        outcome: 'Failed',
        evidence: [
          {
            key: 'flightops:evidence.equipmentMissing',
            values: { letters: 'Y', rules: 'I', filed: 'SDFG/S' },
          },
        ],
        ranBy: 'Server',
        ranAt: '2026-09-22T12:00:05Z',
        errorIds: [WARNING],
      },
    ],
    checksRanAt: '2026-09-22T12:00:05Z',
    history: [
      {
        fromStatus: null,
        toStatus: 'Queued',
        by: { vid: 222222, name: 'Test Pilot' },
        at: '2026-09-22T12:00:00Z',
        note: 'flightops:events.submitted',
      },
    ],
    dispute: null,
    actions: {
      canTake: true,
      canRelease: false,
      canDecide: false,
      canReopen: false,
      canDecideDispute: false,
    },
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
  disputes: Record<string, unknown>[];
}

async function stubTheValidator(
  page: Page,
  stored: unknown = null,
  initial: Record<string, unknown> = {},
): Promise<Seen> {
  const seen: Seen = { queue: [], preference: [], suggestion: [], decisions: [], disputes: [] };
  let current = review(initial);

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
  await page.route('**/api/flightops/review/5/dispute', (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.disputes.push(body);
    current = review({
      ...initial,
      status: 'Queued',
      isDisputed: false,
      dispute: { ...(initial.dispute as object), status: 'Upheld', decidedAt: '2026-09-23T09:00:00Z' },
      actions: {
        canTake: true,
        canRelease: false,
        canDecide: false,
        canReopen: false,
        canDecideDispute: false,
      },
      rowVersion: '2026-09-23T09:00:00Z',
    });
    return route.fulfill(json(current));
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
  await expect(page.getByRole('cell', { name: words.review.options.checks.Accept })).toBeVisible();
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

  // The weather kept for the flight (T16): the METAR with who published it, and «none kept» said as such.
  await expect(page.getByText('LIRF 220950Z 24008KT CAVOK 22/12 Q1015')).toBeVisible();
  await expect(page.getByText(words.review.weather.airportNone)).toBeVisible();

  // The checks (T17): what failed, worded from the server's key, and the error it suggests — not ticked by anybody.
  await expect(
    page.getByText(filled(words.evidence.equipmentMissing, { letters: 'Y', rules: 'I', filed: 'SDFG/S' })),
  ).toBeVisible();
  await expect(page.getByText(filled(words.review.checks.suggests, { errors: 'Late plan' }))).toBeVisible();
  await expect(page.getByRole('checkbox', { name: 'Late plan' })).not.toBeChecked();

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

test('a dispute is decided with the answer the pilot reads in its thread', async ({ page }) => {
  const seen = await stubTheValidator(page, null, {
    status: 'Rejected',
    isDisputed: true,
    decidedBy: { vid: 444444, name: 'Other Validator' },
    decidedAt: '2026-09-22T12:05:00Z',
    dispute: {
      status: 'Open',
      text: 'The chart had that SID.',
      disputedAt: '2026-09-22T18:00:00Z',
      decidedBy: null,
      decidedAt: null,
      threadId: 12,
      department: 'FOD',
    },
    actions: {
      canTake: false,
      canRelease: false,
      canDecide: false,
      canReopen: false,
      canDecideDispute: true,
    },
  });
  await page.goto('/staff/tours/review/5');

  await expect(page.getByText(words.review.options.disputeStatus.Open, { exact: true })).toBeVisible();
  await expect(page.getByText('The chart had that SID.')).toBeVisible();
  // The validator holds no Contacts.View here: the thread is theirs to read among their own.
  await expect(page.getByRole('link', { name: words.review.disputeThread })).toHaveAttribute(
    'href',
    '/me/contacts/12',
  );

  await page
    .getByText(words.review.fields.outcome, { exact: true })
    .locator('..')
    .getByRole('combobox')
    .click();
  await page.getByRole('option', { name: words.review.options.outcome.Upheld, exact: true }).click();
  await page.getByLabel(words.review.fields.answer).fill('You were right.');
  await page.getByRole('button', { name: words.review.decideDispute }).click();

  await expect.poll(() => seen.disputes.length).toBe(1);
  expect(seen.disputes[0]).toEqual({
    upheld: true,
    answer: 'You were right.',
    rowVersion: '2026-09-22T12:00:00Z',
  });
  await expect(page.getByText(words.review.options.disputeStatus.Upheld, { exact: true })).toBeVisible();
  await expect(page.getByText(words.review.options.status.Queued, { exact: true }).first()).toBeVisible();
});
