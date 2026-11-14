import type { i18n as I18n } from 'i18next';
import { z } from 'zod';

/**
 * The words of a check the browser makes with a schema, in the server's keys (#386, note
 * `2026-11-14-le-parole-dei-controlli-del-browser`, decided by the maintainer).
 *
 * The generated form validates with the schema before it sends anything (`zodResolver`), and draws under the field the
 * message zod gives it. No schema passes zod a message — the rules are written once, in the schema — so zod spoke its own
 * English: «Too big: expected string to have <=16 characters» under the SID of the pilot's report, whatever the language
 * on screen. The server refuses the same values with i18n keys, which `useProblemDetails` turns into sentences; the
 * browser now says the same words, and no rule is copied to say them.
 *
 * ⚠️ What a schema writes itself still wins: in zod 4 an error given to a schema comes before this one.
 */
export function zodIssueKey(issue: z.core.$ZodRawIssue): string {
  if (issue.code === 'too_big' && issue.origin === 'string') {
    return 'errors.text.tooLong';
  }

  // Missing, or present and empty: the box left blank and the list with nothing in it are both a value not given.
  if (issue.code === 'invalid_type' && (issue.input === undefined || issue.input === null)) {
    return 'errors.required';
  }

  if (
    issue.code === 'too_small' &&
    (issue.origin === 'string' || issue.origin === 'array') &&
    Number(issue.minimum) === 1
  ) {
    return 'errors.required';
  }

  return 'errors.invalid';
}

/**
 * Sets the words once, at the start of the application, with its language files: every schema of every module is
 * covered without touching one. Resolved when the check runs, so the sentence is in the language on screen.
 */
export function speakTheServersWords(i18n: I18n): void {
  z.config({ customError: (issue) => i18n.t(zodIssueKey(issue)) });
}
