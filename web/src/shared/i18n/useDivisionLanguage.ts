import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

import { divisionLanguage } from './language';

/**
 * Keeps the hub in a language the division speaks.
 *
 * Before the bootstrap arrives the client only knows which languages it has files for, and a build
 * may carry more than this division publishes in (the repository ships `it/` to a fork that only
 * lists `en`). Once `division.locales` is known, a reader left on another language is moved to the
 * division's answer for them — the rule of `divisionLanguage`, the server's own.
 */
export function useDivisionLanguage(division: { locales: readonly string[]; defaultLocale: string }): void {
  // No suspense: this sits above every route and must never hold the tree up for a language file.
  const { i18n } = useTranslation(undefined, { useSuspense: false });
  const current = i18n.language as string | undefined;
  const wanted = current === undefined ? undefined : divisionLanguage(current, division);

  useEffect(() => {
    if (wanted !== undefined && wanted !== current) {
      void i18n.changeLanguage(wanted);
    }
  }, [i18n, current, wanted]);
}
