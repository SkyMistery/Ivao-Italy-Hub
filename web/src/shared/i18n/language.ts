/**
 * Which language the hub is read in, given what the browser or the member asks for — the same rule
 * the server applies in `LocalePreference`, so that the two never disagree.
 *
 * Browsers hand out regional tags (`en-GB`, `it-IT`) and the language files exist once per language
 * (`locales/en/`, `locales/it/`): a tag counts as its language, and the answer is spelled the way the
 * list spells it. Nothing here knows which languages there are — the caller says, from the files the
 * build shipped or from the division in the bootstrap.
 */

/**
 * English. Not a division's choice: it is the language of IVAO and of this project, so it is what a
 * reader falls back to when the division does not speak theirs (`LocalePreference.Fallback`).
 */
export const FALLBACK_LANGUAGE = 'en';

/** The language as `spoken` spells it, or `undefined` when that language is not among them. */
export function spokenLanguage(
  tag: string | null | undefined,
  spoken: readonly string[],
): string | undefined {
  const language = tag?.trim().split(/[-_]/)[0]?.toLowerCase();
  if (!language) {
    return undefined;
  }

  return spoken.find((candidate) => candidate.toLowerCase() === language);
}

/**
 * The language a reader who asks for `tag` gets from a division: theirs if the division speaks it,
 * English if it speaks that, its own default otherwise (`LocalePreference.Resolve`).
 */
export function divisionLanguage(
  tag: string | null | undefined,
  division: { locales: readonly string[]; defaultLocale: string },
): string {
  return (
    spokenLanguage(tag, division.locales) ??
    spokenLanguage(FALLBACK_LANGUAGE, division.locales) ??
    division.defaultLocale
  );
}
