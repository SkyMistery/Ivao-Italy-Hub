import { expect, test } from 'vitest';

import {
  buttonGroupSchema,
  cardGridSchema,
  ctaSchema,
  heroSchema,
  linkListSchema,
  logoGridSchema,
} from '../../blocks/schemas';

import { namesAddresses, readFields, suggestAddresses, type FieldNode } from './schema';

/**
 * The address of a button, a card or a logo offers the pages of the site and stays open (design of the
 * editor, phase 3, §4.5; answer 5 of Carmine). The addresses are rows, so the schema only says what the
 * field holds and the editor hands the suggestions in, at whatever depth the field sits.
 */

const addresses = [
  { value: '/about', label: 'About', group: 'Web' },
  { value: '/calendar', label: 'Calendar', group: 'Screens' },
];

/** Every field of a schema that is not a list or a group, whatever holds it. */
function leaves(fields: readonly FieldNode[]): FieldNode[] {
  return fields.flatMap((field) =>
    field.kind === 'list' || field.kind === 'object' ? leaves(field.children) : [field],
  );
}

const leaf = (schema: Parameters<typeof readFields>[0], path: string) =>
  leaves(readFields(schema)).find((field) => field.path === path);

test.each([
  ['the retired button, at the top', ctaSchema, 'href'],
  ['the main button of a hero, in an optional object', heroSchema, 'primary.href'],
  ['the second button of a hero', heroSchema, 'secondary.href'],
  ['a button of a group, in a list', buttonGroupSchema, 'buttons.href'],
  ['a card, optional in a list', cardGridSchema, 'cards.href'],
  ['a logo, optional in a list', logoGridSchema, 'items.href'],
])('%s offers the addresses, and takes what it does not offer', (_where, schema, path) => {
  expect(namesAddresses(schema)).toBe(true);

  expect(leaf(suggestAddresses(schema, addresses), path)).toMatchObject({
    kind: 'suggest',
    suggestions: addresses,
    only: false,
  });
});

test('the schema of the registry is left as it was, and what a block stores is read as before', () => {
  suggestAddresses(heroSchema, addresses);

  expect(leaf(heroSchema, 'primary.href')).toMatchObject({ kind: 'text', choices: null });
});

test('a block with no address names none', () => {
  expect(namesAddresses(linkListSchema)).toBe(false);
});

test('every other field keeps what it was, and an optional button stays optional', () => {
  for (const schema of [heroSchema, buttonGroupSchema, cardGridSchema, logoGridSchema]) {
    const shape = (fields: readonly FieldNode[]) =>
      leaves(fields)
        .filter((field) => !field.path.endsWith('href'))
        .map((field) => [field.path, field.kind, field.optional, field.defaultValue]);

    expect(shape(readFields(suggestAddresses(schema, addresses)))).toEqual(shape(readFields(schema)));
  }

  const shaped = suggestAddresses(heroSchema, addresses);
  const primary = readFields(shaped).find((field) => field.path === 'primary');
  expect(primary).toMatchObject({ kind: 'object', optional: true });

  // A hero with no button at all, and a button to somewhere else: both are still what the block takes.
  const bare = { title: { en: 'Hello' }, align: 'left', tone: 'muted', accent: 'brand' };
  expect(shaped.safeParse(bare).success).toBe(true);
  expect(
    shaped.safeParse({ ...bare, primary: { label: { en: 'Forum' }, href: 'https://forum.example' } }).success,
  ).toBe(true);
});
