import { expect, test } from 'vitest';

import type { PersonalTokenWriteDto } from './queries';
import { audienceWordKey, tokenSchema } from './schema';

/** The form schema is a mirror of the write DTO, in both directions (design M0 §7.5). */
const CONTRACT_FIELDS: readonly (keyof PersonalTokenWriteDto)[] = ['name', 'audience', 'days'];

test('the form schema carries exactly the fields of PersonalTokenWriteDto', () => {
  expect(Object.keys(tokenSchema([{ value: 'sample.agent', label: 'Sample' }]).shape).sort()).toEqual(
    [...CONTRACT_FIELDS].sort(),
  );
});

test('the audiences are the ones the bootstrap offers, as a closed choice with their words', () => {
  const offered = [
    { value: 'a.one', label: 'One' },
    { value: 'b.two', label: 'Two' },
  ];
  expect(tokenSchema(offered).shape.audience.meta()).toMatchObject({ choices: offered });
});

test('an audience is worded by its module, under tokenAudiences', () => {
  expect(audienceWordKey('flightops.agent')).toBe('flightops:tokenAudiences.agent');
  expect(audienceWordKey('sample.agent.v2')).toBe('sample:tokenAudiences.agent.v2');
  expect(audienceWordKey('bare')).toBe('bare');
});
