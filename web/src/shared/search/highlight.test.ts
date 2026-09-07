import { describe, expect, test } from 'vitest';

import { highlight } from './highlight';

/**
 * The highlighting of search results (design M1 §7, point 2). What is worth testing here is not
 * that it finds a word — it is the four ways of getting it subtly wrong: accents, case, two terms
 * that overlap, and a term that appears more than once.
 */

/** What was marked, in order. Reads better in an assertion than the parts themselves. */
const marked = (text: string, query: string) =>
  highlight(text, query)
    .filter((part) => part.match)
    .map((part) => part.text);

/** The whole text back, so that nothing was lost or duplicated along the way. */
const rebuilt = (text: string, query: string) =>
  highlight(text, query)
    .map((part) => part.text)
    .join('');

describe('highlight', () => {
  test('marks the term wherever it appears, whatever the case', () => {
    expect(marked('Events and events and EVENTS', 'events')).toEqual(['Events', 'events', 'EVENTS']);
  });

  test('finds a word written with accents when the query has none', () => {
    // Somebody typing on a keyboard without accents is searching for the same word, and the server
    // that produced this snippet folded it the same way.
    expect(marked('La città e le città vicine', 'citta')).toEqual(['città', 'città']);
  });

  test('finds a word written without accents when the query has them', () => {
    expect(marked('The citta of Rome', 'città')).toEqual(['citta']);
  });

  test('keeps the text exactly as it was, accents included', () => {
    // The marking must not rewrite what it marks: what is drawn is the original text, cut into
    // pieces. A fold that leaked into the output would show "citta" on a page that says "città".
    const text = 'La città è grande';
    expect(rebuilt(text, 'citta')).toBe(text);
    expect(marked(text, 'citta')).toEqual(['città']);
  });

  test('merges two terms that overlap instead of marking one inside the other', () => {
    // `cities` contains `citi`: without merging, the second range would open inside the first and
    // the text would come back cut in the wrong places.
    expect(marked('Two cities', 'citi cities')).toEqual(['cities']);
    expect(rebuilt('Two cities', 'citi cities')).toBe('Two cities');
  });

  test('marks nothing when nothing matches, and still returns the whole text', () => {
    expect(marked('Nothing to see', 'aeroplane')).toEqual([]);
    expect(rebuilt('Nothing to see', 'aeroplane')).toBe('Nothing to see');
  });

  test('an empty query marks nothing rather than everything', () => {
    expect(marked('Some text', '')).toEqual([]);
    expect(marked('Some text', '   ')).toEqual([]);
  });

  test('splits the query on punctuation, the way the server does', () => {
    expect(marked('Rome, and Milan', 'rome, milan')).toEqual(['Rome', 'Milan']);
  });

  test('an empty text is no parts at all', () => {
    expect(highlight('', 'anything')).toEqual([]);
  });
});
