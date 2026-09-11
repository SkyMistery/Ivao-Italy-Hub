import { expect, test } from 'vitest';
import { z } from 'zod';

import { isBlank, localized } from './schema';

/**
 * "Nothing written yet", decided from the schema: a setting chosen is not something written, a
 * word, a file or an entry is. It is what the editor draws a placeholder for.
 */

const heading = z.object({ level: z.number().int(), text: localized() });
const picture = z.object({
  mediaId: z.number().optional().meta({ media: true }),
  alt: localized().optional(),
  fit: z.enum(['cover', 'contain']),
});
const cards = z.object({
  columns: z.number().int(),
  items: z.array(z.object({ title: localized() })),
});

test('a block with its settings and no words is blank, and one word makes it not', () => {
  expect(isBlank(heading, { level: 2, text: { en: '', it: '' } })).toBe(true);
  expect(isBlank(heading, { level: 2, text: { en: '', it: 'Ciao' } })).toBe(false);
});

test('a file counts as written, a fit chosen does not', () => {
  expect(isBlank(picture, { fit: 'cover' })).toBe(true);
  expect(isBlank(picture, { fit: 'contain', mediaId: 3 })).toBe(false);
});

test('a list with no entries is blank, and an entry is content', () => {
  expect(isBlank(cards, { columns: 3, items: [] })).toBe(true);
  expect(isBlank(cards, { columns: 3, items: [{ title: { en: 'One', it: 'Uno' } }] })).toBe(false);
});
