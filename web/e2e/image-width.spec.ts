import { expect, test, type Locator, type Page } from '@playwright/test';

import { oneTemplate, stubTheApiAsStaff, stubThePublishedPage } from './fixtures';
import { englishCommon } from './locales';

/**
 * The width of a picture in its column, and the picture at full screen (PR 3.10, design of the editor's phase 3, §5.3),
 * in a browser: the share of the column it takes, what a phone does to it, the handle on its edge dragged in the editor
 * as one step of the history, the slider of the panel, an older body's `width` drawn where it was and moved by the
 * first write, and the dialog that a click opens and Esc closes.
 *
 * In a browser because all of it is layout — a percentage of a column, a minimum, a container query, a drag measured in
 * pixels — and jsdom has none.
 */

const editor = englishCommon.content.editor;
const words = englishCommon.blocks.image;

const en = (value: string) => ({ en: value, it: value });

const image = (id: string, props: Record<string, unknown>, align: string | null = null) => ({
  id,
  type: 'image',
  version: 1,
  props: { mediaId: 9, ...props },
  align,
});

const section = (blocks: unknown[]) => ({
  id: 's_pictures',
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks,
  sections: [],
});

/** Where a thing is, once the browser has laid it out. */
async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box, 'the element is not laid out at all').not.toBeNull();
  return box!;
}

/** The share of its column a picture takes, measured: the wrapper of the width against the column it stands in. */
async function shareOf(picture: Locator): Promise<number> {
  return picture.evaluate((element) => {
    const wrapper = element.closest('[data-resizable]')!;
    const column = wrapper.closest('.\\@container')!;
    return (wrapper.getBoundingClientRect().width / column.getBoundingClientRect().width) * 100;
  });
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test.describe('on the public site', () => {
  const SLUG = 'pictures';

  test.beforeEach(async ({ page }) => {
    await stubThePublishedPage(page, SLUG, {
      schemaVersion: 1,
      sections: [
        section([
          image('b_share', { alt: en('A share of the column'), widthPercent: 40 }),
          // Written before PR 3.10: half the column.
          image('b_old', { alt: en('An older picture'), width: 'half' }),
          image('b_middle', { alt: en('In the middle'), widthPercent: 50 }, 'center'),
          image('b_least', { alt: en('The least'), widthPercent: 20, fullscreen: false }),
        ]),
      ],
    });
  });

  test('a picture takes its share of the column, an older one the width it had, never less than 10 rem', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(`/${SLUG}`);

    expect(await shareOf(page.getByRole('img', { name: 'A share of the column' }))).toBeCloseTo(40, 0);
    expect(await shareOf(page.getByRole('img', { name: 'An older picture' }))).toBeCloseTo(50, 0);

    // In the middle: as much air on either side.
    const middle = page.getByRole('img', { name: 'In the middle' });
    const column = await middle.evaluate((element) => {
      const box = element.closest('.\\@container')!.getBoundingClientRect();
      return { left: box.left, right: box.right };
    });
    const picture = await boxOf(middle);
    expect(picture.x - column.left).toBeCloseTo(column.right - (picture.x + picture.width), 0);

    // A fifth of the reading column is more than 10 rem here: a fifth is what it gets.
    expect(await shareOf(page.getByRole('img', { name: 'The least' }))).toBeCloseTo(20, 0);

    // On a tablet a fifth of the column is under 10 rem, and 10 rem is what it gets; the column is still wider than
    // 24 rem, so the share counts.
    await page.setViewportSize({ width: 768, height: 900 });
    const least = page.getByRole('img', { name: 'The least' });
    expect(await shareOf(least)).toBeGreaterThan(20);
    expect((await boxOf(least)).width).toBeCloseTo(160, 0);
    expect(await shareOf(page.getByRole('img', { name: 'A share of the column' }))).toBeCloseTo(40, 0);
  });

  test('on a phone every picture takes the whole column, whatever its share', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 800 });
    await page.goto(`/${SLUG}`);

    for (const name of ['A share of the column', 'An older picture', 'In the middle', 'The least']) {
      expect(await shareOf(page.getByRole('img', { name })), name).toBeCloseTo(100, 0);
    }
  });

  test('a click opens the picture at the size of the window, named by its words, and Esc closes it', async ({
    page,
  }) => {
    // A photograph larger than the window, as an upload is: the eight pixels of the other tests would be shown at
    // their own size, which is what a picture smaller than the window gets (not stretched, and blurred).
    await page.route('**/media/*/**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'image/svg+xml',
        body: '<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="1600"><rect width="2400" height="1600" fill="#0d2c99"/></svg>',
      }),
    );
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(`/${SLUG}`);

    const onThePage = await boxOf(page.getByRole('img', { name: 'A share of the column' }));
    await page
      .getByRole('button', { name: words.enlargeNamed.replace('{{name}}', 'A share of the column') })
      .click();

    const dialog = page.getByRole('dialog', { name: 'A share of the column' });
    await expect(dialog).toBeVisible();
    // Measured once it has arrived: it opens sliding in from the left (Atmosphere's animation).
    await expect.poll(async () => (await boxOf(dialog)).x).toBeGreaterThanOrEqual(0);
    await expect.poll(async () => (await boxOf(dialog)).width, { timeout: 2_000 }).toBeGreaterThan(1000);
    const large = await boxOf(dialog.getByRole('img', { name: 'A share of the column' }));
    expect(large.width).toBeGreaterThan(onThePage.width * 2);
    // As large as the window lets it be, its shape kept, and inside the window with the dialog's margin.
    expect(large.width / large.height).toBeCloseTo(1.5, 1);
    const frame = await boxOf(dialog);
    expect(frame.y).toBeGreaterThanOrEqual(0);
    expect(frame.x + frame.width).toBeLessThanOrEqual(1280);
    expect(frame.y + frame.height).toBeLessThanOrEqual(900);
    expect(large.height).toBeGreaterThan(900 * 0.75);

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();

    // A picture that says it does not open is not a button.
    await expect(
      page.getByRole('button', { name: words.enlargeNamed.replace('{{name}}', 'The least') }),
    ).toHaveCount(0);
  });
});

test.describe('in the editor', () => {
  const aPage = (blocks: unknown[]) => ({
    ...oneTemplate,
    id: 7,
    kind: 'Page',
    slug: 'welcome',
    path: 'welcome',
    ownerDepartment: 'ED',
    visibility: 'Public',
    isTemplate: false,
    publishedAt: null,
    draftFromFile: false,
    title: { en: 'Welcome', it: 'Benvenuti' },
    rowVersion: '2026-11-10T10:00:00',
    body: { schemaVersion: 1, sections: [section(blocks)] },
  });

  interface Written {
    body: { sections: { blocks: { id: string; props: Record<string, unknown> }[] }[] };
  }

  /** The row as the server holds it: a save is answered with what it wrote. */
  async function stubTheRow(page: Page, row: Record<string, unknown>) {
    const writes: Written[] = [];
    let current = row;

    await page.route('**/api/content/7', async (route) => {
      if (route.request().method() === 'PUT') {
        const written = route.request().postDataJSON() as Written;
        writes.push(written);
        current = { ...current, ...written, rowVersion: `2026-11-10T10:00:0${writes.length}` };
      }

      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) });
    });

    return writes;
  }

  const savedProps = (writes: Written[], id: string) =>
    writes
      .at(-1)
      ?.body.sections.flatMap((one) => one.blocks)
      .find((block) => block.id === id)?.props;

  const thePage = (page: Page) => page.getByRole('region', { name: editor.preview });
  const slider = (page: Page) => page.getByRole('slider', { name: words.fields.widthPercent });

  test.beforeEach(async ({ page }) => {
    await stubTheApiAsStaff(page);
    await page.setViewportSize({ width: 1440, height: 900 });
  });

  test('the handle on the edge of a picked picture drags its width, as one step to undo', async ({
    page,
  }) => {
    const writes = await stubTheRow(
      page,
      aPage([image('b_pic', { alt: en('A runway'), widthPercent: 100 })]),
    );
    await page.goto('/staff/content/7');

    const picture = thePage(page).getByRole('img', { name: 'A runway' });
    await picture.click();
    await expect(slider(page)).toHaveAttribute('aria-valuenow', '100');

    // From the right edge to the middle of the column, a few pixels at a time.
    const handle = thePage(page).locator('[data-resize-handle]');
    const grip = await boxOf(handle);
    const room = await picture.evaluate((element) => {
      const box = element.closest('[data-resizable]')!.parentElement!.getBoundingClientRect();
      return { left: box.left, width: box.width };
    });
    await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
    await page.mouse.down();
    await page.mouse.move(room.left + room.width * 0.5, grip.y + grip.height / 2, { steps: 12 });
    // While it is dragged, it says the number.
    await expect(handle).toContainText('50%');
    await page.mouse.up();

    expect(await shareOf(picture)).toBeCloseTo(50, 0);
    await expect(slider(page)).toHaveAttribute('aria-valuenow', '50');
    // The release picked nothing else: the picture's panel is still the one open.
    await expect(handle).toBeVisible();

    // One gesture, one step: a single undo puts the whole column back.
    await page.getByRole('button', { name: editor.undo }).click();
    expect(await shareOf(picture)).toBeCloseTo(100, 0);

    await page.getByRole('button', { name: editor.redo }).click();
    await page.getByRole('button', { name: editor.saveDraft }).click();
    await expect.poll(() => savedProps(writes, 'b_pic')?.widthPercent).toBe(50);
  });

  test('the slider of the panel is the keyboard’s road, five at a key', async ({ page }) => {
    await stubTheRow(page, aPage([image('b_pic', { alt: en('A runway'), widthPercent: 60 })]));
    await page.goto('/staff/content/7');

    const picture = thePage(page).getByRole('img', { name: 'A runway' });
    await picture.click();

    await slider(page).focus();
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');

    await expect(slider(page)).toHaveAttribute('aria-valuenow', '50');
    expect(await shareOf(picture)).toBeCloseTo(50, 0);
  });

  test('an older picture is drawn where it was, and its first write moves the width to the new property', async ({
    page,
  }) => {
    const writes = await stubTheRow(page, aPage([image('b_old', { alt: en('A runway'), width: 'half' })]));
    await page.goto('/staff/content/7');

    const picture = thePage(page).getByRole('img', { name: 'A runway' });
    expect(await shareOf(picture)).toBeCloseTo(50, 0);

    await picture.click();
    await expect(slider(page)).toHaveAttribute('aria-valuenow', '50');

    // The caption in the language its tab opens on, whichever that is.
    await page.locator('input[id^="caption."]:visible').first().fill('At dawn');
    await page.getByRole('button', { name: editor.saveDraft }).click();

    await expect.poll(() => savedProps(writes, 'b_old')?.widthPercent).toBe(50);
    expect(savedProps(writes, 'b_old')).not.toHaveProperty('width');
    expect(await shareOf(picture)).toBeCloseTo(50, 0);
  });

  test('a click on the picture picks it, and opens nothing', async ({ page }) => {
    await stubTheRow(page, aPage([image('b_pic', { alt: en('A runway'), widthPercent: 60 })]));
    await page.goto('/staff/content/7');

    await thePage(page).getByRole('img', { name: 'A runway' }).click();

    await expect(slider(page)).toBeVisible();
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });
});
