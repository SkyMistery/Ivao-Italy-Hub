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
 * `LocaleSwitcher` writes `hub.lang`, which is what this detector reads; a signed in member's
 * choice also goes to `hub_users.locale` through `PUT /api/me/locale`, so it follows them to
 * another browser.
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
      ns: ['common', 'errors'],
      defaultNS: 'common',
      // The server sends i18n keys in the machine readable part of a refusal, and they are the
      // keys of `errors.json` with no namespace in front, because the server does not have
      // namespaces: `errors.localized.missing`. Falling back to that file is what lets the client
      // resolve them without every call site remembering where they live.
      fallbackNS: 'errors',
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
