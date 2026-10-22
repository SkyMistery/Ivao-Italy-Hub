import type { TFunction } from 'i18next';
import { useCallback, useState } from 'react';
import type { FieldValues, Path, UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ApiError, type HubProblem } from '../api/problem';

/**
 * Turns the refusal of a write into what the form should show. The server sends i18n keys, so this
 * is where they become sentences, and it is the only place that does it: no screen resolves an
 * error of its own (design M0 §7.5).
 *
 * Field errors land on their field. Anything that is not about a field — a 409 because somebody
 * saved first, a 403 because the row belongs to another department, a refusal of the whole row
 * under `id` — becomes the alert above the form, because there is no field to point at.
 */

/** The i18n key for a status the whole form has to report, not one field. */
const TITLE_BY_STATUS: Readonly<Record<number, string>> = {
  403: 'errors.forbidden.title',
  404: 'errors.notFound.title',
  409: 'errors.conflict.title',
  429: 'errors.tooManyRequests.title',
};

export interface ProblemState {
  /** Resolved sentence for the alert above the form, or null when every error found a field. */
  readonly summary: string | null;
  /** Applies a refusal: field errors to their fields, the rest to the summary. */
  readonly apply: (error: unknown) => void;
  /** Clears the summary, which a new submission does. */
  readonly reset: () => void;
}

/**
 * `place` answers where the form draws the error the server sent under a name — as the form names
 * it — or null when it draws it nowhere (`whereDrawn`). It is asked rather than assumed because the
 * hook cannot see the fields, and an error handed to `setError` under a name nothing draws is an
 * error nobody reads: `id`, the server's refusal of the whole row, was the commonest (#331).
 */
export function useProblemDetails<TValues extends FieldValues>(
  form: UseFormReturn<TValues>,
  place: (field: string) => string | null,
): ProblemState {
  const { t, i18n } = useTranslation();
  const [summary, setSummary] = useState<string | null>(null);

  const { setError } = form;

  const apply = useCallback(
    (error: unknown) => {
      if (!(error instanceof ApiError)) {
        setSummary(t('errors.unknown'));
        return;
      }

      const fields = Object.entries(error.problem?.errors ?? {});
      const nowhere: string[] = [];

      for (const [field, keys] of fields) {
        const message = describe(keys, error.problem, field, t, i18n.language);
        const at = place(field);

        if (at === null) {
          nowhere.push(message);
        } else {
          setError(at as Path<TValues>, { type: 'server', message });
        }
      }

      // A validation answer whose errors all found a field needs no banner: the fields say it. What
      // found none is said above the form, in the words `describeProblem` uses for the same keys.
      setSummary(
        fields.length === 0 ? statusSummary(error, t) : nowhere.length === 0 ? null : nowhere.join(' '),
      );
    },
    [setError, place, t, i18n.language],
  );

  const reset = useCallback(() => setSummary(null), []);

  return { summary, apply, reset };
}

/**
 * The same refusal, for a control that is not a form. An upload has one button and no fields to
 * point an error at, so everything the server said becomes the one sentence above it.
 *
 * It lives next to the hook rather than in the screen that needed it first, so that a refusal is
 * never worded two ways; anything with fields uses the hook.
 */
export function describeProblem(error: unknown, t: TFunction, language: string): string | null {
  if (error === null || error === undefined) {
    return null;
  }

  if (!(error instanceof ApiError)) {
    return t('errors.unknown');
  }

  const fields = Object.entries(error.problem?.errors ?? {});
  if (fields.length === 0) {
    return statusSummary(error, t);
  }

  return fields.map(([field, keys]) => describe(keys, error.problem, field, t, language)).join(' ');
}

/**
 * One field, one sentence. When the key is "some languages are missing" the extension says which,
 * and naming them is the whole reason the server carries that state (design M0 §3.1).
 */
function describe(
  keys: string[],
  problem: HubProblem | undefined,
  field: string,
  t: TFunction,
  language: string,
): string {
  const missing = problem?.localized?.[field] ?? [];

  if (missing.length > 0) {
    return t('errors.localized.missingIn', { locales: languageNames(missing, language) });
  }

  return keys.map((key) => t(key)).join(' ');
}

/**
 * "it" reads as "Italian" to an English speaker and "italiano" to an Italian one. The browser owns
 * that table, so the division does not have to carry a name for every language it might add.
 *
 * Exported because publication says the same thing about a path inside a page rather than about a
 * field, and the sentence must not be written twice.
 */
export function languageNames(locales: string[], language: string): string {
  const names = new Intl.DisplayNames([language], { type: 'language' });
  const list = new Intl.ListFormat(language, { style: 'long', type: 'conjunction' });
  return list.format(locales.map((locale) => names.of(locale) ?? locale));
}

function statusSummary(error: ApiError, t: TFunction): string {
  const key = TITLE_BY_STATUS[error.status];
  return key ? t(key) : (error.problem?.title ?? t('errors.unknown'));
}
