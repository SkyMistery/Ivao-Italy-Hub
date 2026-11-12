import { describe, expect, it } from 'vitest';
import { resolveLocalized } from './localized';

describe('resolveLocalized', () => {
  it('reads the language on screen, then its base, then the division default, then any', () => {
    expect(resolveLocalized({ it: 'Ciao', en: 'Hello' }, 'en', 'it')).toBe('Hello');
    expect(resolveLocalized({ it: 'Ciao', en: 'Hello' }, 'en-GB', 'it')).toBe('Hello');
    expect(resolveLocalized({ it: 'Ciao' }, 'en', 'it')).toBe('Ciao');
    expect(resolveLocalized({ de: 'Hallo' }, 'en', 'it')).toBe('Hallo');
  });

  it('takes an empty value for no translation (issue #332, item 6)', () => {
    // What a form sends for a row written only in Italian.
    expect(resolveLocalized({ it: 'Ciao', en: '' }, 'en', 'it')).toBe('Ciao');
    expect(resolveLocalized({ it: '', en: '  ' }, 'en', 'it')).toBe('');
    expect(resolveLocalized({ en: '', fr: 'Bonjour' }, 'en', 'it')).toBe('Bonjour');
  });

  it('reads nothing as an empty string', () => {
    expect(resolveLocalized(null, 'en', 'it')).toBe('');
    expect(resolveLocalized(undefined, 'en', 'it')).toBe('');
  });
});
