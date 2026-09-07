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

/**
 * What a menu entry is called. Two kinds of entry arrive from `/api/me` and they stay two kinds:
 * a module's carries a translation key, because a module cannot know which language this browser
 * is showing; an editorial row carries the words themselves in every language, because the person
 * who typed them was never going to invent a key (design M1 §8.1).
 *
 * One function rather than the same ternary in the header, the footer and the sidebar.
 */
export function navLabel(
  item: { key: string | null; label: LocalizedString | null },
  translate: (key: string) => string,
  locale: string,
  defaultLocale: string,
): string {
  return item.label ? resolveLocalized(item.label, locale, defaultLocale) : translate(item.key ?? '');
}
