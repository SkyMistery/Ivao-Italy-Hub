/**
 * What the API answers when it refuses. The machine readable part carries i18n keys and never
 * prose, because only the browser knows which language it is drawing (design M0 §3.9 and §7.5):
 * `errors[field] = ["errors.localized.missing"]`, and when languages are missing the `localized`
 * extension says which ones.
 *
 * The generated contract describes `HttpValidationProblemDetails` without its extensions — an
 * extension is, by definition, not in the schema — so the extension is spelled out here, once.
 */
export interface HubProblem {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  /** One or more i18n keys per field, named as the API spells the field. */
  errors?: Record<string, string[]>;
  /** Per field, the languages of the division that have no value yet. */
  localized?: Record<string, string[]>;
}

/**
 * A call the server refused. Thrown by every query and mutation, so that a screen never inspects
 * a raw response and `useProblemDetails` has exactly one shape to read.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: HubProblem | undefined,
  ) {
    super(`The API answered ${status}.`);
    this.name = 'ApiError';
  }
}

/** Reads the extension safely: it is absent whenever nothing was missing. */
export function missingLocales(problem: HubProblem | undefined, field: string): string[] {
  return problem?.localized?.[field] ?? [];
}
