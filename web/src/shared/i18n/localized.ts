import type { LocalizedString } from '../api/bootstrap';

/**
 * Reading a translated field. The server sends every language it has and never picks one, because
 * only the browser knows which language it is drawing (design M0 §3.1).
 *
 * The order is: the language on screen, then the same language without its region (`it-IT` reads
 * `it`), then the default language of the division, then whatever is there. The last step matters
 * for a draft: a row that only has English yet must still show something in an Italian list rather
 * than an empty cell that reads like a bug.
 */
export function resolveLocalized(
  value: LocalizedString | null | undefined,
  locale: string,
  defaultLocale: string,
): string {
  if (!value) {
    return '';
  }

  const base = locale.split('-')[0] ?? locale;
  return value[locale] ?? value[base] ?? value[defaultLocale] ?? Object.values(value)[0] ?? '';
}

/** The languages that carry a non empty value; what `LocaleFields` marks as done. */
export function filledLocales(value: LocalizedString | null | undefined): string[] {
  if (!value) {
    return [];
  }

  return Object.entries(value)
    .filter(([, text]) => text.trim().length > 0)
    .map(([locale]) => locale);
}

/** An empty translated value with one entry per language, which is what a new form starts from. */
export function emptyLocalized(locales: readonly string[]): Record<string, string> {
  return Object.fromEntries(locales.map((locale) => [locale, '']));
}
