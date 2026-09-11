import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

/**
 * The widths that decide a layout under `blocks/` are measured on the page, not on the window
 * (G15, `styles/index.css`). A `md:` here would be a section that lays out as a desktop inside a
 * preview that looks like a phone — the fault that was measured on 11 September 2026 and that no
 * assertion about text can see. jsdom does no layout, so this is a reading of the source: the one
 * `@container` on the renderer, and no window variant anywhere a page is drawn.
 */

const files = ['ContentRenderer.tsx', 'blocks.tsx'] as const;

/** Read from the working directory, which is `web/`: under jsdom `import.meta.url` is not a file. */
const read = (file: string) => readFileSync(resolve(process.cwd(), 'src', 'blocks', file), 'utf8');

/** A window variant: `sm:` and friends, followed by a class — and not the `sm:` of a lookup table. */
const WINDOW_VARIANT = /(?<![@\w-])(sm|md|lg|xl|2xl):[\w[-]/gu;

test('nothing under blocks/ lays out by the width of the window', () => {
  for (const file of files) {
    const offenders = [...read(file).matchAll(WINDOW_VARIANT)].map((match) => match[0]);

    expect(offenders, `${file} asks the window`).toEqual([]);
  }
});

test('the renderer is the container the sections measure themselves against', () => {
  const source = read('ContentRenderer.tsx');

  expect(source).toContain('className="@container flex flex-col"');
  expect(source).toContain('@view-md:grid-cols-2');
});
