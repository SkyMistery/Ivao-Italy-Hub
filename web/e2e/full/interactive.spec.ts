import { expect, test } from '@playwright/test';

import { createContent, deleteContent, publishContent, readInEnglish, signIn } from './bench';

/**
 * An interactive block, end to end: written into a row, published, and read by a visitor who is
 * nobody — in a real browser, against the real server, because every promise this block makes is
 * about what a browser does with it (12 September 2026,
 * `decisions/2026-09-12-il-blocco-interattivo.md`).
 *
 * ⚠️ Four of the five things asserted here **cannot be asserted anywhere else**. That the frame
 * draws at all needs a server to compose the document; that it cannot touch the page around it needs
 * two real origins; that the page grows to the height the frame asks for needs layout; and that a
 * dark section reaches inside the frame needs both. A unit test can only say which attributes were
 * written, and it does.
 */

/**
 * The animation, in the shape the guidelines teach: one figure, `HUB.t` for every word, a choice
 * that is a real button, and no network of any kind. It is a trimmed cousin of the example the
 * guidelines carry — enough to prove the contract, short enough to read in a test.
 */
const SOURCE = `
<figure style="display:flex;flex-direction:column;gap:.5rem">
  <svg viewBox="0 0 200 90" width="100%" aria-labelledby="t">
    <title id="t">Runway 09/27</title>
    <rect x="40" y="60" width="120" height="10" fill="var(--ink-quiet)"></rect>
    <path id="circuit" d="M 50 65 L 150 65 L 165 40 L 50 40 Z" fill="none" stroke="var(--ocean)" stroke-width="2"></path>
    <circle id="dot" cx="50" cy="65" r="4" fill="var(--artifice)"></circle>
  </svg>
  <button type="button" id="side" aria-pressed="true"></button>
  <p id="ink" style="margin:0;color:var(--ink)"></p>
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

  side.addEventListener('click', function () {
    left = !left;
    draw();
  });

  window.addEventListener('hub:theme', draw);
  draw();
</script>
`;

const words = { en: 'A left hand circuit', it: 'Un circuito sinistro' };
const about = { en: 'What the traffic does, drawn.', it: 'Quello che fa il traffico, disegnato.' };

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
        props: { title: words, description: about, minHeight: 160 },
        source: SOURCE,
      },
    ],
    sections: [],
  };
}

test('an animation draws on a published page, and cannot touch the page it is on', async ({ browser }) => {
  const staff = await browser.newContext();
  await signIn(staff);

  const slug = `interactive-${Date.now().toString(36)}`;
  const row = await createContent(staff, {
    slug,
    visibility: 'Public',
    title: { en: 'With an animation', it: "Con un'animazione" },
    body: {
      schemaVersion: 1,
      // The same block twice: once on the page's own ground, once on a dark one. The second is the
      // case a frame cannot see for itself — a section can be dark inside a light page — and the
      // page has to tell it.
      sections: [section('s_light', 'none', 'b_light'), section('s_dark', 'aurora', 'b_dark')],
    },
  });

  await publishContent(staff, row.id);

  const visitor = await browser.newContext();
  await readInEnglish(visitor);
  const page = await visitor.newPage();

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/${slug}`);

  const frames = page.locator('iframe');
  await expect(frames).toHaveCount(2);

  // ---------------------------------------------------------------- it draws, in the right language
  const drawn = page.frameLocator('iframe').first();
  await expect(drawn.locator('svg')).toBeVisible();
  await expect(drawn.getByRole('button', { name: 'Left hand circuit' })).toBeVisible();

  // And it is a choice a keyboard can make, which is what the guidelines demand of one.
  await drawn.getByRole('button', { name: 'Left hand circuit' }).press('Enter');
  await expect(drawn.getByRole('button', { name: 'Right hand circuit' })).toBeVisible();

  // ---------------------------------------------------------------- the sandbox holds
  await expect(frames.first()).toHaveAttribute('sandbox', 'allow-scripts');

  const inside = page.frames()[1]!;
  const reach = await inside.evaluate(() => {
    try {
      // The page that framed this document, from inside it. An opaque origin cannot read it, and
      // "cannot" here is the browser refusing rather than us asking nicely.
      return String(parent.document.title);
    } catch {
      return 'refused';
    }
  });

  expect(reach, 'the frame could read the page that framed it').toBe('refused');

  // Storage throws in an opaque origin too, which is why nothing in a frame may remember anything.
  const stored = await inside.evaluate(() => {
    try {
      window.localStorage.setItem('x', 'y');
      return 'stored';
    } catch {
      return 'refused';
    }
  });

  expect(stored).toBe('refused');

  // ---------------------------------------------------------------- the page grows to fit it
  // The frame measures itself and says so; the page clamps what it hears and uses it. Started at
  // 160 and the drawing is taller, so a page that ignored the message would still be at 160.
  await expect
    .poll(async () => (await frames.first().boundingBox())?.height ?? 0, { timeout: 5000 })
    .toBeGreaterThan(170);

  // ---------------------------------------------------------------- a dark section reaches inside
  const onDark = page.frames()[2]!;
  await expect.poll(async () => onDark.locator('#ink').textContent(), { timeout: 5000 }).toBe('dark');

  // ---------------------------------------------------------------- and on paper it folds away
  await page.emulateMedia({ media: 'print' });
  // Hidden rather than gone: on a page it is `print:hidden` that folds the frame, and the print
  // context takes it out of the document only where that context is mounted — a document (G14).
  await expect(page.locator('iframe').first()).toBeHidden();
  // What stays is the prose, because that is the part of an interactive block worth the paper.
  await expect(page.getByText('What the traffic does, drawn.').first()).toBeVisible();
  await page.emulateMedia({ media: 'screen' });

  await deleteContent(staff, row.id);
  await staff.close();
  await visitor.close();
});
