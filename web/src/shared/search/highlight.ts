/**
 * Marking the words somebody searched for, inside the text the server sent back.
 *
 * It is here and not on the server for the reason design M1 §7 gives: the server does not know
 * which language the browser is showing, and it must not return HTML — a string of markup is one
 * nobody downstream can escape safely, and it decides how a page looks from the wrong end.
 *
 * No library. What this needs is one function and a handful of examples, and both fit here.
 */

/** One piece of the text: either matched by the query or not. */
export interface HighlightPart {
  readonly text: string;
  readonly match: boolean;
}

/** What separates one word from another, in the query. The same set the server splits on. */
const SEPARATORS = /[\s,;.:!?"'()[\]]+/;

/**
 * Folded for comparison: lower case and without accents, so somebody typing `cita` finds `Città`
 * and somebody typing `CITTA` finds it too. Exported because the ⌘K palette compares the names of
 * the back office screens the same way, and two ways of deciding that two words are the same word
 * is one way too many.
 *
 * ⚠️ Folding must not change the **length** of the string, or the offsets found in the folded copy
 * would not point at the same characters in the original. `NFD` splits a letter and its accent into
 * two characters, so the accents are dropped in the same pass that produced them and the string is
 * recomposed — measured on `Città`, which is 5 characters before and 5 after.
 */
export function fold(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .normalize('NFC')
    .toLowerCase();
}

/**
 * The text, split into the parts that match one of the terms and the parts that do not.
 *
 * Overlapping matches are merged rather than nested: searching for `city cities` in "cities" must
 * mark the word once, not mark `citi` inside a mark of `cities`.
 */
export function highlight(text: string, query: string): HighlightPart[] {
  if (text === '') {
    return [];
  }

  const terms = query
    .split(SEPARATORS)
    .filter((term) => term.length > 0)
    .map(fold);

  if (terms.length === 0) {
    return [{ text, match: false }];
  }

  const haystack = fold(text);

  // ⚠️ The folded copy has to line up with the original character for character, or every offset
  // below points at the wrong letter. `fold` is written to keep the length; this is the line that
  // says what happens if it ever stops doing so — no marking at all, rather than marking the wrong
  // words, because a highlight one character off is worse than none.
  if (haystack.length !== text.length) {
    return [{ text, match: false }];
  }

  const ranges: { from: number; to: number }[] = [];

  for (const term of terms) {
    let at = haystack.indexOf(term);
    while (at >= 0) {
      ranges.push({ from: at, to: at + term.length });
      at = haystack.indexOf(term, at + term.length);
    }
  }

  if (ranges.length === 0) {
    return [{ text, match: false }];
  }

  ranges.sort((left, right) => left.from - right.from);

  const merged: { from: number; to: number }[] = [];
  for (const range of ranges) {
    const last = merged.at(-1);
    if (last !== undefined && range.from <= last.to) {
      last.to = Math.max(last.to, range.to);
    } else {
      merged.push({ ...range });
    }
  }

  const parts: HighlightPart[] = [];
  let cursor = 0;

  for (const range of merged) {
    if (range.from > cursor) {
      parts.push({ text: text.slice(cursor, range.from), match: false });
    }

    parts.push({ text: text.slice(range.from, range.to), match: true });
    cursor = range.to;
  }

  if (cursor < text.length) {
    parts.push({ text: text.slice(cursor), match: false });
  }

  return parts;
}
