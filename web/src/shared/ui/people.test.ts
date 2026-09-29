import { expect, test } from 'vitest';

import { createTestI18n } from '../../test/harness';

import { isErased, personName } from './people';

/**
 * A person as every page of the hub writes one (note 2026-09-29-la-persona-cancellata-nel-nucleo): one sentence for every
 * module, and the number that took the place of somebody whose data was erased never shown.
 */

const i18n = createTestI18n();

test('a person is their name with the VID, or the VID alone when the hub has no name', () => {
  const t = i18n.getFixedT(null, null);

  expect(personName({ vid: 790001, name: 'Test Trainee' }, t)).toBe('Test Trainee (790001)');
  expect(personName({ vid: 790001, name: null }, t)).toBe('790001');
  expect(personName({ vid: 790001, name: '' }, t)).toBe('790001');
});

test('somebody whose data was erased is a deleted person, in every language, and never the number in their place', () => {
  expect(isErased(-3)).toBe(true);
  expect(isErased(790001)).toBe(false);

  expect(personName({ vid: -3, name: null }, i18n.getFixedT('en', null))).toBe('Deleted person');

  for (const language of ['en', 'it']) {
    const said = personName({ vid: -3, name: null }, i18n.getFixedT(language, null));

    expect(said).not.toBe('people.deleted');
    expect(said).not.toContain('3');
  }
});
