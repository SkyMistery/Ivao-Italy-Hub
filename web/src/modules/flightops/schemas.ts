import { z } from 'zod';

import { DEPARTMENTS } from '../../shared/api/department';
import { localized, type Suggestion } from '../../shared/forms';

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
