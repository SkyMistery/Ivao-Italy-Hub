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

/**
 * The form of a tour, mirroring `TourWriteDto` (design M2 §1.2, §8.3). A function, because what it offers is known
 * only at runtime — the types as they are typed, the awards of the division — and because two things are decided
 * before it opens: a template has no address, no dates and no award (§1.10), and the kind of a tour the public
 * already sees no longer changes (§1.2.1). Those fields are carried and not drawn. Every rule is the server's.
 */
export function tourSchema({
  types = [],
  awards = [],
  groups = [],
  isTemplate = false,
  kindLocked = false,
}: {
  types?: readonly Suggestion[];
  awards?: readonly ChoiceOption[];
  groups?: readonly ChoiceOption[];
  isTemplate?: boolean;
  kindLocked?: boolean;
} = {}) {
  return z.object({
    ownerDepartment: z.enum(DEPARTMENTS).meta({ hidden: true }),
    isTemplate: z.boolean().meta({ hidden: true }),
    kind: z.enum(TOUR_KINDS).meta({ hidden: kindLocked }),
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
    // The aircraft admitted: types and groups, nothing else (design M2 §1.5); both empty admit all. The types carry the
    // payload's name, so that a refusal of the server — which files it under `allowedAircraft` — lands on a field.
    allowedAircraft: z.array(
      z.object({ icao: z.string().meta({ suggestions: types, suggestionsOnly: true }) }),
    ),
    allowedGroups: z.array(z.object({ groupId: z.string().optional().meta({ choices: groups }) })),
    awardId: z.string().optional().meta({ choices: awards, hidden: isTemplate }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type TourFormValues = z.output<ReturnType<typeof tourSchema>>;

/**
 * The address a tour's own screen is opened with: `?template=true` makes a new one a template, `?tab=` says which tab is
 * open (T6b: settings and briefing; T7a the legs; T7b and T9 add theirs).
 */
export const tourEditorSearchSchema = z.object({
  template: z.boolean().optional(),
  tab: z.enum(['settings', 'briefing', 'legs']).optional(),
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
