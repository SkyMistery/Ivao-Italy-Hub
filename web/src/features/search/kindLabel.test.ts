import { expect, test } from 'vitest';

import englishTours from '../../../../locales/en/flightops.json';
import italianTours from '../../../../locales/it/flightops.json';
import { createTestI18n } from '../../test/harness';

import { kindLabel } from './kindLabel';

/** The real language files, with a module's namespace next to the core's, as the application loads them. */
function i18nWithTours() {
  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishTours);
  i18n.addResourceBundle('it', 'flightops', italianTours);
  return i18n;
}

test('a row of the core is named by the core', () => {
  const i18n = i18nWithTours();

  expect(kindLabel(i18n.t, { sourceModule: 'core', kind: 'news' })).toBe('News');
});

test('a row of a module is named by the module, in the language on screen', async () => {
  const i18n = i18nWithTours();

  expect(kindLabel(i18n.t, { sourceModule: 'flightops', kind: 'tour' })).toBe(englishTours.search.kinds.tour);

  await i18n.changeLanguage('it');
  expect(kindLabel(i18n.t, { sourceModule: 'flightops', kind: 'tour' })).toBe(italianTours.search.kinds.tour);
});

test('a module does not borrow a word of the core, nor the core one of a module', () => {
  const i18n = i18nWithTours();

  expect(kindLabel(i18n.t, { sourceModule: 'flightops', kind: 'news' })).toBe('news');
  expect(kindLabel(i18n.t, { sourceModule: 'core', kind: 'tour' })).toBe('tour');
});

test('a kind nobody has a word for shows itself', () => {
  const i18n = i18nWithTours();

  expect(kindLabel(i18n.t, { sourceModule: 'somewhere', kind: 'thing' })).toBe('thing');
});
