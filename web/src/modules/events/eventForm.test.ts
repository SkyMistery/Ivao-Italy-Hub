import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';
import { readFields } from '../../shared/forms';

import { emptyEvent, eventSchema, kindChoices, presetSwitches, type KindPreset } from './schemas';

/**
 * The form of an event (M4, E3a) mirrors `EventWriteDto`, and follows a change of kind: the switches a kind presets are set when
 * the kind is chosen (design M4 §1.12). It can follow only what passes its schema, so a form just opened — nothing written yet —
 * has to pass it: every rule is the server's.
 */

const presets: readonly KindPreset[] = [
  {
    kind: 'first',
    publicSlots: true,
    privateSlots: true,
    hasRoster: true,
    wholeDivision: false,
    inPerson: false,
  },
  {
    kind: 'second',
    publicSlots: false,
    privateSlots: false,
    hasRoster: false,
    wholeDivision: true,
    inPerson: true,
  },
];

test('the form has a field for everything a client writes on an event but the description, which has a tab of its own', () => {
  const written: components['schemas']['EventWriteDto'] = {
    kind: 'first',
    publicSlots: true,
    privateSlots: false,
    wholeDivision: false,
    organizer: 'Division',
    externalUrl: null,
    title: { en: 'An event' },
    slug: 'an-event',
    summary: { en: 'What it is' },
    body: null,
    bannerMediaId: null,
    visibleFromUtc: null,
    bookingOpensAtUtc: null,
    startsAtUtc: '2026-11-21T17:00:00Z',
    endsAtUtc: '2026-11-21T22:00:00Z',
    visibility: 'Public',
    rowVersion: '0001-01-01T00:00:00',
  };

  expect(
    readFields(eventSchema({ kinds: [] }))
      .map((field) => field.path)
      .sort(),
  ).toEqual(
    Object.keys(written)
      .filter((key) => key !== 'body')
      .sort(),
  );
});

test('a form just opened passes its schema, so it follows the kind from the first choice', () => {
  const schema = eventSchema({ kinds: [{ value: 'first', label: 'First' }] });

  expect(schema.safeParse(emptyEvent(['it', 'en'])).success).toBe(true);
  expect(schema.safeParse({ ...emptyEvent(['it', 'en']), kind: 'first' }).success).toBe(true);
});

test('a kind presets the switches of the form, and a kind without a preset switches them all off', () => {
  expect(presetSwitches(presets, 'first')).toEqual({
    publicSlots: true,
    privateSlots: true,
    wholeDivision: false,
  });
  expect(presetSwitches(presets, 'second')).toEqual({
    publicSlots: false,
    privateSlots: false,
    wholeDivision: true,
  });
  expect(presetSwitches(presets, 'third')).toEqual({
    publicSlots: false,
    privateSlots: false,
    wholeDivision: false,
  });
  expect(presetSwitches([], 'first')).toEqual({
    publicSlots: false,
    privateSlots: false,
    wholeDivision: false,
  });
});

test('the kind of an event the calendar no longer offers stays on offer, so the event is drawn with it', () => {
  const calendar = [{ value: 'first', label: 'First' }];

  expect(kindChoices(calendar, [{ kind: 'retired' }])).toEqual([
    ...calendar,
    { value: 'retired', label: 'retired' },
  ]);
  expect(kindChoices(calendar, [{ kind: 'first' }])).toEqual(calendar);
});
