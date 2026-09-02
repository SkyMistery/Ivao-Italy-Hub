import i18next, { type i18n as I18n } from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import HttpBackend from 'i18next-http-backend';
import { initReactI18next } from 'react-i18next';

/**
 * The division owns its languages: `locales/{lng}/{ns}.json` at the root of the repository is the
 * only source of user facing text, read by the SPA and (from F1) by the backend as well.
 * The list of supported languages comes from `division.json` and is wired in F1; until then the
 * detector decides and anything missing falls back to the default language.
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
