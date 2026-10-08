import { z } from 'zod';

import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import { localized, type ChoiceOption } from '../../shared/forms';
import { listSearchSchema } from '../../shared/list';
import { CALENDAR_SCREEN_VIEWS } from '../../shared/ui';

/**
 * The forms of the events (M4: the settings of E2, the event of E3a, its routes and the filters of `/events` of E4, its slots
 * and the table they are loaded from of E5), as zod
 * schemas: types and what is required. The rules — a kind the calendar has, the ranges, an airport the hub knows — are the
 * server's (design M0 §7.5).
 *
 * A kind of event is chosen, never typed: it is a word of the division's calendar, which the bootstrap carries, so this
 * module writes no kind of its own (note 2026-09-29-i-tipi-di-evento) — one of the kinds the presets list, while they list any
 * (note 2026-10-06-i-tipi-che-un-evento-sceglie).
 */

/** What a kind switches on when the staff chooses it for an event (`KindPreset`, design M4 §1.12). */
export interface KindPreset {
  readonly kind: string;
  readonly publicSlots: boolean;
  readonly privateSlots: boolean;
  readonly hasRoster: boolean;
  readonly wholeDivision: boolean;
  readonly inPerson: boolean;
}

/** The settings as the server keeps them (`EventsSettings`, design M4 §1.12): the ones of M4a. */
export interface EventsSettings {
  readonly kindPresets: readonly KindPreset[];
  readonly bookingGapMinutes: number;
  readonly pilotRetentionMonths: number;
  readonly reminderLeadHours: number;
}

/** What the fields of the settings choose from, already in the language on screen. */
export interface SettingsChoices {
  readonly kinds: readonly ChoiceOption[];
}

export function settingsSchema(choices: SettingsChoices) {
  return z.object({
    // A row is a kind and the five switches it presets; the staff changes them on the event.
    kindPresets: z.array(
      z.object({
        kind: z.string().min(1).meta({ choices: choices.kinds }),
        publicSlots: z.boolean(),
        privateSlots: z.boolean(),
        hasRoster: z.boolean(),
        wholeDivision: z.boolean(),
        inPerson: z.boolean(),
      }),
    ),
    bookingGapMinutes: z.number().int(),
    pilotRetentionMonths: z.number().int(),
    reminderLeadHours: z.number().int(),
  });
}

/** The form's values are the settings themselves, the lists copied: what the form sends back is what the server keeps. */
export type SettingsFormValues = z.output<ReturnType<typeof settingsSchema>>;

export function settingsToFormValues(settings: EventsSettings): SettingsFormValues {
  return {
    ...settings,
    kindPresets: settings.kindPresets.map((preset) => ({ ...preset })),
  };
}

/**
 * The kinds an event chooses from (note 2026-10-06-i-tipi-che-un-evento-sceglie, decided): the calendar's kinds that have a row in
 * the presets, in the calendar's order — or every kind of the calendar while the presets have none, so that a new division's form
 * is never empty — and the event's own kind, which it keeps when its row is taken out. The server refuses any other on a new
 * event or a change of kind.
 */
export function eventKinds(
  calendar: readonly ChoiceOption[],
  presets: readonly KindPreset[],
  own?: string,
): ChoiceOption[] {
  if (presets.length === 0) {
    return [...calendar];
  }

  return calendar.filter(
    (choice) => choice.value === own || presets.some((preset) => preset.kind === choice.value),
  );
}

/**
 * The kinds a field chooses from: the ones offered, and the ones already written — on the presets, or on an event — that the
 * calendar no longer offers, so that they can be seen — the server refuses a preset of one on its row, and keeps an event of
 * one as it is — rather than drawn as a select with nothing chosen.
 */
export function kindChoices(
  calendar: readonly ChoiceOption[],
  written: readonly { readonly kind: string }[],
): ChoiceOption[] {
  const gone = [...new Set(written.map((row) => row.kind))]
    .filter((kind) => kind !== '' && !calendar.some((choice) => choice.value === kind))
    .map((kind) => ({ value: kind, label: kind }));

  return [...calendar, ...gone];
}

// ---- the events (E3a) ------------------------------------------------------------------------------

/** The views of the staff's list (`EventViews` on the server, design M4 §7.2), in the order the list offers them. */
export const EVENT_VIEWS = ['drafts', 'upcoming', 'inProgress', 'ended', 'cancelled'] as const;

export type EventView = (typeof EVENT_VIEWS)[number];

/** The list of the events: the five of every list, and the view as a filter — left out, every event. */
export const eventsSearchSchema = listSearchSchema.extend({
  view: z.enum(EVENT_VIEWS).optional(),
});

export type EventsSearch = z.output<typeof eventsSearchSchema>;

/** The tabs of an event's page: its settings, its description, its airports, its routes, its slots (design M4 §7.2). */
export const eventEditorSearchSchema = z.object({
  tab: z.enum(['settings', 'description', 'airports', 'routes', 'slots']).optional(),
});

export type EventEditorTab = NonNullable<z.infer<typeof eventEditorSearchSchema>['tab']>;

/** Who organises an event, as `EventOrganizer` spells it (§1.2). */
export const ORGANIZERS = ['Division', 'Network', 'OtherDivision'] as const;

/** Who reads an event once it is published (§1.2): never one department alone. */
export const EVENT_VISIBILITIES = ['Public', 'Members'] as const;

/** What the fields of an event choose from, already in the language on screen. */
export interface EventChoices {
  readonly kinds: readonly ChoiceOption[];
}

/**
 * The form of an event, mirroring `EventWriteDto` (design M4 §1.2, E3a): the kind first, because choosing it presets the
 * switches below it (§1.12); then who organises it, its words, its window, who reads it, its banner. The switches of the ATC
 * roster and of an event in person enter with their phases (M4b, M4c). Every rule is the server's: nothing here refuses a
 * field the server would take, so the form can follow what is written as it is written.
 */
export function eventSchema(choices: EventChoices) {
  return z.object({
    kind: z.string().meta({ choices: choices.kinds }),
    publicSlots: z.boolean(),
    privateSlots: z.boolean(),
    wholeDivision: z.boolean(),
    organizer: z.enum(ORGANIZERS),
    externalUrl: z.string(),
    title: localized(),
    slug: z.string().meta({ slugFrom: 'title' }),
    summary: localized().meta({ localized: true, multiline: true }),
    startsAtUtc: z.string().optional().meta({ datetime: true }),
    endsAtUtc: z.string().optional().meta({ datetime: true }),
    visibleFromUtc: z.string().optional().meta({ datetime: true }),
    bookingOpensAtUtc: z.string().optional().meta({ datetime: true }),
    visibility: z.enum(EVENT_VISIBILITIES),
    bannerMediaId: z.number().int().optional().meta({ media: true }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type EventFormValues = z.output<ReturnType<typeof eventSchema>>;

/** The switches of the form a kind presets: the three of M4a. */
type PresetSwitches = Pick<EventFormValues, 'publicSlots' | 'privateSlots' | 'wholeDivision'>;

/**
 * What a kind switches on (§1.12): its preset, or nothing at all for a kind the division gave no preset to. Never imposed —
 * the staff changes them on the event.
 */
export function presetSwitches(presets: readonly KindPreset[], kind: string): PresetSwitches {
  const preset = presets.find((candidate) => candidate.kind === kind);

  return {
    publicSlots: preset?.publicSlots ?? false,
    privateSlots: preset?.privateSlots ?? false,
    wholeDivision: preset?.wholeDivision ?? false,
  };
}

/** A new event: no kind chosen yet, organised by the division, read by everybody. */
export function emptyEvent(locales: readonly string[]): EventFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    kind: '',
    publicSlots: false,
    privateSlots: false,
    wholeDivision: false,
    organizer: 'Division',
    externalUrl: '',
    title: blank,
    slug: '',
    summary: blank,
    visibility: 'Public',
    rowVersion: NEW_ROW_VERSION,
  };
}

/** An airport of an event (§1.3): the ICAO, its place, and its capacity in one of the two ways. */
export const airportSchema = z.object({
  eventId: z.number().int().meta({ hidden: true }),
  icao: z.string(),
  ordinal: z.number().int(),
  maxMovementsPerHour: z.number().int().optional(),
  maxArrivalsPerHour: z.number().int().optional(),
  maxDeparturesPerHour: z.number().int().optional(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type AirportFormValues = z.output<typeof airportSchema>;

export function emptyAirport(eventId: number, ordinal: number): AirportFormValues {
  return { eventId, icao: '', ordinal, rowVersion: NEW_ROW_VERSION };
}

/** "Cancel" (§2.3): why, in every language of the division — the page of the event says it until its end. */
export const cancelSchema = z.object({
  note: localized().meta({ localized: true, multiline: true }),
  rowVersion: z.string().meta({ hidden: true }),
});

export type CancelFormValues = z.output<typeof cancelSchema>;

// ---- the routes of an event (E4) ---------------------------------------------------------------------

/**
 * A route of an event (§1.4), which the flight operations write: the two airports, the route to file, and the remarks — empty in
 * every language is none; written in one only is the server's to refuse, because the page shows them to everybody.
 */
export const routeSchema = z.object({
  eventId: z.number().int().meta({ hidden: true }),
  departureIcao: z.string(),
  arrivalIcao: z.string(),
  route: z.string().meta({ multiline: true }),
  remarks: localized().meta({ localized: true, multiline: true }),
  rowVersion: z.string().meta({ hidden: true }),
});

export type RouteFormValues = z.output<typeof routeSchema>;

export function emptyRoute(eventId: number, locales: readonly string[]): RouteFormValues {
  return {
    eventId,
    departureIcao: '',
    arrivalIcao: '',
    route: '',
    remarks: Object.fromEntries(locales.map((locale) => [locale, ''])),
    rowVersion: NEW_ROW_VERSION,
  };
}

// ---- the slots of an event (E5) ----------------------------------------------------------------------

/**
 * The columns of the table of the public slots (design M4 §3.1), as its header names them and in the order the page offers to copy
 * them: the first eight required in the header, the rotation and the place of a leg in it not. `SlotColumns` on the server reads
 * them, and is the one that decides; this is the line the page hands over, so that nobody types it.
 */
export const SLOT_COLUMNS = [
  'callsign',
  'flight_number',
  'aircraft_types',
  'departure_icao',
  'off_block_utc',
  'arrival_icao',
  'on_block_utc',
  'stand',
  'rotation',
  'leg',
] as const;

/** What a load does with the public slots already there (`SlotLoadMode`): they stay, or the free ones make room. */
export const SLOT_LOAD_MODES = ['Add', 'ReplaceFree'] as const;

/** The table, pasted or read from a CSV file, and what to do with the slots already there (§3.1). */
export const slotLoadSchema = z.object({
  text: z.string().meta({ multiline: true }),
  mode: z.enum(SLOT_LOAD_MODES),
});

export type SlotLoadFormValues = z.output<typeof slotLoadSchema>;

/**
 * One public slot, corrected after a load or written alone (§1.5): the cells of a row of the table, the aircraft types as two
 * fields — the main one, and the others written as the table writes them (`A20N/A321`) —, which the server keeps main first, as a
 * cell `A320/A20N` of the table says (note 2026-10-07-gli-slot-sulla-pagina-dell-evento §1). Its airport of the event and its
 * direction are the server's to read off its airports; every rule is the server's too.
 */
export const slotSchema = z.object({
  eventId: z.number().int().meta({ hidden: true }),
  callsign: z.string(),
  flightNumber: z.string(),
  mainAircraftType: z.string(),
  otherAircraftTypes: z.string(),
  departureIcao: z.string(),
  offBlockUtc: z.string().optional().meta({ datetime: true }),
  arrivalIcao: z.string(),
  onBlockUtc: z.string().optional().meta({ datetime: true }),
  stand: z.string(),
  rotationCode: z.string(),
  rotationLeg: z.number().int().optional(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type SlotFormValues = z.output<typeof slotSchema>;

export function emptySlot(eventId: number): SlotFormValues {
  return {
    eventId,
    callsign: '',
    flightNumber: '',
    mainAircraftType: '',
    otherAircraftTypes: '',
    departureIcao: '',
    arrivalIcao: '',
    stand: '',
    rotationCode: '',
    rowVersion: NEW_ROW_VERSION,
  };
}

// ---- the public side (E4) ----------------------------------------------------------------------------

/**
 * The address of `/events` (design M4 §7.1): the kind and the airport the cards are narrowed to, and how the calendar under
 * them is drawn — the view and the day it is drawn around, as the public calendar keeps them. Left out, every event of every
 * kind, the month of today. `catch` on each, as `/calendar` does: an address edited by hand shows the events, not an error.
 */
export const eventsPublicSearchSchema = z.object({
  kind: z.string().optional().catch(undefined),
  airport: z.string().optional().catch(undefined),
  view: z.enum(CALENDAR_SCREEN_VIEWS).optional().catch(undefined),
  on: z.string().optional().catch(undefined),
});

export type EventsPublicSearch = z.output<typeof eventsPublicSearchSchema>;
