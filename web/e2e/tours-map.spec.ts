import { expect, test, type Page } from '@playwright/test';

import { stubTheApi } from './fixtures';

/**
 * The map of a tour in a real browser, under the real policy (M2, T10).
 *
 * Everything the two public screens *say* is checked without a browser; what needs one is MapLibre,
 * and MapLibre is exactly the sort of thing this suite exists for: it needs WebGL2, it starts a
 * **worker**, it draws into a canvas, and `config/security.json` is what decides whether the browser
 * lets it. The preview server reads that same file, so a directive that refuses the worker fails
 * here rather than on the day a visitor opens a tour.
 *
 * There is no API behind this suite, so the tour arrives stubbed and the base map archive is never
 * served — which is the second thing worth proving: **the map still draws**, legs and airports on a
 * plain ground, because an installation that has not uploaded 180 MB of world yet must not get an
 * empty box (note 2026-09-22-il-pubblico-dei-tour).
 */

const SLUG = 'across-the-alps';

const tour = {
  id: 1,
  slug: SLUG,
  kind: 'Sequential',
  title: { en: 'Across the Alps', it: 'Attraverso le Alpi' },
  summary: { en: 'Two legs over the mountains.', it: 'Due tratte sopra le montagne.' },
  briefing: { schemaVersion: 1, sections: [] },
  coverMediaId: null,
  bannerMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  reportWindowDays: 7,
  progression: 'FlyAhead',
  hubRotationOrder: null,
  requiresProcedures: false,
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
  legs: [
    leg(1, 'LIRF', 41.8003, 12.2389, 'LSZH', 47.4647, 8.5492),
    // Across the antimeridian, which is the case a straight line gets wrong and the one a map with a
    // wrapped longitude draws backwards over the whole world.
    leg(2, 'RJAA', 35.7647, 140.3863, 'KLAX', 33.9416, -118.4085),
  ],
  totalNm: 5440,
  totalEstimatedMinutes: null,
  parent: null,
  subtours: [],
};

function leg(
  number: number,
  departureIcao: string,
  departureLatitude: number,
  departureLongitude: number,
  arrivalIcao: string,
  arrivalLatitude: number,
  arrivalLongitude: number,
) {
  return {
    id: number,
    number,
    kind: 'Normal',
    rotationId: null,
    departureIcao,
    departureIata: null,
    departureLatitude,
    departureLongitude,
    arrivalIcao,
    arrivalIata: null,
    arrivalLatitude,
    arrivalLongitude,
    distanceNm: 2720,
    estimatedMinutes: null,
    callsigns: ['ITY1234'],
    flightNumbers: [],
    aircraft: { types: [], groups: [] },
    releaseAt: null,
    released: true,
  };
}

async function stubTheTours(page: Page): Promise<void> {
  await stubTheApi(page);

  await page.route('**/api/flightops/tours/public', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: tour.id,
          slug: tour.slug,
          kind: tour.kind,
          title: tour.title,
          summary: tour.summary,
          coverMediaId: null,
          state: tour.state,
          releaseAt: tour.releaseAt,
          closeAt: tour.closeAt,
          legs: 2,
          totalNm: tour.totalNm,
        },
      ]),
    }),
  );

  await page.route(`**/api/flightops/tours/public/${SLUG}`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tour) }),
  );
}

/**
 * Everything the browser complained about, minus what this suite is knowingly not serving.
 *
 * ⚠️ A failed request is not a complaint here: this suite has no API behind it on purpose — the
 * stub answers anything it was not asked about with a 500, and `/tiles` has nothing behind it at
 * all. What is left is what the browser itself refused, which is the whole point: a policy that
 * will not let MapLibre start its worker says exactly that, in the console, and nowhere else.
 */
function complaintsOf(page: Page): string[] {
  const complaints: string[] = [];
  const expected = (text: string) => text.includes('favicon') || text.includes('Failed to load resource');

  page.on('pageerror', (error) => complaints.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error' && !expected(message.text())) {
      complaints.push(message.text());
    }
  });

  return complaints;
}

test('the cards of the tours are a grid a visitor can read', async ({ page }) => {
  const complaints = complaintsOf(page);
  await stubTheTours(page);

  await page.goto('/tours');

  const card = page.getByRole('article').filter({ hasText: 'Across the Alps' });
  await expect(card).toBeVisible();
  await expect(card.getByText('2 legs · 5440 NM')).toBeVisible();

  expect(complaints).toEqual([]);
});

test('the map of a tour draws, with no base map to draw on and under the real policy', async ({ page }) => {
  const complaints = complaintsOf(page);
  await stubTheTours(page);

  await page.goto(`/tours/${SLUG}`);
  await expect(page.getByRole('heading', { level: 1, name: 'Across the Alps' })).toBeVisible();

  // The canvas MapLibre draws into. It exists only if the library started at all — which means the
  // worker was allowed to start, which is the thing this suite is here to say.
  const map = page.getByTestId('route-map');
  const canvas = map.locator('canvas.maplibregl-canvas');
  await expect(canvas).toBeVisible();

  const drawn = await canvas.evaluate((element) => (element as HTMLCanvasElement).width);
  expect(drawn).toBeGreaterThan(0);

  // The codes of the four airports, which are HTML markers and not labels of the base map — the
  // archive is not served here at all, and these are still on screen.
  for (const code of ['LIRF', 'LSZH', 'RJAA', 'KLAX']) {
    await expect(map.getByText(code, { exact: true })).toBeVisible();
  }

  // The attribution the licence of the data asks for, whether or not the data arrived.
  await expect(map.getByRole('link', { name: 'OpenStreetMap' })).toBeVisible();

  // ⚠️ The assertion the whole file is for: not one refusal from the policy. A `worker-src` or an
  // `img-src` that MapLibre needs and this hub does not grant shows up here and nowhere else.
  expect(complaints).toEqual([]);
});
