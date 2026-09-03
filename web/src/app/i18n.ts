import i18next, { type i18n as I18n } from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import HttpBackend from 'i18next-http-backend';
import { initReactI18next } from 'react-i18next';

/**
 * The division owns its languages: `locales/{lng}/{ns}.json` at the root of the repository is the
 * only source of user facing text, read by the SPA and by the backend as well.
 *
 * English is the fallback, and not because this division happens to speak it: it is the language of
 * IVAO and of this project, so it is what anyone falls back to when the division does not speak
 * theirs. The server picks a signed in member's language with the same rule (`LocalePreference`)
 * and returns it as `user.locale` in the bootstrap.
 *
 * Applying it here is F6, together with the language switcher that writes the cookie this detector
 * reads. Until then the browser decides, and `hub.lang` is only ever read, never written.
 */
export const DEFAULT_LOCALE = 'en';
export const LOCALE_COOKIE = 'hub.lang';

export function createI18n(): I18n {
  const instance = i18next.createInstance();

  void instance
    .use(HttpBackend)
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      fallbackLng: DEFAULT_LOCALE,
      ns: ['common'],
      defaultNS: 'common',
      backend: { loadPath: '/locales/{{lng}}/{{ns}}.json' },
      detection: {
        order: ['cookie', 'navigator'],
        lookupCookie: LOCALE_COOKIE,
        caches: [],
      },
      interpolation: { escapeValue: false },
    });

  return instance;
}
