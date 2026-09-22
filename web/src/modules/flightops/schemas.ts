import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized, type ChoiceOption, type Suggestion } from '../../shared/forms';

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
  tab: z.enum(['settings', 'briefing', 'legs', 'hubs', 'subtours', 'callsigns', 'open']).optional(),
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
  'whole' | 'wholeOptional' | 'airport' | 'airports' | 'countries' | 'firs' | 'rules' | 'categories';

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

function parameterField(kind: ParameterKind, categories: readonly ChoiceOption[]) {
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
      return z.array(z.string()).meta({ multi: true, choices: categories });
  }
}

/** The shape of a set of parameters, built from what each one holds. */
export function parametersShape(
  parameters: Readonly<Record<string, ParameterKind>>,
  categories: readonly ChoiceOption[] = [],
) {
  return z.object(
    Object.fromEntries(
      Object.entries(parameters).map(([name, kind]) => [name, parameterField(kind, categories)]),
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
    ...(Object.keys(parameters).length === 0 ? {} : { parameters: parametersShape(parameters, categories) }),
    rowVersion: z.string().meta({ hidden: true }),
  }) as never;
}

export interface TourConstraintFormValues extends Record<string, unknown> {
  tourId: number;
  kind: TourConstraintKind;
  parameters?: ParameterValues;
  rowVersion: string;
}
