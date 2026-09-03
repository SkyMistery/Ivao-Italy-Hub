import { expect, test } from 'vitest';

import type { LinkWriteDto } from './queries';
import { linkSchema } from './schema';

/**
 * The form schema is a mirror of the write DTO, and this is the mirror's frame (design M0 §7.5).
 *
 * Both directions matter. A field the server added and the form does not have is a field nobody can
 * fill in; a field the form has and the server does not know is a payload the server ignores, which
 * looks like a save that quietly did nothing.
 */

/**
 * Typed against the generated contract, so a renamed field on the server stops this list from
 * compiling before it stops the assertion from passing.
 */
const CONTRACT_FIELDS: readonly (keyof LinkWriteDto)[] = [
  'ownerDepartment',
  'visibility',
  'title',
  'url',
  'description',
  'category',
  'sort',
  'isActive',
  'rowVersion',
];

test('the form schema carries exactly the fields of LinkWriteDto', () => {
  expect(Object.keys(linkSchema.shape).sort()).toEqual([...CONTRACT_FIELDS].sort());
});

test('the title is the translated field the form has to draw with language tabs', () => {
  expect(linkSchema.shape.title.meta()).toMatchObject({ localized: true });
});

test('the row version travels with the payload but is never a field on screen', () => {
  expect(linkSchema.shape.rowVersion.meta()).toMatchObject({ hidden: true });
});
