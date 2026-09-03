import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import i18next, { type i18n as I18n } from 'i18next';
import type { ReactNode } from 'react';
import { I18nextProvider, initReactI18next } from 'react-i18next';

import englishCommon from '../../../locales/en/common.json';
import englishErrors from '../../../locales/en/errors.json';
import italianCommon from '../../../locales/it/common.json';

/**
 * What a component needs around it in a test: the real language files of the division, and a query
 * client that does not retry.
 *
 * The language files are the ones the application ships, not a fixture — a test that invented its
 * own would pass while the screen showed a raw key. Extra keys can be layered on top for a schema
 * that only exists in a test.
 */
export function createTestI18n(extra: Record<string, unknown> = {}): I18n {
  const instance = i18next.createInstance();

  void instance.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common', 'errors'],
    defaultNS: 'common',
    fallbackNS: 'errors',
    resources: {
      en: { common: { ...englishCommon, ...extra }, errors: englishErrors },
      it: { common: italianCommon, errors: englishErrors },
    },
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

  return instance;
}

export function renderWithProviders(
  ui: ReactNode,
  options: { i18n?: I18n } = {},
): RenderResult & { i18n: I18n } {
  const i18n = options.i18n ?? createTestI18n();
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  const result = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    </I18nextProvider>,
  );

  return { ...result, i18n };
}
