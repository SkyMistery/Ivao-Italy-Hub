import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../api/bootstrap';

import { resolveLocalized } from './localized';

/**
 * Reading translated values in the language on screen, for a component that is handed no locale.
 * Every block is one of those: it is drawn inside a page and inside the gallery, and threading a
 * language through a tree of sections would be a prop that exists only to be passed on.
 *
 * The language on screen is already one of the division's — i18next only ever loads those — and
 * `resolveLocalized` ends at "whatever is there", so a draft written in one language still shows
 * something rather than an empty paragraph that reads like a bug.
 */
export function useLocalized(): (value: LocalizedString | null | undefined) => string {
  const { i18n } = useTranslation();
  const locale = i18n.language;

  return useCallback((value) => resolveLocalized(value, locale, locale), [locale]);
}
