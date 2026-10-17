import { z } from 'zod';

import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import { localized, type ChoiceOption } from '../../shared/forms';
import { listSearchSchema } from '../../shared/list';
import { CALENDAR_SCREEN_VIEWS } from '../../shared/ui';

/**
 * The forms of the events (M4: the settings of E2, the event of E3a, its routes and the filters of `/events` of E4, its slots
 * and the table they are loaded from of E5, the flight of a private slot of E7, a position and an availability of E11a, an activity of
 * E17), the numbers of the statistics (E13a), as zod
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

/**
 * The settings as the server keeps them (`EventsSettings`, design M4 §1.12): the ones of M4a, how long an event in person keeps its
 * registrations (E16), the numbers of the ATC roster (E11a) — the least rating as the network's number of a rating of the core's
 * vocabulary, chosen from it, none for any member —, the minutes that make a shift attended (E13a), the weight of the penalty with
 * the limits of whoever books and does not fly (E13b), and the days a report of support may be sent in (E14a).
 */
export interface EventsSettings {
  readonly kindPresets: readonly KindPreset[];
  readonly bookingGapMinutes: number;
  readonly pilotRetentionMonths: number;
  readonly inPersonRetentionMonths: number;
  readonly reminderLeadHours: number;
  readonly shiftMinutes: number;
  readonly minimumAtcRating: number | null;
  readonly maxConsecutiveShifts: number;
  readonly breakShifts: number;
  readonly applicationsCloseDays: number;
  readonly rosterPublishDays: number;
  readonly experienceMonths: number;
  readonly attendanceMinimumMinutes: number;
  readonly reportDays: number;
  readonly noShowWeight: number;
  readonly unflownThreshold: number;
  readonly restrictedWindowHours: number;
  readonly restrictedMaxPerWindow: number;
  readonly restrictedMaxPerEvent: number;
}

/** What the fields of the settings choose from, already in the language on screen: the kinds, and the ATC ratings (E11a). */
export interface SettingsChoices {
  readonly kinds: readonly ChoiceOption[];
  readonly ratings: readonly ChoiceOption[];
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
    inPersonRetentionMonths: z.number().int(),
    reminderLeadHours: z.number().int(),
    // The ATC roster (E11a): the rating chosen among the core's, its number travelling as text in the choice; empty, any member.
    shiftMinutes: z.number().int(),
    minimumAtcRating: z.string().optional().meta({ choices: choices.ratings }),
    maxConsecutiveShifts: z.number().int(),
    breakShifts: z.number().int(),
    applicationsCloseDays: z.number().int(),
    rosterPublishDays: z.number().int(),
    experienceMonths: z.number().int(),
    // After the event (E13a): the minutes on the position within a shift that make it attended.
    attendanceMinimumMinutes: z.number().int(),
    // The reports of support (E14a): the days after an event they may be sent in.
    reportDays: z.number().int(),
    // The registers (E13b): the weight of a no-show, and the limits of whoever booked this many slots and did not fly them.
    noShowWeight: z.number(),
    unflownThreshold: z.number().int(),
    restrictedWindowHours: z.number().int(),
    restrictedMaxPerWindow: z.number().int(),
    restrictedMaxPerEvent: z.number().int(),
  });
}

/** The form's values: the settings themselves, the lists copied and the rating as the text of its choice. */
export type SettingsFormValues = z.output<ReturnType<typeof settingsSchema>>;

export function settingsToFormValues(settings: EventsSettings): SettingsFormValues {
  const { minimumAtcRating, ...rest } = settings;

  return {
    ...rest,
    kindPresets: settings.kindPresets.map((preset) => ({ ...preset })),
    ...(minimumAtcRating === null ? {} : { minimumAtcRating: String(minimumAtcRating) }),
  };
}

/** The settings as the server takes them back: the rating chosen as its number, none for an empty choice. */
export function settingsFromFormValues(values: SettingsFormValues): EventsSettings {
  const { minimumAtcRating, ...rest } = values;

  return {
    ...rest,
    minimumAtcRating:
      minimumAtcRating === undefined || minimumAtcRating === '' ? null : Number(minimumAtcRating),
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

/**
 * The tabs of an event's page: its settings, its description, its airports, its routes, its slots, its bookings, for an event in
 * person its questions, its registrations, its activities and their seats (E17), for an event with a roster its ATC, and for an event over its
 * statistics (E13a) and its reports of support (E14a) (design M4 §7.2).
 */
export const eventEditorSearchSchema = z.object({
  tab: z
    .enum([
      'settings',
      'description',
      'airports',
      'routes',
      'slots',
      'bookings',
      'questions',
      'registrations',
      'activities',
      'seats',
      'atc',
      'stats',
      'reports',
    ])
    .optional(),
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
 * switches below it (§1.12); then who organises it, its words, its window, who reads it, its banner. The ATC roster with the length
 * of its shifts is E11a's — empty, the settings' —; the event in person and its venue are E16's; its own limits of whoever books and does
 * not fly are E13b's — each empty, the settings'. Every rule is the server's: nothing
 * here refuses a field the server would take, so the form can follow what is written as it is written.
 */
export function eventSchema(choices: EventChoices) {
  return z.object({
    kind: z.string().meta({ choices: choices.kinds }),
    publicSlots: z.boolean(),
    privateSlots: z.boolean(),
    hasRoster: z.boolean(),
    shiftMinutes: z.number().int().optional(),
    wholeDivision: z.boolean(),
    inPerson: z.boolean(),
    organizer: z.enum(ORGANIZERS),
    externalUrl: z.string(),
    title: localized(),
    slug: z.string().meta({ slugFrom: 'title' }),
    summary: localized().meta({ localized: true, multiline: true }),
    venue: localized().optional(),
    startsAtUtc: z.string().optional().meta({ datetime: true }),
    endsAtUtc: z.string().optional().meta({ datetime: true }),
    visibleFromUtc: z.string().optional().meta({ datetime: true }),
    bookingOpensAtUtc: z.string().optional().meta({ datetime: true }),
    visibility: z.enum(EVENT_VISIBILITIES),
    bannerMediaId: z.number().int().optional().meta({ media: true }),
    restrictedWindowHours: z.number().int().optional(),
    restrictedMaxPerWindow: z.number().int().optional(),
    restrictedMaxPerEvent: z.number().int().optional(),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type EventFormValues = z.output<ReturnType<typeof eventSchema>>;

/** The switches of the form a kind presets: the three of M4a, the event in person (E16) and the roster (E11a). */
type PresetSwitches = Pick<
  EventFormValues,
  'publicSlots' | 'privateSlots' | 'hasRoster' | 'wholeDivision' | 'inPerson'
>;

/**
 * What a kind switches on (§1.12): its preset, or nothing at all for a kind the division gave no preset to. Never imposed —
 * the staff changes them on the event.
 */
export function presetSwitches(presets: readonly KindPreset[], kind: string): PresetSwitches {
  const preset = presets.find((candidate) => candidate.kind === kind);

  return {
    publicSlots: preset?.publicSlots ?? false,
    privateSlots: preset?.privateSlots ?? false,
    hasRoster: preset?.hasRoster ?? false,
    wholeDivision: preset?.wholeDivision ?? false,
    inPerson: preset?.inPerson ?? false,
  };
}

/** A new event: no kind chosen yet, organised by the division, read by everybody. */
export function emptyEvent(locales: readonly string[]): EventFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    kind: '',
    publicSlots: false,
    privateSlots: false,
    hasRoster: false,
    wholeDivision: false,
    inPerson: false,
    organizer: 'Division',
    externalUrl: '',
    title: blank,
    slug: '',
    summary: blank,
    venue: blank,
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

/**
 * "Duplicate" (§2.3-bis, E8b), mirroring `EventDuplicateRequest`: the start of the new event — every other time moves by as much —,
 * its address, and whether its public slots come too, asked only of an event that takes some. Every rule is the server's: the address
 * one an event may have, and free.
 */
export function duplicateSchema(takesPublicSlots: boolean) {
  return z.object({
    startsAtUtc: z.string().optional().meta({ datetime: true }),
    slug: z.string(),
    publicSlots: z.boolean().meta({ hidden: !takesPublicSlots }),
  });
}

export type DuplicateFormValues = z.output<ReturnType<typeof duplicateSchema>>;

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

/** The two directions of a slot from the airport of the event (design M4 §3.3): the address writes one, or neither. */
export const SLOT_DIRECTIONS = ['departures', 'arrivals'] as const;

/**
 * The address of the page of an event (E6b): what its list of slots is narrowed to (design M4 §3.3) — arrivals or departures, a
 * stretch of hours at the airport of the event, an aircraft type, an airline, a rotation (`SlotFilters`). Left out, every slot. `catch`
 * on each, as `/events` does: an address edited by hand shows the slots, not an error.
 */
export const eventPageSearchSchema = z.object({
  direction: z.enum(SLOT_DIRECTIONS).optional().catch(undefined),
  from: z.string().optional().catch(undefined),
  until: z.string().optional().catch(undefined),
  type: z.string().optional().catch(undefined),
  airline: z.string().optional().catch(undefined),
  rotation: z.string().optional().catch(undefined),
});

export type EventPageSearch = z.output<typeof eventPageSearchSchema>;

// ---- the private slots (E7) --------------------------------------------------------------------------

/** What the fields of the flight of a private slot choose from, already written on screen: slots by their time there. */
export interface PrivateFlightChoices {
  /** The free private slots of the hour opened, the value the slot's id as text, the label its time. */
  readonly slots: readonly ChoiceOption[];
  /** The free private departures from the same airport an arrival may link (§3.4), the same way. */
  readonly departures: readonly ChoiceOption[];
}

/**
 * The flight a pilot flies through a private slot (design M4 §3.4, E7), mirroring `PrivateBookingRequest`: the slot among the free ones
 * of its hour, the callsign, the aircraft type, the other airport and the time there — where an arrival leaves from and when, where a
 * departure lands and when. Every rule is the server's: the types, the airports, the order of the times, the gap to the pilot's other
 * flights.
 */
export function privateFlightSchema(choices: PrivateFlightChoices) {
  return z.object({
    slotId: z.string().meta({ choices: choices.slots }),
    callsign: z.string(),
    aircraftIcao: z.string(),
    otherIcao: z.string(),
    otherTimeUtc: z.string().optional().meta({ datetime: true }),
  });
}

/** The flight of a private arrival with its linked departure (§3.4): the same aircraft, and the departure's own flight. */
export function privatePairSchema(choices: PrivateFlightChoices) {
  return privateFlightSchema(choices).extend({
    departure: z.object({
      slotId: z.string().meta({ choices: choices.departures }),
      callsign: z.string(),
      otherIcao: z.string(),
      otherTimeUtc: z.string().optional().meta({ datetime: true }),
    }),
  });
}

export type PrivateFlightValues = z.output<ReturnType<typeof privateFlightSchema>>;

export type PrivatePairValues = z.output<ReturnType<typeof privatePairSchema>>;

// ---- the bookings (E6b) ------------------------------------------------------------------------------

/**
 * «Take away» (§3.6): why, in the staff's own words — the pilot reads it in the mail. Required by the server; the dialog keeps its
 * confirmation off while it is empty, so the field passes as it is written, an emptied one included.
 */
export const bookingRemovalSchema = z.object({
  reason: z.string().meta({ multiline: true }),
});

export type BookingRemovalValues = z.output<typeof bookingRemovalSchema>;

// ---- the events in person (E16) ----------------------------------------------------------------------

/** What a question asks for (`QuestionType`, design M4 §4-bis.1): a closed list, each checked by the server by its type. */
export const QUESTION_TYPES = ['YesNo', 'Choice', 'MultiChoice', 'Quantity', 'Text'] as const;

/**
 * A question of an event in person (§4-bis.1), mirroring `EventQuestionWriteDto`: its place in the form, its kind of answer, its words
 * in every language, its choices — only for one or several choices; a new one has no key, and the server gives it one —, and whether an
 * answer is required. Every rule is the server's: the languages, how many choices, what an answered question keeps.
 */
export const questionSchema = z.object({
  eventId: z.number().int().meta({ hidden: true }),
  ordinal: z.number().int(),
  type: z.enum(QUESTION_TYPES),
  label: localized(),
  choices: z.array(
    z.object({
      key: z.string().meta({ hidden: true }),
      label: localized(),
    }),
  ),
  required: z.boolean(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type QuestionFormValues = z.output<typeof questionSchema>;

/** A new question of an event: after the others, a yes or no, not required. */
export function emptyQuestion(
  eventId: number,
  ordinal: number,
  locales: readonly string[],
): QuestionFormValues {
  return {
    eventId,
    ordinal,
    type: 'YesNo',
    label: Object.fromEntries(locales.map((locale) => [locale, ''])),
    choices: [],
    required: false,
    rowVersion: NEW_ROW_VERSION,
  };
}

// ---- the parallel activities (E17) -------------------------------------------------------------------

/**
 * A parallel activity of an event in person (§4-bis.1), mirroring `EventActivityWriteDto`: its name and its place in every language — an
 * empty place is the event's venue —, its window, how long a turn lasts and how many seats each has. Every rule is the server's: inside
 * the event, at least one turn, what the seats taken still allow.
 */
export const activitySchema = z.object({
  eventId: z.number().int().meta({ hidden: true }),
  name: localized(),
  place: localized(),
  fromUtc: z.string().optional().meta({ datetime: true }),
  toUtc: z.string().optional().meta({ datetime: true }),
  slotMinutes: z.number().int(),
  seatsPerSlot: z.number().int(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type ActivityFormValues = z.output<typeof activitySchema>;

/** A new activity of an event: the event's window, turns of thirty minutes, one seat each — the staff narrow it to theirs. */
export function emptyActivity(
  eventId: number,
  startsAtUtc: string,
  endsAtUtc: string,
  locales: readonly string[],
): ActivityFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));
  return {
    eventId,
    name: blank,
    place: { ...blank },
    fromUtc: startsAtUtc,
    toUtc: endsAtUtc,
    slotMinutes: 30,
    seatsPerSlot: 1,
    rowVersion: NEW_ROW_VERSION,
  };
}

// ---- the ATC (E11a) ----------------------------------------------------------------------------------

/**
 * A position an event opens (design M4 §4.1), mirroring `EventAtcPositionWriteDto`: the callsign chosen among the positions of the
 * division — a closed list, as the core's directory gives it —, the hours it is open, and a note for the staff. Empty hours are the
 * event's; every rule is the server's: a position of the division, inside the event, once at a time.
 */
export function atcPositionSchema(positions: readonly ChoiceOption[]) {
  return z.object({
    eventId: z.number().int().meta({ hidden: true }),
    callsign: z.string().meta({ choices: positions }),
    fromUtc: z.string().optional().meta({ datetime: true }),
    toUtc: z.string().optional().meta({ datetime: true }),
    note: z.string().meta({ multiline: true }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type AtcPositionFormValues = z.output<ReturnType<typeof atcPositionSchema>>;

/** A new position of an event: no callsign chosen yet, open the whole event. */
export function emptyAtcPosition(eventId: number): AtcPositionFormValues {
  return { eventId, callsign: '', note: '', rowVersion: NEW_ROW_VERSION };
}

/**
 * A controller's availability (design M4 §4.2), mirroring `AvailabilityRequest`: their windows, each from when to when — in UTC, as
 * every hour of an event —, and a note for the staff who make the roster. Every rule is the server's: inside the event, each ending
 * after it starts, none meeting another.
 */
export const availabilitySchema = z.object({
  windows: z.array(
    z.object({
      fromUtc: z.string().optional().meta({ datetime: true }),
      toUtc: z.string().optional().meta({ datetime: true }),
    }),
  ),
  note: z.string().meta({ multiline: true }),
});

export type AvailabilityFormValues = z.output<typeof availabilitySchema>;

/** A new availability: one window, the whole event — a controller narrows it to their hours. */
export function emptyAvailability(startsAtUtc: string, endsAtUtc: string): AvailabilityFormValues {
  return { windows: [{ fromUtc: startsAtUtc, toUtc: endsAtUtc }], note: '' };
}

/**
 * A shift of the roster as the staff correct it (design M4 §4.4, E11b), mirroring `EventAtcShiftWriteDto`: the position — one of the
 * event's, chosen by its callsign —, the hours in UTC, and the controller by VID: anybody who signed in once, available or not. Every rule
 * is the server's; what leaves the rules of the proposal is a warning on the roster, not a refusal.
 */
export function shiftSchema(positions: readonly ChoiceOption[]) {
  return z.object({
    positionId: z.string().meta({ choices: positions }),
    fromUtc: z.string().optional().meta({ datetime: true }),
    toUtc: z.string().optional().meta({ datetime: true }),
    controllerVid: z.number().int().optional(),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type ShiftFormValues = z.output<ReturnType<typeof shiftSchema>>;

/**
 * Handing a shift over (§4.4-bis, E12), mirroring `TransferRequest` and `TransferApproval`: to whom, by VID — none: the controller looks for
 * a substitute, and the staff name one when they approve — and a note. Every rule is the server's.
 */
export const transferSchema = z.object({
  toVid: z.number().int().optional(),
  note: z.string().meta({ multiline: true }),
});

export type TransferFormValues = z.output<typeof transferSchema>;

/** Refusing a request (§4.4-bis), mirroring `TransferRefusal`: why, which whoever asked reads in the mail. */
export const refuseTransferSchema = z.object({
  note: z.string().meta({ multiline: true }),
});

export type RefuseTransferFormValues = z.output<typeof refuseTransferSchema>;

/** Where «Add» on an uncovered piece opens the form: that position and those hours, nobody on it yet. */
export const shiftSearchSchema = z.object({
  positionId: z.coerce.number().int().optional(),
  fromUtc: z.string().optional(),
  toUtc: z.string().optional(),
});

/** A new shift: the piece «Add» was pressed on, when it was, and no controller yet. */
export function emptyShift(search: z.output<typeof shiftSearchSchema>): ShiftFormValues {
  return {
    positionId: search.positionId === undefined ? '' : String(search.positionId),
    ...(search.fromUtc === undefined ? {} : { fromUtc: search.fromUtc }),
    ...(search.toUtc === undefined ? {} : { toUtc: search.toUtc }),
    rowVersion: NEW_ROW_VERSION,
  };
}

/**
 * The numbers of an event as the tab «Statistics» draws them (design M4 §1.10, E13a): a generated form that saves nothing, its words the
 * page's. The bookings still to check are the ones the check has not found flown nor not flown — the sources did not answer. The reports
 * of support and the signals of award join with E14a and E14b.
 */
export const statsSchema = z.object({
  publicSlots: z.number().int(),
  privateSlots: z.number().int(),
  booked: z.number().int(),
  bookedFlown: z.number().int(),
  bookedUnflown: z.number().int(),
  bookedToCheck: z.number().int(),
  // Empty when it was not counted (an event of the whole division, one without airports, one closed before any count): never a 0.
  flownUnbooked: z.number().int().optional(),
  atcAvailabilities: z.number().int(),
  atcShifts: z.number().int(),
  atcAttended: z.number().int(),
  atcNoShows: z.number().int(),
  atcExcused: z.number().int(),
  registrations: z.number().int(),
  activitySeats: z.number().int(),
});

export type StatsFormValues = z.output<typeof statsSchema>;

/** The numbers the server sent, with the bookings still to check worked out from them. */
export function statsToFormValues(stats: {
  readonly publicSlots: number;
  readonly privateSlots: number;
  readonly booked: number;
  readonly bookedFlown: number;
  readonly bookedUnflown: number;
  readonly flownUnbooked: number | null;
  readonly atcAvailabilities: number;
  readonly atcShifts: number;
  readonly atcAttended: number;
  readonly atcNoShows: number;
  readonly atcExcused: number;
  readonly registrations: number;
  readonly activitySeats: number;
}): StatsFormValues {
  return {
    publicSlots: stats.publicSlots,
    privateSlots: stats.privateSlots,
    booked: stats.booked,
    bookedFlown: stats.bookedFlown,
    bookedUnflown: stats.bookedUnflown,
    bookedToCheck: Math.max(0, stats.booked - stats.bookedFlown - stats.bookedUnflown),
    ...(stats.flownUnbooked === null ? {} : { flownUnbooked: stats.flownUnbooked }),
    atcAvailabilities: stats.atcAvailabilities,
    atcShifts: stats.atcShifts,
    atcAttended: stats.atcAttended,
    atcNoShows: stats.atcNoShows,
    atcExcused: stats.atcExcused,
    registrations: stats.registrations,
    activitySeats: stats.activitySeats,
  };
}

/** The outcomes the staff give a shift (design M4 §4.5, E13b), in the order the form offers them. */
export const DECIDED_ATTENDANCES = ['NoShow', 'Excused', 'Attended'] as const;

export type DecidedAttendanceValue = (typeof DECIDED_ATTENDANCES)[number];

/**
 * The outcomes a shift may pass to (note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 1, as Carmine changed it on #283): any of the
 * three but the one it has — no outcome is final; a no-show is offered excused first, the no-show taken away. The server decides again.
 */
export function decidableAttendances(current: string): DecidedAttendanceValue[] {
  return DECIDED_ATTENDANCES.filter((outcome) => outcome !== current);
}

/** Deciding how a shift went, mirroring `AttendanceDecision`: the outcome among those still open, and a note — the server asks one to excuse. */
export function attendanceSchema(allowed: readonly DecidedAttendanceValue[]) {
  return z.object({
    attendance: z.enum(
      allowed.length === 0
        ? DECIDED_ATTENDANCES
        : (allowed as [DecidedAttendanceValue, ...DecidedAttendanceValue[]]),
    ),
    note: z.string().meta({ multiline: true }),
  });
}

export type AttendanceFormValues = z.output<ReturnType<typeof attendanceSchema>>;

/** Taking a booking not flown out of its pilot's record (§3.7), mirroring `UnflownExcuse`: why. */
export const unflownExcuseSchema = z.object({
  note: z.string().meta({ multiline: true }),
});

export type UnflownExcuseFormValues = z.output<typeof unflownExcuseSchema>;

// ---- the reports of support (E14a) -------------------------------------------------------------------------------------------

/**
 * A pilot's report of support (design M4 §5.2), mirroring `ReportRequest`: their flights, each with its callsign, its two airports and the
 * day it flew, in UTC. Every rule is the server's: the airports the hub knows, one of the event's touched, the day within the event.
 */
export const pilotReportSchema = z.object({
  items: z.array(
    z.object({
      callsign: z.string(),
      departureIcao: z.string(),
      arrivalIcao: z.string(),
      flightDay: z.string().optional().meta({ date: true }),
    }),
  ),
});

export type PilotReportFormValues = z.output<typeof pilotReportSchema>;

/**
 * A controller's report of support (§5.2), mirroring `ReportRequest`: their shifts, each with the position's callsign and its hours in UTC.
 * Every rule is the server's: within the event, ending after it starts, none over another.
 */
export const atcReportSchema = z.object({
  items: z.array(
    z.object({
      callsign: z.string(),
      fromUtc: z.string().optional().meta({ datetime: true }),
      toUtc: z.string().optional().meta({ datetime: true }),
    }),
  ),
});

export type AtcReportFormValues = z.output<typeof atcReportSchema>;

/** The midnight UTC of the day an instant falls on, as a date field holds it. */
function dayOf(instant: string): string {
  return `${instant.slice(0, 10)}T00:00:00Z`;
}

/** A new pilot's report: one flight on the day the event starts, nothing else written. */
export function emptyPilotReport(startsAtUtc: string): PilotReportFormValues {
  return { items: [{ callsign: '', departureIcao: '', arrivalIcao: '', flightDay: dayOf(startsAtUtc) }] };
}

/** A new controller's report: one shift, the whole event — they narrow it to their hours. */
export function emptyAtcReport(startsAtUtc: string, endsAtUtc: string): AtcReportFormValues {
  return { items: [{ callsign: '', fromUtc: startsAtUtc, toUtc: endsAtUtc }] };
}

/** A report sent, as its form loads it again to be corrected. */
export function pilotReportValues(items: readonly ReportItemValues[]): PilotReportFormValues {
  return {
    items: items.map((item) => ({
      callsign: item.callsign,
      departureIcao: item.departureIcao ?? '',
      arrivalIcao: item.arrivalIcao ?? '',
      ...(item.flightDay === null ? {} : { flightDay: item.flightDay }),
    })),
  };
}

export function atcReportValues(items: readonly ReportItemValues[]): AtcReportFormValues {
  return {
    items: items.map((item) => ({
      callsign: item.callsign,
      ...(item.fromUtc === null ? {} : { fromUtc: item.fromUtc }),
      ...(item.toUtc === null ? {} : { toUtc: item.toUtc }),
    })),
  };
}

/** What a sent item says, as the server gives it back. */
export interface ReportItemValues {
  readonly callsign: string;
  readonly departureIcao: string | null;
  readonly arrivalIcao: string | null;
  readonly flightDay: string | null;
  readonly fromUtc: string | null;
  readonly toUtc: string | null;
}

/** The two decisions on a report (§5.3), as `ReportState` spells them. */
export const REPORT_DECISIONS = ['Accepted', 'Rejected'] as const;

export type ReportDecisionValue = (typeof REPORT_DECISIONS)[number];

/** The decisions a report may take from where it stands: both while it waits, the other one once decided (choice 1). */
export function decidableReportStates(current: string): ReportDecisionValue[] {
  return REPORT_DECISIONS.filter((state) => state !== current);
}

/** Deciding a report, mirroring `ReportDecision`: accepted or rejected, and a note — the server asks one to reject. */
export function reportDecisionSchema(allowed: readonly ReportDecisionValue[]) {
  return z.object({
    state: z.enum(
      allowed.length === 0 ? REPORT_DECISIONS : (allowed as [ReportDecisionValue, ...ReportDecisionValue[]]),
    ),
    note: z.string().meta({ multiline: true }),
  });
}

export type ReportDecisionFormValues = z.output<ReturnType<typeof reportDecisionSchema>>;
