import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';
import { readFields } from '../../shared/forms';

import { routeToFormValues } from './api';
import {
  emptyRoute,
  eventsPublicSearchSchema,
  kindChoices,
  routeSchema,
  settingsSchema,
  settingsToFormValues,
  type EventsSettings,
} from './schemas';

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

test('the form of a route has a field for everything a route is written with (E4), and a new one starts empty', () => {
  // The fields the server takes, as the contract spells them: the form writes nothing else, and leaves nothing out.
  const written: Record<keyof components['schemas']['EventRouteWriteDto'], true> = {
    eventId: true,
    departureIcao: true,
    arrivalIcao: true,
    route: true,
    remarks: true,
    rowVersion: true,
  };

  expect(
    readFields(routeSchema)
      .map((field) => field.path)
      .sort(),
  ).toEqual(Object.keys(written).sort());

  const fresh = emptyRoute(7, ['en', 'it']);
  expect(routeSchema.parse(fresh)).toEqual(fresh);
  expect(fresh.remarks).toEqual({ en: '', it: '' });
});

test('a route comes back into its form in every language of the division, the remarks it lacks written empty', () => {
  const values = routeToFormValues(
    {
      id: 3,
      eventId: 7,
      ownerDepartment: 'ED',
      departureIcao: 'XEA1',
      arrivalIcao: 'XEA2',
      route: 'DCT POINT DCT',
      remarks: null,
      updatedAt: '2026-10-06T09:00:00.000Z',
      rowVersion: '2026-10-06T09:00:00.000001Z',
    },
    ['en', 'it'],
  );

  expect(values.remarks).toEqual({ en: '', it: '' });
  expect(routeSchema.parse(values)).toEqual(values);
});

test('the address of /events keeps the kind, the airport and the calendar, and drops a view that does not exist', () => {
  expect(
    eventsPublicSearchSchema.parse({ kind: 'rfo', airport: 'XEA1', view: 'week', on: '2026-11-01' }),
  ).toEqual({
    kind: 'rfo',
    airport: 'XEA1',
    view: 'week',
    on: '2026-11-01',
  });
  expect(eventsPublicSearchSchema.parse({ view: 'agenda', airport: 42 })).toEqual({});
});
