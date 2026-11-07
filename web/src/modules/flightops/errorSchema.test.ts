import { expect, test } from 'vitest';

import { readFields } from '../../shared/forms';

import { tourErrorSchema } from './schemas';

/** The catalogue draws both with `MarkdownContent` (`blocks/errorCatalog.tsx`), so both have the bar (design of the editor, §4.6). */
test.each(['description', 'examples'])('the %s of an error is drawn as Markdown, and has the bar', (path) => {
  const field = readFields(tourErrorSchema).find((candidate) => candidate.path === path);

  expect(field?.meta.markdown).toBe(true);
});
