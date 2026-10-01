import { expect, test } from 'vitest';

import { readFields } from '../../shared/forms';

import { kindChoices, settingsSchema, settingsToFormValues, type EventsSettings } from './schemas';

/**
 * The form of the settings mirrors `EventsSettings` (design M4 §1.12), which the core keeps as the module's own JSON and the
 * contract therefore does not describe: so the two are held together here, field by field and value by value.
 */

const saved: EventsSettings = {
  kindPresets: [
    {
      kind: 'first',
      publicSlots: true,
      privateSlots: false,
      hasRoster: true,
      wholeDivision: false,
      inPerson: false,
    },
    {
      kind: 'second',
      publicSlots: false,
      privateSlots: true,
      hasRoster: false,
      wholeDivision: true,
      inPerson: true,
    },
  ],
  bookingGapMinutes: 10,
  pilotRetentionMonths: 24,
  reminderLeadHours: 24,
};

test('the form has a field for every setting, and nothing else', () => {
  const fields = readFields(settingsSchema({ kinds: [] }));

  expect(fields.map((field) => field.path).sort()).toEqual(Object.keys(saved).sort());

  // A row of the presets is a kind and the five switches of an event.
  const presets = fields.find((field) => field.path === 'kindPresets');
  expect(presets?.kind === 'list' ? presets.children.map((child) => child.path).sort() : []).toEqual(
    Object.keys(saved.kindPresets[0]!)
      .map((name) => `kindPresets.${name}`)
      .sort(),
  );
});

test('the settings come back from the form as they went in', () => {
  expect(settingsSchema({ kinds: [] }).parse(settingsToFormValues(saved))).toEqual(saved);
});

test('a kind the calendar no longer has stays on offer once, so that its row can be seen and taken out', () => {
  const calendar = [{ value: 'first', label: 'First' }];
  const presets = [saved.kindPresets[0]!, saved.kindPresets[1]!, { ...saved.kindPresets[1]!, kind: '' }];

  expect(kindChoices(calendar, [...presets, saved.kindPresets[1]!])).toEqual([
    { value: 'first', label: 'First' },
    { value: 'second', label: 'second' },
  ]);
  expect(kindChoices(calendar, [saved.kindPresets[0]!])).toEqual(calendar);
});
