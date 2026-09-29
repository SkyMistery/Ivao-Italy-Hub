import { describe, expect, it } from 'vitest';

import { divisionLanguage, spokenLanguage } from './language';

describe('spokenLanguage', () => {
  it('counts a regional tag as its language, spelled the way the list spells it', () => {
    expect(spokenLanguage('en-GB', ['it', 'en'])).toBe('en');
    expect(spokenLanguage('it-IT', ['it', 'en'])).toBe('it');
    expect(spokenLanguage('EN_us', ['it', 'en'])).toBe('en');
    expect(spokenLanguage('it', ['IT'])).toBe('IT');
  });

  it('answers nothing for a language the list does not have, or for no tag at all', () => {
    expect(spokenLanguage('de-DE', ['it', 'en'])).toBeUndefined();
    expect(spokenLanguage('', ['it', 'en'])).toBeUndefined();
    expect(spokenLanguage(null, ['it', 'en'])).toBeUndefined();
  });
});

describe('divisionLanguage', () => {
  const division = { locales: ['xx', 'en'], defaultLocale: 'xx' };

  it('keeps the reader in their language when the division speaks it', () => {
    expect(divisionLanguage('xx-YY', division)).toBe('xx');
    expect(divisionLanguage('en-GB', division)).toBe('en');
  });

  it('falls back to English, and to the division default when English is not spoken either', () => {
    expect(divisionLanguage('de-DE', division)).toBe('en');
    expect(divisionLanguage('de-DE', { locales: ['xx'], defaultLocale: 'xx' })).toBe('xx');
    expect(divisionLanguage(undefined, { locales: ['xx'], defaultLocale: 'xx' })).toBe('xx');
  });
});
