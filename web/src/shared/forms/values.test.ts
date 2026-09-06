import { expect, test } from 'vitest';
import { z } from 'zod';

import { blankEntry, blankValues, localized, readFields, writtenValues, type FieldNode } from './schema';

/**
 * What a field holds when it is empty, and what is worth storing when it still is.
 *
 * Both questions arrived with G3, because G3 is the first phase whose schemas have optional
 * translated properties and lists of objects. The second one is not tidiness: publication refuses a
 * page holding a translated value written in one language and not another, and it reads the body
 * without knowing what any block means — so a caption nobody wrote, carried along as
 * `{ en: "", it: "" }`, would stop the page from ever being published.
 */

const LOCALES = ['en', 'it'] as const;

const card = z.object({
  title: localized(),
  text: localized().optional(),
  href: z.string().optional(),
  icon: z.string().optional().meta({ icon: true }),
});

const schema = z.object({
  columns: z
    .number()
    .int()
    .default(3)
    .meta({ choices: [2, 3, 4] }),
  caption: localized().optional(),
  cards: z.array(card),
  primary: z.object({ label: localized(), href: z.string() }).optional(),
});

test('a blank value is read off the schema, kind by kind', () => {
  expect(blankValues(schema, LOCALES)).toEqual({
    // What the schema declares beats what the kind implies.
    columns: 3,
    caption: { en: '', it: '' },
    cards: [],
    primary: { label: { en: '', it: '' }, href: '' },
  });
});

test('a blank entry of a list is every field of one entry, at its own blank value', () => {
  const cards = readFields(schema).find((field) => field.kind === 'list' && field.path === 'cards');
  expect(cards?.kind).toBe('list');

  expect(blankEntry((cards as Extract<FieldNode, { kind: 'list' }>).children, 'cards', LOCALES)).toEqual({
    title: { en: '', it: '' },
    text: { en: '', it: '' },
    href: '',
    icon: undefined,
  });
});

test('an optional translated value nobody wrote is not stored at all', () => {
  const stored = writtenValues(schema, {
    columns: 3,
    caption: { en: '', it: '  ' },
    cards: [],
    primary: { label: { en: '', it: '' }, href: '' },
  });

  expect(stored).toEqual({ columns: 3, cards: [] });
});

test('a translated value written in one language only is kept, so that publication can refuse it', () => {
  // The rule on the server does not soften. Half a translation is a hole a reader would fall into,
  // and it has to reach publication to be named.
  const stored = writtenValues(schema, {
    columns: 3,
    caption: { en: 'Opening hours', it: '' },
    cards: [],
  });

  expect(stored.caption).toEqual({ en: 'Opening hours', it: '' });
});

test('a required field is never dropped, empty or not', () => {
  const stored = writtenValues(schema, {
    columns: 3,
    cards: [{ title: { en: '', it: '' }, text: { en: '', it: '' } }],
  });

  // The title of a card is required: empty, it is a mistake, and an editor should be told about it
  // by name rather than have it quietly disappear.
  expect(stored.cards).toEqual([{ title: { en: '', it: '' } }]);
});

test('the same rule reaches inside the entries of a list', () => {
  const stored = writtenValues(schema, {
    columns: 3,
    cards: [
      { title: { en: 'Fly', it: 'Vola' }, text: { en: '', it: '' }, href: '', icon: 'plane' },
      { title: { en: 'Learn', it: 'Impara' }, text: { en: 'Here', it: 'Qui' } },
    ],
  });

  expect(stored.cards).toEqual([
    { title: { en: 'Fly', it: 'Vola' }, icon: 'plane' },
    { title: { en: 'Learn', it: 'Impara' }, text: { en: 'Here', it: 'Qui' } },
  ]);
});

test('an optional object whose every field is empty goes away whole', () => {
  const stored = writtenValues(schema, {
    columns: 3,
    cards: [],
    primary: { label: { en: '', it: '' }, href: '' },
  });

  expect(stored.primary).toBeUndefined();
});
