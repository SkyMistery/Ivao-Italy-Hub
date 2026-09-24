import { expect, test } from 'vitest';

import type { PersonalTokenWriteDto } from './queries';
import { tokenSchema } from './schema';

/** The form schema is a mirror of the write DTO, in both directions (design M0 §7.5). */
const CONTRACT_FIELDS: readonly (keyof PersonalTokenWriteDto)[] = ['name', 'audience', 'days'];

test('the form schema carries exactly the fields of PersonalTokenWriteDto', () => {
  expect(Object.keys(tokenSchema(['sample.agent']).shape).sort()).toEqual([...CONTRACT_FIELDS].sort());
});

test('the audiences are the ones the bootstrap offers, as a closed choice', () => {
  expect(tokenSchema(['a.one', 'b.two']).shape.audience.meta()).toMatchObject({
    choices: ['a.one', 'b.two'],
  });
});
