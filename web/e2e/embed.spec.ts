import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { stubThePublishedPage } from './fixtures';

/**
 * An interactive block in a browser: what the frame does, and what it cannot do
 * (12 September 2026, `decisions/2026-09-12-il-blocco-interattivo.md`).
 *
 * ⚠️ Five things are asserted here and **none of them can be asserted anywhere else**. That the
 * sandbox holds needs a real browser refusing a real access; that the page grows to the height the
 * frame asks for needs layout and a message crossing a boundary; that a dark section reaches inside
 * a document that cannot see it needs both; that a choice works from a keyboard needs a keyboard;
 * and that printing folds the frame away needs print media. jsdom has none of that, and the unit
 * tests beside it can only say which attributes were written.
 *
 * The document the frame loads is **the real shell**, read from the file the server serves, with the
 * same three substitutions `EmbedEndpoints.Compose` makes. What is stubbed is the server, not the
 * thing under test: a suite that invented its own shell would be testing its own invention.
 */

const shell = readFileSync(
  fileURLToPath(new URL('../../src/IvaoHub.Core/Content/EmbedShell.html', import.meta.url)),
  'utf8',
);

/** The animation, in the shape the guidelines teach: `HUB.t` for every word, a real button. */
const SOURCE = `
<figure style="display:flex;flex-direction:column;gap:.5rem">
  <svg viewBox="0 0 200 90" width="100%" aria-labelledby="t">
    <title id="t">Runway 09/27</title>
    <rect x="40" y="60" width="120" height="10" fill="var(--ink-quiet)"></rect>
    <path id="circuit" d="M 50 65 L 150 65 L 165 40 L 50 40 Z" fill="none" stroke="var(--ocean)" stroke-width="2"></path>
  </svg>
  <button type="button" id="side" aria-pressed="true"></button>
  <p id="ink" style="margin:0">…</p>
</figure>
<script>
  var left = true;
  var side = document.getElementById('side');
  var ink = document.getElementById('ink');

  function draw() {
    side.textContent = HUB.t(left
      ? { en: 'Left hand circuit', it: 'Circuito sinistro' }
      : { en: 'Right hand circuit', it: 'Circuito destro' });
    side.setAttribute('aria-pressed', String(left));
    ink.textContent = HUB.dark ? 'dark' : 'light';
    document.getElementById('circuit').setAttribute('transform', left ? '' : 'matrix(1 0 0 -1 0 130)');
  }

  side.addEventListener('click', function () { left = !left; draw(); });
  window.addEventListener('hub:theme', draw);
  draw();
</script>
`;

const title = { en: 'A left hand circuit', it: 'Un circuito sinistro' };
const description = { en: 'What the traffic does, drawn.', it: 'Quello che fa il traffico, disegnato.' };

function section(id: string, background: string, blockId: string) {
  return {
    id,
    layout: 'stacked',
    background,
    padding: 'md',
    width: 'default',
    blocks: [
      {
        id: blockId,
        type: 'interactive',
        version: 1,
        props: { title, description, minHeight: 160 },
        source: SOURCE,
      },
    ],
    sections: [],
  };
}

/** The endpoint, stood in for: the same shell, the same three substitutions, no server. */
async function stubTheFrame(page: Page) {
  await page.route('**/embed/**', (route) => {
    const lang = new URL(route.request().url()).searchParams.get('lang') ?? 'en';

    void route.fulfill({
      status: 200,
      contentType: 'text/html; charset=utf-8',
      body: shell.replace('{{lang}}', lang).replace('{{title}}', 'a-page').replace('{{source}}', SOURCE),
    });
  });
}

test('the animation draws, answers a keyboard, and grows the room it is given', async ({ page }) => {
  await stubTheFrame(page);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_light', 'none', 'b_light')],
  });

  await page.goto('/animated');

  const frame = page.locator('iframe').first();
  await expect(frame).toHaveAttribute('sandbox', 'allow-scripts');

  const inside = page.frameLocator('iframe').first();
  await expect(inside.locator('svg')).toBeVisible();

  // The words come from `HUB.t`, so this is also the proof that the language reached the document.
  const choice = inside.getByRole('button', { name: 'Left hand circuit' });
  await expect(choice).toBeVisible();

  // A choice the guidelines demand be reachable from a keyboard, made from a keyboard.
  await choice.press('Enter');
  await expect(inside.getByRole('button', { name: 'Right hand circuit' })).toBeVisible();

  // It started at 160 — `minHeight` — and the drawing is taller: a page that ignored the frame's
  // message would still be at 160, with the button cut off.
  await expect
    .poll(async () => (await frame.boundingBox())?.height ?? 0, { timeout: 5000 })
    .toBeGreaterThan(170);
});

test('the frame cannot read the page that framed it, and cannot remember anything', async ({ page }) => {
  await stubTheFrame(page);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_light', 'none', 'b_light')],
  });

  await page.goto('/animated');
  await expect(page.frameLocator('iframe').first().locator('svg')).toBeVisible();

  const inside = page.frames()[1]!;

  // ⚠️ The whole design in one assertion. The document is served from our own origin and is still
  // unable to read the page around it, because `sandbox` without `allow-same-origin` puts it in an
  // opaque origin — the browser refuses, rather than the code politely not trying.
  const reach = await inside.evaluate(() => {
    try {
      return String(parent.document.title);
    } catch {
      return 'refused';
    }
  });

  expect(reach, 'the frame could read the page that framed it').toBe('refused');

  // Storage throws in an opaque origin, which is why nothing in a frame can remember a choice.
  const stored = await inside.evaluate(() => {
    try {
      window.localStorage.setItem('x', 'y');
      return 'stored';
    } catch {
      return 'refused';
    }
  });

  expect(stored).toBe('refused');
});

test('a dark section reaches inside a document that cannot see it', async ({ page }) => {
  await stubTheFrame(page);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_dark', 'aurora', 'b_dark')],
  });

  await page.goto('/animated');

  // The frame has no way of knowing it is being drawn on a dark ground: a section can be dark inside
  // a light page, and the document inside cannot see out. The page tells it, and this is the line
  // that says the message arrived — `#ink` is written from `HUB.dark`.
  await expect
    .poll(async () => page.frames()[1]?.locator('#ink').textContent(), { timeout: 5000 })
    .toBe('dark');
});

test('on paper the frame folds away and the prose stays', async ({ page }) => {
  await stubTheFrame(page);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_light', 'none', 'b_light')],
  });

  await page.goto('/animated');
  await expect(page.locator('iframe')).toBeVisible();

  // Carmine, 12 September: "un banner non serve a nulla". A rectangle nobody can press is not worth
  // the paper; what the author wrote about it is prose of the document and stays.
  //
  // ⚠️ Hidden and not gone, and the difference is which of the two mechanisms is doing it. On a
  // **document** the print context takes the frame out of the page altogether, before the paper is
  // drawn; on any other page — and on a print started from the browser's own menu, which fires no
  // event this application hears — it is `print:hidden` that folds it. This test is the second case,
  // and it is the one that used to fail: a page printed from the menu kept its empty rectangle.
  await page.emulateMedia({ media: 'print' });

  await expect(page.locator('iframe')).toBeHidden();
  await expect(page.getByText('What the traffic does, drawn.').first()).toBeVisible();
});
