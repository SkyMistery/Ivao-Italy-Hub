import { render, screen } from '@testing-library/react';
import i18next from 'i18next';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { beforeAll, expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';

import { HomePage } from './HomePage';

const i18n = i18next.createInstance();

beforeAll(async () => {
  await i18n.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common'],
    defaultNS: 'common',
    resources: { en: { common: englishCommon } },
    interpolation: { escapeValue: false },
  });
});

test('renders translated text, never a literal', () => {
  render(
    <I18nextProvider i18n={i18n}>
      <HomePage />
    </I18nextProvider>,
  );

  expect(screen.getByRole('heading', { name: englishCommon.home.heading })).toBeInTheDocument();
  expect(screen.getByText(englishCommon.home.lead)).toBeInTheDocument();
});
