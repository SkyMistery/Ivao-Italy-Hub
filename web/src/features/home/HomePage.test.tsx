import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import i18next from 'i18next';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { beforeAll, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';

import { HomePage } from './HomePage';

/**
 * The home is a published row now, so what this test watches has changed with it: that the page
 * draws what the server published, and that a site whose home is missing says so in words from
 * `locales/` rather than showing a blank frame or a raw key.
 */

const i18n = i18next.createInstance();

const api = vi.hoisted(() => ({ get: vi.fn() }));

/**
 * The two calls this screen makes, answered by address. Answering every GET with the same body is
 * what the first version of this file did, and it handed the bootstrap a page: the screen then
 * threw while reading the name of a division that was not there, and the failure looked like a
 * missing heading three assertions later.
 */
function answer(page: unknown) {
  api.get.mockImplementation((path: string) =>
    Promise.resolve(path === '/api/me' ? { data: bootstrap } : page),
  );
}

const bootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    faviconUrl: null,
    firStaffScope: 'all',
    siteDepartment: 'WD',
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], widgets: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
};

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get },
}));

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

function draw() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <HomePage />
      </I18nextProvider>
    </QueryClientProvider>,
  );
}

test('draws the page somebody published, and never a word of its own', async () => {
  answer({
    data: {
      kind: 'Page',
      slug: 'home',
      ownerDepartment: 'WD',
      title: { en: 'Home' },
      summary: null,
      seo: null,
      body: {
        schemaVersion: 1,
        sections: [
          {
            id: 's_hero',
            key: 'hero',
            title: { en: 'Opening' },
            layout: 'stacked',
            background: 'none',
            padding: 'md',
            width: 'default',
            blocks: [
              {
                id: 'b_heading',
                type: 'heading',
                version: 1,
                props: { level: 1, text: { en: 'What the division published' } },
              },
            ],
          },
        ],
      },
      schemaVersion: 1,
      category: null,
      coverMediaId: null,
      fileMediaId: null,
      version: 1,
      publishedAt: '2026-09-06T10:00:00Z',
    },
  });

  draw();

  expect(
    await screen.findByRole('heading', { name: 'What the division published', level: 1 }),
  ).toBeInTheDocument();
});

test('says so when no home has been published, in words from the language files', async () => {
  answer({ error: { status: 404 } });

  draw();

  expect(await screen.findByText(englishCommon.home.empty)).toBeInTheDocument();
});
