import { expect, test } from 'vitest';

import { cannotProposeASlug, slugify } from './slug';

// The maintainer's answer on #554, point 1: a title that is written and proposes no slug opens the field of the slug.
test.each([
  ['Правила сайта', true],
  ['Κανόνες', true],
  ['規則', true],
  ['🚀✈️', true],
  ['!!! ???', true],
  ['Città', false],
  ['ł ø ß', true],
  ['', false],
  ['   ', false],
])('%j proposes no slug: %s', (title, opens) => {
  expect(cannotProposeASlug(title, 'en')).toBe(opens);
});

test('a translated title is read in the main language, and the others stand in while it is empty', () => {
  expect(cannotProposeASlug({ en: 'Правила', it: 'Regole' }, 'en')).toBe(true);
  expect(cannotProposeASlug({ en: '', it: 'Regole' }, 'en')).toBe(false);
  expect(cannotProposeASlug({ en: '', it: '' }, 'en')).toBe(false);
});

test('the cut never leaves a dash at the end', () => {
  expect(slugify('The rules of the site', 10)).toBe('the-rules');
  expect(slugify('The rules of the site')).toBe('the-rules-of-the-site');
});
