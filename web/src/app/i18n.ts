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
 *
 * Which namespaces to load is the composed registry's answer, not a list written here: a module
 * declares its own in its manifest and `pnpm i18n:sync` puts the files where this can fetch them.
 * They are passed in rather than read from `app/registry` directly, because the registry pulls in
 * every block and widget component of the application and half of those reach back here — the
 * composition root is where the two meet, and it is the one place with no cycle to make.
 */
export const DEFAULT_LOCALE = 'en';
export const LOCALE_COOKIE = 'hub.lang';

export function createI18n(namespaces: readonly string[]): I18n {
  const instance = i18next.createInstance();

  void instance
    .use(HttpBackend)
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
      fallbackLng: DEFAULT_LOCALE,
      ns: [...namespaces],
      defaultNS: 'common',
      // A namespace is where a file is, never part of a key: the server proves it, because it
      // sends the keys of a refusal with no namespace in front (`errors.localized.missing`) and
      // its own catalogue has none. So everything that is not the default is a fallback, and a key
      // is written the same wherever it lives -- including `nav.atc`, which belongs to a module.
      fallbackNS: namespaces.filter((namespace) => namespace !== 'common'),
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
