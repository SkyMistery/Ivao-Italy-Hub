import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized, type ChoiceOption, type Suggestion } from '../../shared/forms';
import { listSearchSchema } from '../../shared/list';

/**
 * The forms of the tours' skeleton (M2, T5), as zod schemas. Types and what is required; the rules — a
 * type IVAO knows, one profile per type, the ranges of the settings — are the server's (design M0 §7.5).
 *
 * A type is a **closed** suggestion: what is offered is asked of the server as it is typed
 * (`/api/reference/aircraft-types`), and a code nobody offered is put back. A list of types is a list of
 * objects, because that is what the generator repeats.
 */

export function aircraftProfileSchema(types: readonly Suggestion[] = []) {
  return z.object({
    ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
    icaoType: z.string().meta({ suggestions: types, suggestionsOnly: true }),
    cruiseTasKt: z.number().int(),
    note: z.string().meta({ multiline: true }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type AircraftProfileFormValues = z.output<ReturnType<typeof aircraftProfileSchema>>;

export function aircraftGroupSchema(types: readonly Suggestion[] = []) {
  return z.object({
    ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
    name: localized(),
    icaoTypes: z.array(z.object({ icao: z.string().meta({ suggestions: types, suggestionsOnly: true }) })),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type AircraftGroupFormValues = z.output<ReturnType<typeof aircraftGroupSchema>>;

/** The settings as the server keeps them (`FlightOpsSettings`, design M2 §1.11). */
export interface FlightOpsSettings {
  readonly dailyLegLimit: number | null;
  readonly defaultReportWindowDays: number;
  readonly disputeWindowDays: number;
  readonly rejectGraceHours: number;
  readonly leaseMinutes: number;
  readonly durationFactor: number;
  readonly durationFixedMinutes: number;
  readonly northSouthLevelCountries: readonly string[];
  readonly trackRetentionDays: number;
  readonly retentionMonths: number;
  readonly retentionMonthsLong: number;
  readonly thresholdToleranceMeters: number;
}

export const settingsSchema = z.object({
  // Empty switches the daily limit off (§3.7).
  dailyLegLimit: z.number().int().optional(),
  defaultReportWindowDays: z.number().int(),
  disputeWindowDays: z.number().int(),
  rejectGraceHours: z.number().int(),
  leaseMinutes: z.number().int(),
  durationFactor: z.number(),
  durationFixedMinutes: z.number().int(),
  northSouthLevelCountries: z.array(z.object({ code: z.string() })),
  trackRetentionDays: z.number().int(),
  retentionMonths: z.number().int(),
  retentionMonthsLong: z.number().int(),
  thresholdToleranceMeters: z.number().int(),
});

export type SettingsFormValues = z.output<typeof settingsSchema>;

export function settingsToFormValues(settings: FlightOpsSettings): SettingsFormValues {
  const { dailyLegLimit, northSouthLevelCountries, ...rest } = settings;

  return {
    ...rest,
    ...(dailyLegLimit === null ? {} : { dailyLegLimit }),
    northSouthLevelCountries: northSouthLevelCountries.map((code) => ({ code })),
  };
}

export function settingsFromFormValues(values: SettingsFormValues): FlightOpsSettings {
  return {
    ...values,
    dailyLegLimit: values.dailyLegLimit ?? null,
    northSouthLevelCountries: values.northSouthLevelCountries
      .map((entry) => entry.code.trim().toUpperCase())
      .filter((code) => code !== ''),
  };
}

// ---- tours ---------------------------------------------------------------------------------------

/** The kinds of a tour (design M2 §2), as `TourKind` spells them. */
export const TOUR_KINDS = [
  'Sequential',
  'Free',
  'Hub',
  'SequentialChosenStart',
  'Distance',
  'Open',
  'Container',
] as const;

/** The kinds a subtour may have: any but a container, one level only (design M2 §2.7). */
export const SUBTOUR_KINDS = [
  'Sequential',
  'Free',
  'Hub',
  'SequentialChosenStart',
  'Distance',
  'Open',
] as const satisfies readonly Exclude<(typeof TOUR_KINDS)[number], 'Container'>[];

/**
 * The form of a tour, mirroring `TourWriteDto` (design M2 §1.2, §8.3). A function, because what it offers is known
 * only at runtime — the types as they are typed, the awards of the division — and because three things are decided
 * before it opens: a template has no address, no dates and no award (§1.10), the kind of a tour the public already
 * sees no longer changes (§1.2.1), and a subtour is never a container, has no award, and may leave its dates empty to
 * take its container's (§2.7, note 2026-09-21-la-forma-dei-tour). Those fields are carried and not drawn. Every rule
 * is the server's.
 */
export function tourSchema({
  types = [],
  awards = [],
  groups = [],
  isTemplate = false,
  kindLocked = false,
  isSubtour = false,
}: {
  types?: readonly Suggestion[];
  awards?: readonly ChoiceOption[];
  groups?: readonly ChoiceOption[];
  isTemplate?: boolean;
  kindLocked?: boolean;
  isSubtour?: boolean;
} = {}) {
  return z.object({
    ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
    isTemplate: z.boolean().meta({ hidden: true }),
    // Chosen when a subtour is created, and never changed.
    parentTourId: z.number().int().optional().meta({ hidden: true }),
    // A subtour offers every kind but a container; what the form holds is a kind either way.
    kind: (
      (isSubtour ? z.enum(SUBTOUR_KINDS) : z.enum(TOUR_KINDS)) as z.ZodType<
        (typeof TOUR_KINDS)[number],
        (typeof TOUR_KINDS)[number]
      >
    ).meta({ hidden: kindLocked }),
    title: localized(),
    slug: z.string().meta({ slugFrom: 'title', hidden: isTemplate }),
    summary: localized().meta({ localized: true, multiline: true }),
    releaseAt: z.string().optional().meta({ datetime: true, hidden: isTemplate }),
    closeAt: z.string().optional().meta({ datetime: true, hidden: isTemplate }),
    showPreview: z.boolean().meta({ hidden: isTemplate }),
    coverMediaId: z.number().int().optional().meta({ media: true }),
    bannerMediaId: z.number().int().optional().meta({ media: true }),
    progression: z.enum(['FlyAhead', 'WaitForValidation']),
    // Read only on a hub tour; the server leaves it empty on the others.
    hubRotationOrder: z.enum(['Fixed', 'Free']).optional(),
    reportWindowDays: z.number().int().optional(),
    dailyLegLimit: z.number().int().optional(),
    requiresProcedures: z.boolean(),
    minPilotRating: z.number().int().optional(),
    referenceAircraftIcao: z.string().meta({ suggestions: types, suggestionsOnly: true }),
    // Read only on a distance tour, like the order of the rotations on a hub tour (T7a).
    requiredNm: z.number().int().optional(),
    // Read only on a container: how many of its subtours complete it (T7b).
    requiredSubtours: z.number().int().optional().meta({ hidden: isSubtour }),
    // The aircraft admitted: types and groups, nothing else (design M2 §1.5); both empty admit all. The types carry the
    // payload's name, so that a refusal of the server — which files it under `allowedAircraft` — lands on a field.
    allowedAircraft: z.array(
      z.object({ icao: z.string().meta({ suggestions: types, suggestionsOnly: true }) }),
    ),
    allowedGroups: z.array(z.object({ groupId: z.string().optional().meta({ choices: groups }) })),
    awardId: z
      .string()
      .optional()
      .meta({ choices: awards, hidden: isTemplate || isSubtour }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type TourFormValues = z.output<ReturnType<typeof tourSchema>>;

/**
 * The address a tour's own screen is opened with: `?template=true` makes a new one a template, `?parent=` a subtour of
 * that container, `?tab=` says which tab is open (T6b: settings and briefing; T7a the legs; T7b the hubs, the subtours
 * and the callsigns; T9 adds its own).
 */
export const tourEditorSearchSchema = z.object({
  template: z.boolean().optional(),
  parent: z.number().int().optional(),
  tab: z.enum(['settings', 'briefing', 'legs', 'hubs', 'subtours', 'callsigns', 'open', 'rules']).optional(),
});

export type TourEditorTab = NonNullable<z.infer<typeof tourEditorSearchSchema>['tab']>;

/** "New from a template": which one, and the name and address of the tour it makes. */
export function tourFromTemplateSchema(templates: readonly ChoiceOption[] = []) {
  return z.object({
    templateId: z.string().meta({ choices: templates }),
    title: localized(),
    slug: z.string().meta({ slugFrom: 'title' }),
  });
}

export type TourFromTemplateFormValues = z.output<ReturnType<typeof tourFromTemplateSchema>>;

/** "Save as template": the name of the template the tour's settings become. */
export const tourSaveAsTemplateSchema = z.object({ title: localized() });

export type TourSaveAsTemplateFormValues = z.output<typeof tourSaveAsTemplateSchema>;

// ---- the shape of a tour (T7b) -------------------------------------------------------------------

/** A hub of a hub tour (design M2 §1.3): an airport, once per tour, in an order. */
export const hubSchema = z.object({
  tourId: z.number().int().meta({ hidden: true }),
  icao: z.string(),
  sort: z.number().int(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type HubFormValues = z.output<typeof hubSchema>;

/** The sizes a rotation may have (design M2 §1.3). */
export const ROTATION_SIZES = ['2', '4', '6'] as const;

/** A rotation: which hub, in which order inside it, and how many legs. */
export function rotationSchema(hubs: readonly ChoiceOption[] = []) {
  return z.object({
    tourId: z.number().int().meta({ hidden: true }),
    hubId: z.string().meta({ choices: hubs }),
    sort: z.number().int(),
    size: z.enum(ROTATION_SIZES),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type RotationFormValues = z.output<ReturnType<typeof rotationSchema>>;

/**
 * A callsign constraint (design M2 §1.6, note 2026-09-21-la-forma-dei-tour): allow or deny an airline — three letters, the
 * rest being the pilot's choice — or deny one whole callsign; on the tour, or on one of its legs.
 */
export function callsignRuleSchema(legs: readonly ChoiceOption[] = []) {
  return z.object({
    tourId: z.number().int().meta({ hidden: true }),
    mode: z.enum(['Allow', 'Deny']),
    match: z.enum(['Airline', 'Exact']),
    value: z.string(),
    // A template has no legs: its constraints are the tour's.
    legId: z
      .string()
      .optional()
      .meta({ choices: legs, hidden: legs.length === 0 }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type CallsignRuleFormValues = z.output<ReturnType<typeof callsignRuleSchema>>;

// ---- the Open tour: its goal, its filters and sequence rules (T7c) ------------------------------------

/** The goals of an Open tour (design M2 §2.6.1), as `OpenGoal` spells them. */
export const OPEN_GOALS = [
  'Distance',
  'FlightCount',
  'DistinctAirports',
  'DistinctCountries',
  'CollectList',
  'CollectRegions',
] as const;

export type OpenGoalKind = (typeof OPEN_GOALS)[number];

/** The filters and sequence rules of an Open tour, as `TourConstraintKind` spells them. */
export const TOUR_CONSTRAINT_KINDS = [
  'DepartureOrArrivalIn',
  'DepartureIn',
  'ArrivalIn',
  'TouchesAirport',
  'DistanceBetween',
  'AircraftCategory',
  'ArrivalRunwayMax',
  'ArrivalElevationMin',
  'FlightRules',
  'Chained',
  'Eastbound',
  'Westbound',
  'IncreasingDistance',
  'MinFlightsAt',
] as const;

export type TourConstraintKind = (typeof TOUR_CONSTRAINT_KINDS)[number];

/** The wake categories an `AircraftCategory` filter chooses among. */
export const WAKE_CATEGORIES = ['L', 'M', 'H', 'J'] as const;

/**
 * What one parameter holds, as the form draws it. The bounds, what is required between two fields and whether a code
 * exists are the server's (`OpenCatalog`); a list of codes is a list of objects, because that is what the generator
 * repeats.
 */
export type ParameterKind =
  | 'whole'
  | 'wholeOptional'
  | 'airport'
  | 'airports'
  | 'countries'
  | 'firs'
  | 'rules'
  | 'categories'
  | 'letters';

/** The parameters of each goal, in the order the form shows them (note 2026-09-22-il-tour-open). */
export const GOAL_PARAMETERS: Readonly<Record<OpenGoalKind, Readonly<Record<string, ParameterKind>>>> = {
  Distance: { nm: 'whole' },
  FlightCount: { count: 'whole' },
  DistinctAirports: { count: 'whole' },
  DistinctCountries: { count: 'whole' },
  CollectList: { airports: 'airports', count: 'wholeOptional' },
  CollectRegions: { countries: 'countries', firs: 'firs', count: 'wholeOptional' },
};

/** The parameters of each filter and sequence rule; a rule of sequence mostly has none. */
export const CONSTRAINT_PARAMETERS: Readonly<
  Record<TourConstraintKind, Readonly<Record<string, ParameterKind>>>
> = {
  DepartureOrArrivalIn: { countries: 'countries' },
  DepartureIn: { countries: 'countries' },
  ArrivalIn: { countries: 'countries' },
  TouchesAirport: { airports: 'airports' },
  DistanceBetween: { minNm: 'wholeOptional', maxNm: 'wholeOptional' },
  AircraftCategory: { categories: 'categories' },
  ArrivalRunwayMax: { meters: 'whole' },
  ArrivalElevationMin: { feet: 'whole' },
  FlightRules: { rules: 'rules' },
  Chained: {},
  Eastbound: {},
  Westbound: {},
  IncreasingDistance: {},
  MinFlightsAt: { airport: 'airport', count: 'whole' },
};

/** What a set of parameters chooses among, where a kind is a choice: wake categories, equipment letters. */
export interface ParameterChoices {
  readonly categories?: readonly ChoiceOption[];
  readonly letters?: readonly ChoiceOption[];
}

function parameterField(kind: ParameterKind, choices: ParameterChoices) {
  switch (kind) {
    case 'whole':
      return z.number().int();
    case 'wholeOptional':
      return z.number().int().optional();
    case 'airport':
      return z.string();
    case 'airports':
      return z.array(z.object({ icao: z.string() }));
    case 'countries':
    case 'firs':
      return z.array(z.object({ code: z.string() }));
    case 'rules':
      return z.enum(['I', 'V']);
    case 'categories':
      return z.array(z.string()).meta({ multi: true, choices: choices.categories ?? [] });
    case 'letters':
      return z.array(z.string()).meta({ multi: true, choices: choices.letters ?? [] });
  }
}

/** The shape of a set of parameters, built from what each one holds. */
export function parametersShape(
  parameters: Readonly<Record<string, ParameterKind>>,
  choices: ParameterChoices = {},
) {
  return z.object(
    Object.fromEntries(
      Object.entries(parameters).map(([name, kind]) => [name, parameterField(kind, choices)]),
    ),
  );
}

/** The parameters as the form holds them: numbers, codes, lists of `{ icao }` and `{ code }`. */
export type ParameterValues = Record<string, unknown>;

/**
 * The goal's form, under the name the server files its refusals with (`openGoalParameters.count`). The kind is chosen
 * above it, in a form of its own (`openGoalKindSchema`): the parameters are those of the kind chosen.
 */
export function openGoalSchema(goal: OpenGoalKind): z.ZodType<OpenGoalFormValues, OpenGoalFormValues> {
  return z.object({ openGoalParameters: parametersShape(GOAL_PARAMETERS[goal]) });
}

export interface OpenGoalFormValues extends Record<string, unknown> {
  openGoalParameters: ParameterValues;
}

/** The one field that picks the goal, applied as it is chosen. */
export const openGoalKindSchema = z.object({ openGoal: z.enum(OPEN_GOALS) });

/** The one field that picks the kind of a new constraint, applied as it is chosen. */
export const constraintKindSchema = z.object({ kind: z.enum(TOUR_CONSTRAINT_KINDS) });

/**
 * A filter or a sequence rule of an Open tour: its kind, chosen before and never changed, and the parameters of that
 * kind — none for most rules of sequence, and then the form has only its button.
 */
export function tourConstraintSchema(
  kind: TourConstraintKind,
  categories: readonly ChoiceOption[] = [],
): z.ZodType<TourConstraintFormValues, TourConstraintFormValues> {
  const parameters = CONSTRAINT_PARAMETERS[kind];

  return z.object({
    tourId: z.number().int().meta({ hidden: true }),
    kind: z.enum(TOUR_CONSTRAINT_KINDS).meta({ hidden: true }),
    ...(Object.keys(parameters).length === 0
      ? {}
      : { parameters: parametersShape(parameters, { categories }) }),
    rowVersion: z.string().meta({ hidden: true }),
  }) as never;
}

export interface TourConstraintFormValues extends Record<string, unknown> {
  tourId: number;
  kind: TourConstraintKind;
  parameters?: ParameterValues;
  rowVersion: string;
}

// ---- rules and errors (T9) --------------------------------------------------------------------------

/** The checks a rule or an error may name (design M2 §6.4), as `CheckCatalog` spells them. */
export const CHECK_KEYS = [
  'callsign',
  'aircraft',
  'landingAtArrival',
  'disconnections',
  'parking',
  'speed250',
  'simRate',
  'alternate',
  'equipment',
  'takeoffFromThreshold',
  'vmc',
  'repeatedRoute',
  'semicircularLevels',
  'atcCoverage',
] as const;

export type CheckKey = (typeof CHECK_KEYS)[number];

/** What the check picker holds: a check, or none. */
export type CheckChoice = CheckKey | 'none';

/** The letters of item 10a an `equipment` rule may require. */
export const EQUIPMENT_LETTERS = [
  'A',
  'B',
  'C',
  'D',
  'E1',
  'E2',
  'E3',
  'F',
  'G',
  'H',
  'I',
  'J1',
  'J2',
  'J3',
  'J4',
  'J5',
  'J6',
  'J7',
  'K',
  'L',
  'M1',
  'M2',
  'M3',
  'O',
  'P1',
  'P2',
  'P3',
  'R',
  'S',
  'T',
  'U',
  'V',
  'W',
  'X',
  'Y',
  'Z',
] as const;

/**
 * The parameters of each check. Every number may be left empty: a rule of its own then takes the check's starting value,
 * an amendment the value of the rule it amends (Carmine, 22 September 2026). Bounds and starting values are the server's
 * (`CheckCatalog`); the tolerance of the take-off from the threshold is a setting, not a parameter (answer 15).
 */
export const CHECK_PARAMETERS: Readonly<Record<CheckKey, Readonly<Record<string, ParameterKind>>>> = {
  callsign: {},
  aircraft: {},
  landingAtArrival: { radiusNm: 'wholeOptional' },
  disconnections: { maxSingleDisconnectMinutes: 'wholeOptional', maxTotalDisconnectMinutes: 'wholeOptional' },
  parking: { minParkingMinutesBefore: 'wholeOptional', minParkingMinutesAfter: 'wholeOptional' },
  speed250: { toleranceKt: 'wholeOptional' },
  simRate: { tolerancePercent: 'wholeOptional' },
  alternate: {},
  equipment: { letters: 'letters' },
  takeoffFromThreshold: {},
  vmc: { minVisibilityMeters: 'wholeOptional', minCloudBaseFeet: 'wholeOptional' },
  repeatedRoute: {},
  semicircularLevels: {},
  atcCoverage: {},
};

/** The parameters a check takes; none for no check. */
export function checkParameters(check: CheckChoice): Readonly<Record<string, ParameterKind>> {
  return check === 'none' ? {} : CHECK_PARAMETERS[check];
}

/** The one field that picks the check of a rule or of an error, applied as it is chosen. */
export const checkKeySchema = z.object({ checkKey: z.enum(['none', ...CHECK_KEYS]) });

/**
 * A rule, general or of a tour (design M2 §1.7): its code, title and text in every language, the errors that go with it,
 * and the parameters of the check chosen above it. An amendment's check is its rule's, so the picker is not drawn for it.
 */
export function ruleSchema(
  check: CheckChoice,
  { errors = [], letters = [] }: { errors?: readonly ChoiceOption[]; letters?: readonly ChoiceOption[] } = {},
): z.ZodType<RuleFormValues, RuleFormValues> {
  const parameters = checkParameters(check);

  return z.object({
    tourId: z.number().int().optional().meta({ hidden: true }),
    amendsRuleId: z.number().int().optional().meta({ hidden: true }),
    checkKey: z.enum(['none', ...CHECK_KEYS]).meta({ hidden: true }),
    code: z.string(),
    title: localized(),
    text: localized().meta({ localized: true, multiline: true }),
    ...(Object.keys(parameters).length === 0 ? {} : { parameters: parametersShape(parameters, { letters }) }),
    errorIds: z.array(z.string()).meta({ multi: true, choices: errors }),
    sort: z.number().int(),
    retired: z.boolean(),
    rowVersion: z.string().meta({ hidden: true }),
  }) as never;
}

export interface RuleFormValues extends Record<string, unknown> {
  tourId?: number;
  amendsRuleId?: number;
  checkKey: CheckChoice;
  code: string;
  title: Record<string, string>;
  text: Record<string, string>;
  parameters?: ParameterValues;
  errorIds: string[];
  sort: number;
  retired: boolean;
  rowVersion: string;
}

/** What an error weighs, as `ErrorCategory` spells it. */
export const ERROR_CATEGORIES = ['Info', 'Warning', 'Dangerous'] as const;

/**
 * An error of the division's catalogue (design M2 §1.7): its name, description and examples, its category — a warning has
 * a yearly maximum —, whether the public may read it. The check whose failure suggests it is picked above the form.
 */
export const tourErrorSchema = z.object({
  checkKey: z.enum(['none', ...CHECK_KEYS]).meta({ hidden: true }),
  name: localized(),
  description: localized().meta({ localized: true, multiline: true }),
  examples: localized().optional().meta({ localized: true, multiline: true }),
  category: z.enum(ERROR_CATEGORIES),
  // Read only for a warning; the server refuses one on the others.
  yearlyMax: z.number().int().optional(),
  isPublic: z.boolean(),
  retired: z.boolean(),
  rowVersion: z.string().meta({ hidden: true }),
});

export type TourErrorFormValues = z.output<typeof tourErrorSchema>;

/**
 * `?amends=` on a new rule of a tour: the general rule it takes the place of. Coerced, because a link written as text
 * arrives as the string `"7"` (found by the e2e round of T9).
 */
export const tourRuleSearchSchema = z.object({ amends: z.coerce.number().int().optional() });

/** "Copy the rules of another tour": which one. */
export function copyRulesSchema(tours: readonly ChoiceOption[] = []) {
  return z.object({ sourceTourId: z.string().meta({ choices: tours }) });
}

export type CopyRulesFormValues = z.output<ReturnType<typeof copyRulesSchema>>;

// ---- the pilot's report (T11b) ----------------------------------------------------------------------

/**
 * `/tours/{slug}/report`: which leg (`?leg=`), or which report «to modify» is being corrected (`?report=`). Numbers
 * from the address arrive as strings and are coerced, as T9 learned with `?amends=`.
 */
export const reportSearchSchema = z.object({
  leg: z.coerce.number().int().optional(),
  report: z.coerce.number().int().optional(),
});

export type ReportSearch = z.output<typeof reportSearchSchema>;

/** The bounds the server holds (`PirepValidation`). */
const MAX_PROCEDURE = 16;
const MAX_TEXT = 2000;

/**
 * What the pilot writes once the flight is chosen: the procedures flown and a note. The server decides which procedures
 * a tour requires, by the flight rules of the plan at take-off (design M2 §3.2 point 4) — the form offers all three and
 * says so in their hints, rather than guessing the rules of a plan the browser never read.
 */
export const reportDetailsSchema = z.object({
  sid: z.string().trim().max(MAX_PROCEDURE),
  star: z.string().trim().max(MAX_PROCEDURE),
  approach: z.string().trim().max(MAX_PROCEDURE),
  pilotRemarks: z.string().trim().max(MAX_TEXT).meta({ multiline: true }),
});

export type ReportDetailsValues = z.output<typeof reportDetailsSchema>;

export const DIVERSION_REASONS = ['Weather', 'Technical', 'Medical', 'AtcInstruction', 'Other'] as const;

/** A flight that ended elsewhere (§3.4): where, why, and a line about it. Applied as it is written, no button. */
export const diversionSchema = z.object({
  // The server says whether the airport is one (`diversionIcao`): the browser only keeps it to four letters.
  diversionIcao: z.string().trim().toUpperCase().max(4),
  diversionReason: z.enum(DIVERSION_REASONS).optional(),
  diversionNote: z.string().trim().max(MAX_TEXT).meta({ multiline: true }),
});

export type DiversionValues = z.output<typeof diversionSchema>;

export const EXEMPTION_KINDS = ['FreeSpeed', 'DirectRouting', 'LevelChange', 'Other'] as const;

/**
 * The controllers the pilot adds to the ones proposed, and the exemptions they received (design M2 §3.3): the position of
 * an exemption is chosen among the contacted ones, which is why the schema is built with them. Applied as it is written;
 * the server refuses a callsign, a frequency or an exemption that is not one, and says which list.
 */
export function atcDeclarationSchema(contacted: readonly string[]) {
  return z.object({
    added: z.array(
      z.object({
        callsign: z.string().trim().toUpperCase().max(24),
        frequency: z.string().trim().max(7),
      }),
    ),
    exemptions: z.array(
      z.object({
        callsign: z.string().meta({ choices: [...contacted] }),
        kind: z.enum(EXEMPTION_KINDS),
        note: z.string().trim().max(MAX_TEXT),
      }),
    ),
  });
}

export type AtcDeclarationValues = z.output<ReturnType<typeof atcDeclarationSchema>>;

// ---- the validation (T13b) ------------------------------------------------------------------------

/**
 * The queue's address (design M2 §4.1): the five of every list, the tour it is narrowed to, and whether it shows the
 * decided reports instead of the waiting ones. The order is not here: it is the validator's preference, kept on their user.
 */
export const reviewQueueSearchSchema = listSearchSchema.extend({
  tour: z.coerce.number().int().optional(),
  decided: z.coerce.boolean().optional(),
});

export type ReviewQueueSearch = z.output<typeof reviewQueueSearchSchema>;

/**
 * A decision (§4.3), the words of it: the outcome, the note the pilot reads, the one only the staff reads, and why the
 * decision goes against the suggestion when it does. The errors are ticked in their table, beside the counts. Which of
 * these the server requires — the note of a «to modify», the reason of an override — it says itself, field by field.
 */
export const decisionSchema = z.object({
  outcome: z.enum(['Accepted', 'ToModify', 'Rejected']),
  noteToPilot: z.string().trim().max(MAX_TEXT).meta({ multiline: true }),
  staffNote: z.string().trim().max(MAX_TEXT).meta({ multiline: true }),
  overrideReason: z.string().trim().max(MAX_TEXT).meta({ multiline: true }),
});

export type DecisionValues = z.output<typeof decisionSchema>;

/** Reopening a decision (§4.2.1): the reason is required and stays in the history. */
export const reopenSchema = z.object({
  reason: z.string().trim().min(1).max(MAX_TEXT).meta({ multiline: true }),
});

export type ReopenValues = z.output<typeof reopenSchema>;
