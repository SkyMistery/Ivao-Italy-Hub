import { type Page, expect, test } from '@playwright/test';

import { measureContrast, type Measured } from './contrast';
import { oneTemplate, siteStaffBootstrap, stubTheApi, stubTheApiAsStaff } from './fixtures';

/**
 * The secondary text is readable in the dark theme, measured rather than looked at.
 *
 * ⚠️ Why this exists: Atmosphere flips every foreground for the dark theme — `--foreground` from
 * fuselage-800 to fuselage-100, `--card-foreground` to fuselage-200 — except `--muted-foreground`,
 * which stays fuselage-500 in both. A grey that is dark on white is the same grey on near black,
 * and the staff position codes on `/about` came out at **3.14 : 1** where WCAG AA asks 4.5 : 1 for
 * text that size. The hub overrides that one token in `src/styles/index.css`, and this is what says
 * the override is still there and still winning: it is the only token this project overrides, so it
 * is exactly the sort of line a later change removes by accident.
 *
 * Only a browser can answer it. The value comes from a variable, defined in a stylesheet loaded
 * after Tailwind's utilities, resolved against a background that is often half transparent and
 * painted over something else. The number that matters is the one the browser computed.
 */

/**
 * Where the secondary text of a screen is. Two selectors and not one since 10 September 2026: the
 * footer stopped taking its colours from the theme when it was given a ground of its own -- a token
 * meant for dark-on-light says nothing on a blue band -- so it says what it is with an attribute
 * instead, and this check follows it there rather than losing sight of it.
 */
const SECONDARY_TEXT = '.text-muted-foreground, [data-secondary]';

async function secondaryTextOf(page: Page): Promise<Measured[]> {
  // ⚠️ Asserted and not assumed: without the class this would measure the light theme and pass
  // while proving nothing, which is the failure mode of every test that checks a colour.
  await expect(page.locator('html')).toHaveClass(/dark/);

  // And waited for, for the same reason: `goto` returns when the document loaded, and React draws
  // after that. Measuring an empty screen is a test that says nothing and says it in green — the
  // footer alone carries four of these, so on any screen of this application there is something.
  await page.locator(SECONDARY_TEXT).first().waitFor({ state: 'visible' });

  // The measure itself is shared with every other spec that asks how readable something is: see
  // `./contrast` for why it paints colours instead of reading them.
  const rows = await measureContrast(page, SECONDARY_TEXT);

  expect(rows.length, 'no secondary text on this screen to measure').toBeGreaterThan(0);

  return rows;
}

function readable(where: string, rows: Measured[]) {
  const failing = rows.filter((row) => row.measured < row.needs);
  const said = failing
    .map((row) => `${where}: "${row.text}" at ${row.size}px is ${row.measured}:1, needs ${row.needs}:1`)
    .join('\n');

  expect(failing, said).toEqual([]);
}

/**
 * The headings of the public site, in the **light** theme — the case the check above could not see.
 *
 * ⚠️ Why it is a test of its own. Atmosphere's base rules paint `h2`-`h6` in fuselage-400 while the
 * body of the text is fuselage-800: calculated at ≈ 3.2 : 1 on white, which passes for a large
 * heading and fails AA at `h5` and `h6` — and it was every section heading of the site. The hub
 * overrides it (`src/styles/index.css`, the third and last override), and this is the line that says
 * the override is still there and still winning: it is unlayered on purpose, so a later change that
 * moves it into a layer would lose silently to `@layer base` and put the grey back.
 *
 * The light theme and not the dark one, because in the dark theme their own grey already passes: the
 * failure only exists on a light ground, which is where a visitor reads.
 */
test('the headings of the public site meet AA in the light theme', async ({ page }) => {
  await stubTheApi(page);

  // ⚠️ Inside `main`, and that is not laziness about the frame: the division's name in the bar is an
  // `h1` deliberately forced white on the blue band, so it is neither the colour of the text nor a
  // heading of a page. What this test is about is the reading column.
  const headings = 'main h1, main h2, main h3, main h4, main h5, main h6';

  for (const path of ['/', '/about', '/news']) {
    await page.goto(path);
    await page.locator('main h1, main h2, main h3').first().waitFor({ state: 'visible' });

    const rows = await measureContrast(page, headings);

    // Asserted and not assumed, for the same reason the dark theme's check asserts the class: a
    // screen with no heading measured would pass while proving nothing.
    expect(rows.length, `no heading on ${path} to measure`).toBeGreaterThan(0);
    readable(path, rows);

    // ⚠️ And the rule itself, not only its consequence. These three screens carry `h1`-`h3`, which
    // are large text and would pass at 3 : 1 with Atmosphere's grey still on them — so the ratios
    // above would stay green if the override went away, and the failure would come back on an `h5`
    // nobody has written yet. What cannot go quietly is this: a heading is the colour the body of
    // the text is, which is what `color: var(--foreground)` says and what fuselage-400 was not.
    const drifting = await page.evaluate((selector) => {
      const body = getComputedStyle(document.body).color;
      return [...document.querySelectorAll(selector)]
        .filter((heading) => heading.getClientRects().length > 0)
        .map((heading) => ({
          text: (heading.textContent ?? '').trim().slice(0, 40),
          colour: getComputedStyle(heading).color,
          body,
        }))
        .filter((heading) => heading.colour !== heading.body);
    }, headings);

    expect(drifting, `a heading on ${path} is not the colour of the text`).toEqual([]);
  }
});

test.describe('the dark theme', () => {
  // The browser asks for dark and the application follows: nothing is stored, so the media query
  // decides, exactly as it does for somebody arriving for the first time.
  test.use({ colorScheme: 'dark' });

  test('the secondary text of the public site meets AA', async ({ page }) => {
    await stubTheApi(page);

    for (const path of ['/', '/about', '/news', '/documents', '/calendar']) {
      await page.goto(path);
      readable(path, await secondaryTextOf(page));
    }
  });

  test('the secondary text of the back office meets AA', async ({ page }) => {
    // The half that has the most of it: every list has a description under a title, and every form
    // a hint under a field.
    await stubTheApiAsStaff(page, siteStaffBootstrap);

    for (const path of ['/staff/wd', '/staff/wd/menu', '/staff/wd/menu/3', '/staff/wd/links']) {
      await page.goto(path);
      readable(path, await secondaryTextOf(page));
    }
  });
});

/**
 * The dark grounds a section can stand on — the brand blue, the deep blue and the dark since
 * 11 September 2026, the teal `aurora` since the 12th — and the promise they were added on: that
 * whatever a block draws on them can be read. Each ground is drawn in the dark theme for exactly that
 * reason, and one of them still needed a lighter grey (`.on-brand-ground` in `styles/index.css`):
 * measured at 3.50 : 1 before it. `aurora` is the **dark** stop of its family because of this test's
 * arithmetic: on `aurora-mid` the secondary grey calculates at 2.3 : 1, and no grey light enough to
 * pass would still read as secondary.
 *
 * Measured in the editor, whose page is the same renderer a visitor gets, in the light theme on
 * purpose: these grounds are dark whatever the reader's theme is, so the light one is the case in
 * which a block could still bring dark text with it.
 */
/** A section of a body, as the editor's stubbed row carries it. */
function groundSection(id: string, background: string, blocks: unknown[]) {
  return {
    id,
    key: id,
    title: { en: id, it: id },
    layout: 'stacked',
    background,
    padding: 'md',
    width: 'default',
    mediaId: null,
    required: null,
    locked: null,
    allowedBlocks: null,
    blocks,
    sections: [],
  };
}

/**
 * A heading, a data block whose loading line is the secondary grey the brand ground failed, and a
 * timeline — which carries the smallest text this hub draws on a ground of its own: the number of a
 * step, 12px inside a pill, added here on 12 September 2026 when a screenshot of the new teal ground
 * raised the question and calculating it was not an answer.
 */
function onGround(ground: string) {
  return [
    {
      id: `h-${ground}`,
      type: 'heading',
      version: 1,
      renderMode: null,
      frozen: null,
      column: 0,
      props: { level: 2, text: { en: `On ${ground}`, it: ground } },
    },
    { id: `s-${ground}`, type: 'stats', version: 1, renderMode: null, frozen: null, column: 0, props: {} },
    {
      id: `t-${ground}`,
      type: 'timeline',
      version: 1,
      renderMode: null,
      frozen: null,
      column: 0,
      props: {
        variant: 'timeline',
        items: [{ title: { en: 'A step', it: 'Un passo' }, text: { en: 'Numbered', it: 'Numerato' } }],
      },
    },
  ];
}

/** The editor open on a page whose sections are one per ground, and nothing else. */
async function openOnGrounds(page: Page, grounds: string[]) {
  const row = {
    ...oneTemplate,
    isTemplate: false,
    body: {
      schemaVersion: 1,
      sections: grounds.map((ground) => groundSection(ground, ground, onGround(ground))),
    },
  };

  await stubTheApiAsStaff(page);
  await page.route('**/api/content/*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(row) }),
  );
  await page.route('**/api/content/*/publish-problems', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ errors: {}, localized: {} }),
    }),
  );

  await page.goto('/staff/ed/content/1');
}

test('text on the dark grounds of a section meets AA', async ({ page }) => {
  await openOnGrounds(page, ['brand', 'deep', 'dark', 'aurora']);
  await page.locator('section.dark h2').first().waitFor({ state: 'visible' });

  const rows = await measureContrast(
    page,
    'section.dark h2, section.dark .text-muted-foreground, section.dark .tabular-nums',
  );

  // One heading per ground at least, and the secondary line on the brand blue among what was
  // measured: a check that found nothing on the ground that failed would say nothing.
  expect(rows.filter((row) => row.text.startsWith('On ')).length).toBe(4);
  readable('the dark grounds', rows);
});

/**
 * The `accent` ground, which since 12 September 2026 is a colour and not a fourth grey: `ocean-50` in
 * the light theme. It is measured like the dark ones because the same promise is being made — and this
 * is the ground on which the brand's orange was found to be unreadable **as text**, which is why an
 * accent only ever colours a graphic (`blocks.tsx`, `ACCENT_GLYPH`).
 *
 * Calculated before it was measured: primary text ≈ 14 : 1, secondary ≈ 4.8 : 1.
 */
test('text on the tinted ground of a section meets AA', async ({ page }) => {
  await openOnGrounds(page, ['accent']);

  // The class is the ground, the way `section.dark` is for the dark four: a ground that stopped being
  // this colour would find nothing to measure, and the count below is what turns that into a failure.
  const tinted = 'section.bg-ocean-50';
  await page.locator(`${tinted} h2`).first().waitFor({ state: 'visible' });

  const rows = await measureContrast(page, `${tinted} h2, ${tinted} .text-muted-foreground`);

  expect(rows.filter((row) => row.text.startsWith('On ')).length).toBe(1);
  readable('the tinted ground', rows);
});
