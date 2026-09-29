import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { expect, it } from 'vitest';

import { createTestI18n } from '../../test/harness';
import { useDivisionLanguage } from './useDivisionLanguage';

function mount(language: string, division: { locales: string[]; defaultLocale: string }) {
  const i18n = createTestI18n();
  void i18n.changeLanguage(language);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
  );
  renderHook(() => useDivisionLanguage(division), { wrapper });
  return i18n;
}

it('moves a reader off a language the build has but the division does not publish in', async () => {
  const i18n = mount('it', { locales: ['en'], defaultLocale: 'en' });

  await waitFor(() => expect(i18n.language).toBe('en'));
});

it('leaves a reader alone in a language the division speaks', async () => {
  const i18n = mount('en', { locales: ['it', 'en'], defaultLocale: 'it' });

  await new Promise((resolve) => setTimeout(resolve, 20));
  expect(i18n.language).toBe('en');
});
