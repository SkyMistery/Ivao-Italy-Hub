/**
 * A title turned into the address it proposes.
 *
 * The rule it aims at is the server's — `^[a-z0-9]+(?:-[a-z0-9]+)*$`, in `ContentWriteDtoValidator`
 * — and the comment there has said from the start that "the editor proposes one from the title".
 * This is that half, arriving late: until the demo of M1, an address had to be typed from scratch
 * next to a title that had just been written.
 *
 * It is a **proposal** and not a validation: nothing here refuses anything, the server does that
 * and answers, and what it answers reaches the field through `useProblemDetails` like every other
 * refusal (design M0 §7.5). The length limit of the column is not a rule of this function: it is handed
 * in by the field that knows it (`slugMaxLength`), because since design 13 §4.1 a slug may be proposed in
 * a field the writer cannot see, and a proposal too long would then be a refusal nobody can answer.
 *
 * Accents are folded rather than dropped: "Città" gives `citta`, which is what somebody typing the
 * address by hand would write, while dropping the letter would give `citt`.
 */
export function slugify(title: string, maxLength?: number): string {
  const slug = title
    .normalize('NFD')
    // Everything Unicode files as a combining mark: the accents that NFD has just separated.
    .replace(/\p{Mn}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');

  // Cut where the column ends, and never on a dash: `a-b-` is not an address the server takes.
  return maxLength === undefined ? slug : slug.slice(0, maxLength).replace(/-+$/, '');
}

/**
 * The text a proposal reads. A translated source is read in the default language of the division,
 * which is the language the address is published in; the first language that has anything written
 * in it stands in while that one is still empty, so a title written in Italian first still proposes
 * something rather than nothing.
 */
export function readSource(value: unknown, defaultLocale: string | undefined): string {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value !== 'object' || value === null) {
    return '';
  }

  const translations = value as Record<string, unknown>;
  const preferred = defaultLocale === undefined ? undefined : translations[defaultLocale];

  if (typeof preferred === 'string' && preferred.trim() !== '') {
    return preferred;
  }

  for (const written of Object.values(translations)) {
    if (typeof written === 'string' && written.trim() !== '') {
      return written;
    }
  }

  return '';
}

/**
 * Whether something is written in the source and no slug comes out of it: a title in another script (Cyrillic, Greek,
 * CJK, Arabic), or only emoji and punctuation. The field of the slug is then drawn to whoever writes, who may not see it
 * otherwise, so that they write the address themselves (design 13 §4.1; the maintainer's answer on #554, point 1).
 * Nothing written is not this: the proposal is empty because there is nothing yet to propose from.
 */
export function cannotProposeASlug(value: unknown, defaultLocale: string | undefined): boolean {
  const source = readSource(value, defaultLocale);
  return source.trim() !== '' && slugify(source) === '';
}
