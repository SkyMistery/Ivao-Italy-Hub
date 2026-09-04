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
 * saved first, a 403 because the row belongs to another department — becomes the alert above the
 * form, because there is no field to point at.
 */

/** The i18n key for a status the whole form has to report, not one field. */
const TITLE_BY_STATUS: Readonly<Record<number, string>> = {
  403: 'errors.forbidden.title',
  404: 'errors.notFound.title',
  409: 'errors.conflict.title',
};

export interface ProblemState {
  /** Resolved sentence for the alert above the form, or null when every error found a field. */
  readonly summary: string | null;
  /** Applies a refusal: field errors to their fields, the rest to the summary. */
  readonly apply: (error: unknown) => void;
  /** Clears the summary, which a new submission does. */
  readonly reset: () => void;
}

export function useProblemDetails<TValues extends FieldValues>(form: UseFormReturn<TValues>): ProblemState {
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

      for (const [field, keys] of fields) {
        setError(field as Path<TValues>, {
          type: 'server',
          message: describe(keys, error.problem, field, t, i18n.language),
        });
      }

      // A validation answer whose errors all found a field needs no banner: the fields say it.
      setSummary(fields.length > 0 ? null : statusSummary(error, t));
    },
    [setError, t, i18n.language],
  );

  const reset = useCallback(() => setSummary(null), []);

  return { summary, apply, reset };
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
