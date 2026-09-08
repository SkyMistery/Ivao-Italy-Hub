import { type Page, expect, test } from '@playwright/test';

import { siteStaffBootstrap, stubTheApi, stubTheApiAsStaff } from './fixtures';

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

type Measured = { text: string; size: number; needs: number; measured: number };

async function secondaryTextOf(page: Page): Promise<Measured[]> {
  // ⚠️ Asserted and not assumed: without the class this would measure the light theme and pass
  // while proving nothing, which is the failure mode of every test that checks a colour.
  await expect(page.locator('html')).toHaveClass(/dark/);

  // And waited for, for the same reason: `goto` returns when the document loaded, and React draws
  // after that. Measuring an empty screen is a test that says nothing and says it in green — the
  // footer alone carries four of these, so on any screen of this application there is something.
  await page.locator('.text-muted-foreground').first().waitFor({ state: 'visible' });

  const rows = await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = 1;
    canvas.height = 1;
    const ctx = canvas.getContext('2d', { willReadFrequently: true })!;

    // Any colour — `rgb()`, `oklab()`, `color(...)` — rendered into the bytes the screen shows.
    // Reading the numbers out of the string with a regular expression is what a first version did,
    // and an `oklab()` colour parsed that way comes out near black: a ratio invented, not measured.
    // A tuple and not `number[]`: with `noUncheckedIndexedAccess` an array index is possibly
    // undefined, and three channels are always three.
    type Rgb = [number, number, number];

    const paint = (colour: string, over?: Rgb): Rgb => {
      ctx.clearRect(0, 0, 1, 1);
      if (over) {
        ctx.fillStyle = `rgb(${over.join(',')})`;
        ctx.fillRect(0, 0, 1, 1);
      }
      ctx.fillStyle = colour;
      ctx.fillRect(0, 0, 1, 1);
      const pixel = ctx.getImageData(0, 0, 1, 1).data;
      return [pixel[0] ?? 0, pixel[1] ?? 0, pixel[2] ?? 0];
    };

    const channel = (value: number) => {
      const v = value / 255;
      return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    };
    const luminance = (rgb: Rgb) =>
      0.2126 * channel(rgb[0]) + 0.7152 * channel(rgb[1]) + 0.0722 * channel(rgb[2]);
    const ratio = (a: Rgb, b: Rgb) => {
      const first = luminance(a);
      const second = luminance(b);
      return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
    };

    const opaque = (colour: string) =>
      colour !== '' && colour !== 'rgba(0, 0, 0, 0)' && colour !== 'transparent';

    // The real ground under an element: the chain of ancestors painted from the bottom up, so a
    // half transparent panel counts for what it shows and not for the colour it declares.
    const behind = (element: Element): Rgb => {
      const chain: string[] = [];
      for (let node: Element | null = element; node !== null; node = node.parentElement) {
        const background = getComputedStyle(node).backgroundColor;
        if (opaque(background)) {
          chain.unshift(background);
        }
      }

      let ground = paint(getComputedStyle(document.documentElement).backgroundColor || 'white');
      for (const colour of chain) {
        ground = paint(colour, ground);
      }

      return ground;
    };

    return [...document.querySelectorAll('.text-muted-foreground')]
      .filter((element) => (element.textContent ?? '').trim() !== '' && element.getClientRects().length > 0)
      .map((element) => {
        const style = getComputedStyle(element);
        const size = Number.parseFloat(style.fontSize);
        const bold = Number.parseInt(style.fontWeight, 10) >= 700;
        // WCAG calls text large at 24px, or 18.66px when bold; everything else needs 4.5 : 1.
        const large = size >= 24 || (bold && size >= 18.66);
        const ground = behind(element);

        return {
          text: (element.textContent ?? '').trim().slice(0, 40),
          size,
          needs: large ? 3 : 4.5,
          measured: Math.round(ratio(paint(style.color, ground), ground) * 100) / 100,
        };
      });
  });

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
