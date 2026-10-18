import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';
import { readFields } from '../../shared/forms';

import { routeToFormValues } from './api';
import {
  atcPositionSchema,
  availabilitySchema,
  duplicateSchema,
  emptyAtcPosition,
  emptyAvailability,
  emptyRoute,
  eventsPublicSearchSchema,
  kindChoices,
  routeSchema,
  settingsFromFormValues,
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
  inPersonRetentionMonths: 3,
  reminderLeadHours: 24,
  shiftMinutes: 60,
  minimumAtcRating: 4,
  maxConsecutiveShifts: 2,
  breakShifts: 1,
  applicationsCloseDays: 3,
  rosterPublishDays: 2,
  experienceMonths: 12,
  attendanceMinimumMinutes: 30,
  reportDays: 14,
  minimumFlightMinutes: 10,
  noShowWeight: 1.5,
  unflownThreshold: 3,
  restrictedWindowHours: 2,
  restrictedMaxPerWindow: 1,
  restrictedMaxPerEvent: 2,
};

test('the form has a field for every setting, and nothing else', () => {
  const fields = readFields(settingsSchema({ kinds: [], ratings: [] }));

  expect(fields.map((field) => field.path).sort()).toEqual(Object.keys(saved).sort());

  // A row of the presets is a kind and the five switches of an event.
  const presets = fields.find((field) => field.path === 'kindPresets');
  expect(presets?.kind === 'list' ? presets.children.map((child) => child.path).sort() : []).toEqual(
    Object.keys(saved.kindPresets[0]!)
      .map((name) => `kindPresets.${name}`)
      .sort(),
  );
});

test('the settings come back from the form as they went in, the least rating through its choice (E11a)', () => {
  expect(
    settingsFromFormValues(settingsSchema({ kinds: [], ratings: [] }).parse(settingsToFormValues(saved))),
  ).toEqual(saved);
  expect(settingsToFormValues(saved).minimumAtcRating).toBe(String(saved.minimumAtcRating));

  // No least rating: an empty choice, and none again on the way back.
  const anyMember = { ...saved, minimumAtcRating: null };
  expect(settingsToFormValues(anyMember)).not.toHaveProperty('minimumAtcRating');
  expect(settingsFromFormValues({ ...settingsToFormValues(anyMember), minimumAtcRating: '' })).toEqual(
    anyMember,
  );
});

test('the form of a position has a field for everything a position is written with (E11a), and a new one opens the whole event', () => {
  const written: Record<keyof components['schemas']['EventAtcPositionWriteDto'], true> = {
    eventId: true,
    callsign: true,
    fromUtc: true,
    toUtc: true,
    note: true,
    rowVersion: true,
  };

  expect(
    readFields(atcPositionSchema([]))
      .map((field) => field.path)
      .sort(),
  ).toEqual(Object.keys(written).sort());

  const fresh = emptyAtcPosition(7);
  expect(atcPositionSchema([]).parse(fresh)).toEqual(fresh);
  expect(fresh).not.toHaveProperty('fromUtc');
});

test('the form of an availability has the windows and the note of the request (E11a), a new one the whole event', () => {
  const sent: Record<keyof components['schemas']['AvailabilityRequest'], true> = {
    windows: true,
    note: true,
  };
  const window: Record<keyof components['schemas']['AtcWindowDto'], true> = { fromUtc: true, toUtc: true };

  const fields = readFields(availabilitySchema);
  expect(fields.map((field) => field.path).sort()).toEqual(Object.keys(sent).sort());
  const windows = fields.find((field) => field.path === 'windows');
  expect(windows?.kind === 'list' ? windows.children.map((child) => child.path).sort() : []).toEqual(
    Object.keys(window)
      .map((name) => `windows.${name}`)
      .sort(),
  );

  const fresh = emptyAvailability('2026-11-21T17:00:00Z', '2026-11-21T23:00:00Z');
  expect(availabilitySchema.parse(fresh)).toEqual({
    windows: [{ fromUtc: '2026-11-21T17:00:00Z', toUtc: '2026-11-21T23:00:00Z' }],
    note: '',
  });
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

test('the form of «Duplicate» (E8b) has a field for everything the request carries, the public slots asked only of an event that takes some', () => {
  const sent: Record<keyof components['schemas']['EventDuplicateRequest'], true> = {
    startsAtUtc: true,
    slug: true,
    publicSlots: true,
  };

  const shown = (takesPublicSlots: boolean) =>
    readFields(duplicateSchema(takesPublicSlots))
      .filter((field) => field.meta.hidden !== true)
      .map((field) => field.path)
      .sort();

  expect(
    readFields(duplicateSchema(true))
      .map((field) => field.path)
      .sort(),
  ).toEqual(Object.keys(sent).sort());
  expect(shown(true)).toEqual(['publicSlots', 'slug', 'startsAtUtc']);
  expect(shown(false)).toEqual(['slug', 'startsAtUtc']);
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
