import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, stubTheAddressOfAPage, stubTheApiAsStaff, stubThePublishedPage } from './fixtures';

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

// ⚠️ Playwright hides scroll bars by default, and a bar that takes no width is not the fault the height
// tests below are about: it is those 15 px that kept it there.
test.use({ launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] } });

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
async function stubTheFrame(page: Page, source = SOURCE) {
  await page.route('**/embed/**', (route) => {
    const lang = new URL(route.request().url()).searchParams.get('lang') ?? 'en';

    void route.fulfill({
      status: 200,
      contentType: 'text/html; charset=utf-8',
      body: shell.replace('{{lang}}', lang).replace('{{title}}', 'a-page').replace('{{source}}', source),
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

/**
 * An animation that does one of the forbidden things, and whether anybody is told.
 *
 * ⚠️ This is the question Carmine asked on 12 September — «se qualcuno dovesse fare le cose vietate,
 * ce ne accorgeremo?» — and before this the answer was no for two of the three. Size is refused at
 * save with a message on the field; the network and storage are refused **by the browser**, which
 * writes it in the frame's own console and nowhere else. So the frame now says what it was refused,
 * and the page draws that where only the staff sees it.
 */
const NAUGHTY = `
  <p id="ink">tries to fetch</p>
  <script>
    fetch('https://example.org/nothing').catch(function () {});
  </script>
`;

test('an animation that tries to open the network says so, to the staff and to nobody else', async ({
  page,
}) => {
  await stubTheFrame(page, NAUGHTY);

  // The editor, because that is where `staff` is true and where whoever wrote the animation is
  // standing. A visitor is told nothing: it is not their animation to fix.
  await stubTheApiAsStaff(page);
  await page.route('**/api/content/*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...oneTemplate,
        isTemplate: false,
        body: {
          schemaVersion: 1,
          sections: [section('s_light', 'none', 'b_light')],
        },
      }),
    }),
  );
  await page.route('**/api/content/*/publish-problems', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ errors: {}, localized: {} }),
    }),
  );
  await stubTheAddressOfAPage(page);

  await page.goto('/staff/content/1');

  // The browser refuses the request — `default-src 'none'` — and the refusal, which used to die in a
  // console nobody reads, arrives here as a line naming the directive.
  const told = page.getByText(/the browser refused this animation/i);
  await expect(told.first()).toBeVisible({ timeout: 10000 });
  await expect(told.first()).toContainText(/connect-src|default-src/);
});

test('and a visitor reading the same page is told nothing', async ({ page }) => {
  await stubTheFrame(page, NAUGHTY);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_light', 'none', 'b_light')],
  });

  await page.goto('/animated');
  await expect(page.locator('iframe')).toBeVisible();

  // The frame still sends the message — it does not know who is reading — and the page still hears
  // it. What decides is who is being drawn for: a reader came here to read, and an animation that
  // needs fixing is not their business. Waited for rather than asserted instantly, so that "not
  // shown" cannot mean "not drawn yet".
  await page.waitForTimeout(1500);
  await expect(page.getByText(/the browser refused this animation/i)).toHaveCount(0);
});

/**
 * The height (design of the editor, §5.5): the frame is as tall as what it holds, with no scroll bar up
 * to 2000 px and one past them (`decisions/2026-11-05-l-altezza-dell-interattivo.md`).
 *
 * ⚠️ All of these are measured against what the frame says about itself, from inside, because that is
 * the only place the bar can be seen.
 */
async function showInAPage(page: Page, source: string) {
  await stubTheFrame(page, source);
  await stubThePublishedPage(page, 'animated', {
    schemaVersion: 1,
    sections: [section('s_light', 'none', 'b_light')],
  });

  await page.goto('/animated');
  await expect(page.locator('iframe')).toBeVisible();
}

async function expectItFits(page: Page) {
  await expect
    .poll(
      async () =>
        page
          .frames()[1]
          ?.evaluate(() => {
            const root = document.documentElement;
            return {
              bar: window.innerWidth - root.clientWidth,
              spills: root.scrollHeight > root.clientHeight,
            };
          })
          // Asked while the frame's document is still arriving: the poll asks again.
          .catch(() => null),
      { timeout: 5000 },
    )
    .toEqual({ bar: 0, spills: false });
}

async function frameHeight(page: Page): Promise<number> {
  return (await page.locator('iframe').boundingBox())?.height ?? 0;
}

const FILLED = 'background:var(--line)';

const WHAT_A_FRAGMENT_DOES = {
  // The one that was seen, and the guidelines' own example did it: taller than the room it was first
  // given, so a bar comes, so the drawing is narrower and shorter, and the measure agrees with the bar.
  'a drawing scaled by its viewBox': `<svg viewBox="0 0 400 300" width="100%"><rect width="400" height="300" fill="var(--line)"></rect></svg><p>words</p>`,
  'something positioned below the flow': `<div style="height:100px"></div><div style="position:absolute;top:100px;height:500px;width:50px;${FILLED}"></div>`,
  'a negative margin': `<div style="height:600px;margin-bottom:-300px;${FILLED}"></div><p style="margin:0">after</p>`,
  'a body given the height of the frame': `<style>html,body{height:100%}</style><div style="height:700px;${FILLED}"></div>`,
  'a fragment asking for a bar itself': `<style>html{overflow:scroll}</style><div style="height:400px;${FILLED}"></div>`,
};

for (const [what, source] of Object.entries(WHAT_A_FRAGMENT_DOES)) {
  test(`no scroll bar, on a phone, a tablet and a desktop: ${what}`, async ({ page }) => {
    await showInAPage(page, source);

    for (const width of [360, 768, 1280]) {
      await page.setViewportSize({ width, height: 800 });
      await expectItFits(page);
    }
  });
}

test('a fragment shorter than the room it started with gets less room', async ({ page }) => {
  await showInAPage(page, '<p style="margin:0">a short line</p>');

  // ⚠️ `scrollHeight` alone could never say this: it is never less than the frame. From 160, `minHeight`.
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(80);
});

test('the frame comes back down when the page gets wider', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await showInAPage(
    page,
    `<p style="margin:0">${'words that wrap on a phone and not on a desktop '.repeat(12)}</p>`,
  );
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeGreaterThan(300);

  await page.setViewportSize({ width: 1280, height: 800 });
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeLessThan(200);
});

test('what spills out of the flow keeps its room when the page changes width', async ({ page }) => {
  await showInAPage(page, WHAT_A_FRAGMENT_DOES['something positioned below the flow']);
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(600);

  // The measure starts again from the flow, 100, and the spill says so again: two steps, and then quiet.
  await page.setViewportSize({ width: 768, height: 800 });
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(600);
  await page.waitForTimeout(300);
  expect(await frameHeight(page)).toBe(600);
});

test('a panel opened by a choice gets its room, with no box around it changing', async ({ page }) => {
  await showInAPage(
    page,
    `<style>html,body{height:100%}</style>
     <button type="button" id="more">more</button>
     <div id="panel" hidden style="height:900px;${FILLED}"></div>
     <script>
       document.getElementById('more').addEventListener('click', function () {
         document.getElementById('panel').hidden = false;
       });
     </script>`,
  );

  await page.frameLocator('iframe').getByRole('button', { name: 'more' }).click();

  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeGreaterThan(900);
  await expectItFits(page);
});

/**
 * ⚠️ What a choice changes without moving any box and without touching an attribute the shell watches: a
 * rule of the fragment's own decides, and only the click knows. The guidelines ask for `aria-pressed`.
 */
const PANEL_OF_A_RULE = `html,body{height:100%}.panel{display:none;height:900px;${FILLED}}`;

const WHAT_OPENS_A_PANEL = {
  'aria-pressed': `<style>${PANEL_OF_A_RULE}[aria-pressed=true]~.panel{display:block}</style>
     <button type="button" id="more" aria-pressed="false">more</button><div class="panel"></div>
     <script>
       document.getElementById('more').addEventListener('click', function () {
         this.setAttribute('aria-pressed', 'true');
       });
     </script>`,
  'a data attribute': `<style>${PANEL_OF_A_RULE}[data-open]~.panel{display:block}</style>
     <button type="button" id="more">more</button><div class="panel"></div>
     <script>
       document.getElementById('more').addEventListener('click', function () {
         this.setAttribute('data-open', '');
       });
     </script>`,
  ':checked': `<style>${PANEL_OF_A_RULE}:checked~.panel{display:block}</style>
     <input type="checkbox" id="more" aria-label="more"><div class="panel"></div>`,
};

for (const [what, source] of Object.entries(WHAT_OPENS_A_PANEL)) {
  test(`a panel opened through ${what} gets its room`, async ({ page }) => {
    await showInAPage(page, source);

    await page.frameLocator('iframe').locator('#more').click();

    await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeGreaterThan(900);
    await expectItFits(page);
  });
}

/** Every height the frame says, kept by the page: what a block that throbs cannot hide. */
async function keepTheHeights(page: Page) {
  await page.addInitScript(() => {
    const told: number[] = [];
    Object.assign(window, { told });
    window.addEventListener('message', (event: MessageEvent<{ hub?: string; height?: number }>) => {
      if (event.data?.hub === 'embed-height') {
        told.push(event.data.height ?? 0);
      }
    });
  });
}

/** Waits, then waits again: a frame that has settled has said nothing more, and has not moved. */
async function expectItSettles(page: Page) {
  const seen = async () => ({
    said: await page.evaluate(() => (window as unknown as { told: number[] }).told.length),
    frame: await frameHeight(page),
    scrolling: await page.locator('iframe').getAttribute('scrolling'),
  });

  await page.waitForTimeout(1000);
  const settled = await seen();
  await page.waitForTimeout(700);

  expect(await seen()).toEqual(settled);

  return settled;
}

test('a flow that follows the frame, with something spilling out of it, settles', async ({ page }) => {
  await keepTheHeights(page);

  // ⚠️ The guidelines forbid `vh`, and fragments that use it are already saved. The height of the flow
  // changes because the page answered, so it says nothing about what spills.
  await showInAPage(
    page,
    `<div style="height:50vh"></div><div style="position:absolute;top:0;height:500px;width:50px;${FILLED}"></div>`,
  );

  expect((await expectItSettles(page)).frame).toBe(500);

  // And again from a change of width, which does forget what spilled.
  await page.setViewportSize({ width: 768, height: 800 });
  expect((await expectItSettles(page)).frame).toBe(500);
});

test('after a print, even a cancelled one, the frame is still heard', async ({ page }) => {
  await stubTheFrame(
    page,
    `<button type="button" id="more">more</button>
     <div id="panel" hidden style="height:900px;${FILLED}"></div>
     <script>
       document.getElementById('more').addEventListener('click', function () {
         document.getElementById('panel').hidden = false;
       });
     </script>`,
  );
  await stubThePublishedPage(page, 'animated', { schemaVersion: 1, sections: [] });

  // A document, because only a document and a news item print through the context: there the frame is
  // taken out of the page and what comes back is a new one.
  await page.route('**/api/content/public/Document/printed', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 22,
        kind: 'Document',
        slug: 'printed',
        path: 'printed',
        ownerDepartment: 'ED',
        collections: [],
        title: { en: 'A printed document', it: 'Un documento stampato' },
        summary: null,
        seo: null,
        coverMediaId: null,
        fileMediaId: null,
        media: {},
        showFooter: false,
        effectiveOn: null,
        retiredAt: null,
        supersededBySlug: null,
        supersededByTitle: null,
        body: { schemaVersion: 1, sections: [section('s_light', 'none', 'b_light')] },
        schemaVersion: 1,
        version: 1,
        publishedAt: '2026-09-06T10:00:00Z',
      }),
    }),
  );

  await page.goto('/documents/printed');
  await expect(page.frameLocator('iframe').getByRole('button', { name: 'more' })).toBeVisible();

  // `afterprint` fires on a print that was cancelled too.
  await page.evaluate(() => {
    window.dispatchEvent(new Event('beforeprint'));
    window.dispatchEvent(new Event('afterprint'));
  });

  await page.frameLocator('iframe').getByRole('button', { name: 'more' }).click();

  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeGreaterThan(900);
  await expectItFits(page);
});

/** A wheel over the frame, and how far it went: what a reader does, with a bar of any kind. */
async function wheeled(page: Page): Promise<number> {
  const box = await page.locator('iframe').boundingBox();
  await page.mouse.move((box?.x ?? 0) + 100, Math.min((box?.y ?? 0) + 200, 600));
  await page.mouse.wheel(0, 150);
  await page.waitForTimeout(300);

  return (await page.frames()[1]?.evaluate(() => window.scrollY)) ?? 0;
}

test('past 2000 px the frame stops growing and a scroll bar reaches the rest', async ({ page }) => {
  await showInAPage(page, `<div style="height:2600px;${FILLED}"></div>`);

  // Carmine, 5 November 2026: «(c) resta la barra». What a frame cannot show, it still lets you reach.
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(2000);
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'auto');
  expect(await wheeled(page)).toBeGreaterThan(0);
});

test('up to 2000 px a wheel over the frame moves the page, not the frame', async ({ page }) => {
  await showInAPage(page, `<div style="height:1900px;${FILLED}"></div>`);

  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(1900);
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'no');
  expect(await wheeled(page)).toBe(0);
});

test('and the bar goes when the fragment comes back under the limit', async ({ page }) => {
  await showInAPage(
    page,
    `<button type="button" id="less">less</button>
     <div id="panel" style="height:2600px;${FILLED}"></div>
     <script>
       document.getElementById('less').addEventListener('click', function () {
         document.getElementById('panel').hidden = true;
       });
     </script>`,
  );
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'auto');

  await page.frameLocator('iframe').getByRole('button', { name: 'less' }).click();

  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'no');
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBeLessThan(200);
  await expectItFits(page);
});

/** A drawing exactly this tall at the width it is loaded with, without a bar. */
function aDrawingOf(tall: number, style = '') {
  return `<style>${style}</style>
    <svg id="drawing" width="100%"><rect width="100%" height="100%" fill="var(--line)"></rect></svg>
    <script>
      document.getElementById('drawing').setAttribute('viewBox', '0 0 ' + window.innerWidth + ' ${tall}');
    </script>`;
}

/**
 * ⚠️ Around the limit a bar changes the answer: it takes width, the drawing is shorter, and if that
 * took the bar away the width would be back, and the drawing too tall again, and so on for ever.
 */
for (const tall of [1975, 1990, 2000, 2001, 2010, 2030, 2060]) {
  test(`a drawing of ${tall} px scaled by its viewBox settles, and does not go back and forth`, async ({
    page,
  }) => {
    await keepTheHeights(page);
    await showInAPage(page, aDrawingOf(tall));

    const settled = await expectItSettles(page);

    expect(settled.frame).toBeLessThanOrEqual(2000);
    expect(settled.frame).toBeGreaterThan(1900);
    expect(settled.scrolling).toBe(tall > 2000 ? 'auto' : 'no');
  });
}

test('a bar wider than any browser draws one, which a fragment can ask for, settles too', async ({
  page,
}) => {
  await keepTheHeights(page);
  await showInAPage(page, aDrawingOf(2005, '::-webkit-scrollbar{width:60px}'));

  expect((await expectItSettles(page)).scrolling).toBe('auto');
});

test('something out of the flow and scaled by its viewBox settles around the limit too', async ({ page }) => {
  await keepTheHeights(page);

  // ⚠️ The bar takes width, the root's width changes, what spilled is forgotten: with the bar it says the
  // flow, 100, and without it 2005. No estimate of a bar's width would tell those two apart.
  await showInAPage(
    page,
    `<div style="height:100px"></div>
     <svg id="drawing" style="position:absolute;top:0;left:0;width:100%"><rect width="100%" height="100%"></rect></svg>
     <script>
       document.getElementById('drawing').setAttribute('viewBox', '0 0 ' + window.innerWidth + ' 2005');
     </script>`,
  );

  expect((await expectItSettles(page)).scrolling).toBe('auto');
});

test('a change of width takes a drawing across the limit, and the bar with it', async ({ page }) => {
  await page.setViewportSize({ width: 1000, height: 800 });
  // Tall, so that the page has its own bar as it will below: the width of the frame depends on that too.
  await showInAPage(page, '<div style="height:1500px"></div>');
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(1500);
  const narrow = (await page.locator('iframe').boundingBox())?.width ?? 0;

  // Without a bar this drawing fits at the narrow width, and only just: with a bar's width less, what it
  // says is lower still. Reached from above, it is the case that would keep a bar if it trusted a sum.
  await page.setViewportSize({ width: 1280, height: 800 });
  await showInAPage(
    page,
    `<svg viewBox="0 0 ${narrow} 1995" width="100%"><rect width="100%" height="100%" fill="var(--line)"></rect></svg>`,
  );
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'auto');

  await page.setViewportSize({ width: 1000, height: 800 });
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'no');
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(1995);
  await expectItFits(page);

  await page.setViewportSize({ width: 1280, height: 800 });
  await expect(page.locator('iframe')).toHaveAttribute('scrolling', 'auto');
  await expect.poll(() => frameHeight(page), { timeout: 5000 }).toBe(2000);
});
