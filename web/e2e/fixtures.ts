import type { Page } from '@playwright/test';

/**
 * What `GET /api/me` answers for an anonymous visitor of a division that speaks two languages.
 *
 * Written out rather than fetched from a running API on purpose: this suite is about the front end
 * assembling itself in a browser, and an API in the loop would make it slower, flakier and no
 * better at the one job it has. The shape is the generated contract's — if the server changes it,
 * `pnpm gen:api` moves `schema.d.ts`, the typed client stops compiling, and that is the check.
 */
export const anonymousBootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example', it: 'IVAO Esempio' },
    locales: ['en', 'it'],
    defaultLocale: 'en',
    // Not UTC, deliberately. A hub shows every time in UTC *and* where the division lives, and a
    // fixture whose division sits in UTC makes the two lines identical — which is exactly how a
    // screen showing UTC twice would pass unnoticed (HANDOFF §13, third false alarm).
    timezone: 'Europe/Rome',
    firStaffScope: 'all',
    // Which department owns the site, and therefore where its menu is edited. The client is told
    // rather than knowing (design M1 §8.1).
    siteDepartment: 'WD',
  },
  modules: [],
  // Since M1 G8 the public menu is a table: an editorial entry carries its words in every language,
  // a module's carries a translation key, and the fixture holds one of each because the header has
  // to draw both (design M1 §8.1).
  navigation: {
    public: [
      { key: null, path: '/', label: { en: 'Home', it: 'Home' }, children: [] },
      {
        key: null,
        path: '/about',
        label: { en: 'About', it: 'Chi siamo' },
        children: [{ key: null, path: '/about/team', label: { en: 'Team', it: 'Squadra' }, children: [] }],
      },
      { key: 'nav.atc', path: '/atc', label: null, children: [] },
    ],
    footer: [{ key: null, path: '/legal', label: { en: 'Legal', it: 'Note legali' }, children: [] }],
    staff: [],
  },
  registries: { blocks: [], widgets: [], permissions: [] },
  // The division's calendar vocabulary, which a visitor gets too: a chip on a public calendar says
  // the word and takes the colour somebody chose (decided 8 Sep 2026).
  calendarKinds: [
    { key: 'meeting', label: { en: 'Meeting', it: 'Riunione' }, colour: 'indigo' },
    { key: 'deadline', label: { en: 'Deadline', it: 'Scadenza' }, colour: 'orange' },
  ],
  version: '0.0.0-e2e',
};

/**
 * The page a visitor reads at `/`: a published row, because since M1 G8 the front page is one. The
 * heading is a block, exactly as the editor would have written it.
 */
export const publishedHome = {
  kind: 'Page',
  slug: 'home',
  ownerDepartment: 'WD',
  title: { en: 'Home', it: 'Home' },
  summary: { en: 'The front page.', it: 'La pagina d ingresso.' },
  seo: null,
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 's_hero',
        key: 'hero',
        title: { en: 'Opening', it: 'Apertura' },
        layout: 'stacked',
        background: 'none',
        padding: 'lg',
        width: 'default',
        blocks: [
          {
            id: 'b_heading',
            type: 'heading',
            version: 1,
            props: { level: 1, text: { en: 'Welcome to the division', it: 'Benvenuti nella divisione' } },
          },
          {
            id: 'b_text',
            type: 'text',
            version: 1,
            props: {
              markdown: {
                en: 'Everything on this page is a row somebody published.',
                it: 'Tutto in questa pagina e una riga che qualcuno ha pubblicato.',
              },
            },
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
};

/**
 * Answers the calls the shell makes on its way up, and fails loudly on any other `/api` request:
 * a smoke that silently swallowed an unexpected call would hide the very thing it is watching for.
 */
export async function stubTheApi(page: Page): Promise<void> {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(anonymousBootstrap),
    }),
  );

  // The front page is a published row now, so the shell asks for one on its way up.
  await page.route('**/api/content/public/**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(publishedHome),
    }),
  );

  await page.route('**/api/**', (route) => {
    const url = route.request().url();
    if (url.includes('/api/me') || url.includes('/api/content/public/')) {
      return route.fallback();
    }

    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: `Unexpected call in the smoke suite: ${url}` }),
    });
  });
}

/**
 * A staff member who may work on the links of ED: enough to open `/staff/ed/links` and the form
 * behind its "new" button, and nothing more.
 *
 * `hasAllDepartments` is false and `departments` holds one entry on purpose: that is the identity
 * the department guard on the route actually examines, so a smoke run under a superadmin would not
 * be exercising it.
 */
export const staffBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: 111111,
    firstName: 'Test',
    lastName: 'Coordinator',
    positions: ['XX-EC'],
    isStaff: true,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: ['ED'],
    firs: [],
  },
  permissions: [
    { name: 'Links.View', department: 'ED' },
    { name: 'Links.Edit', department: 'ED' },
    { name: 'Media.View', department: 'ED' },
    { name: 'Media.Edit', department: 'ED' },
    // The gallery is behind `Admin.Access`, and the gallery is where every kind of field the form
    // generator draws is mounted at once — which is the only screen that can be looked at whole.
    { name: 'Admin.Access', department: null },
    // Global, and it is the point of it: the vocabulary of the calendar belongs to the division,
    // so this coordinator holds it not because of their department but in spite of it.
    { name: 'Calendar.ManageKinds', department: null },
  ],
  navigation: {
    ...anonymousBootstrap.navigation,
    staff: [{ key: 'nav.links', path: '/staff/links', label: null, children: [] }],
  },
};

/** The vocabulary of the calendar, the shape `MapCrud` answers a list with. */
export const theVocabulary = {
  items: [
    {
      id: 1,
      key: 'meeting',
      label: { en: 'Meeting', it: 'Riunione' },
      colour: 'indigo',
      sort: 10,
      isActive: true,
      updatedAt: '2026-09-08T12:00:00Z',
    },
    {
      id: 2,
      key: 'deadline',
      label: { en: 'Deadline', it: 'Scadenza' },
      colour: 'orange',
      sort: 20,
      isActive: true,
      updatedAt: '2026-09-08T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 20,
  total: 2,
};

/** One page of links, the shape `MapCrud` answers a list with. */
export const oneLink = {
  items: [
    {
      id: 7,
      ownerDepartment: 'ED',
      visibility: 'Public',
      title: { en: 'Discord', it: 'Discord' },
      url: 'https://example.org/discord',
      category: null,
      sort: 0,
      isActive: true,
      updatedAt: '2026-09-04T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 1,
};

/** One page of the media library, the shape `MapCrud` answers a list with. */
export const oneMedia = {
  items: [
    {
      id: 9,
      ownerDepartment: 'ED',
      visibility: 'Staff',
      fileName: 'banner.png',
      contentType: 'image/png',
      byteSize: 2048,
      width: 640,
      height: 360,
      alt: { en: 'A runway at dawn', it: 'Una pista all alba' },
      category: null,
      url: '/media/9/banner.png',
      createdAt: '2026-09-05T09:00:00Z',
      updatedAt: '2026-09-05T09:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 1,
};

/** The same file as its metadata form loads it. */
export const oneMediaDetail = {
  ...oneMedia.items[0],
  title: null,
  hasFile: true,
  deletedAt: null,
  createdBy: 111111,
  updatedBy: 111111,
  rowVersion: '2026-09-05T09:00:00Z',
};

/**
 * Two calendar entries: one the staff wrote and one a module projected. Two and not one, because
 * what this screen has to get right is the difference — a projection is shown and cannot be edited,
 * and a fixture where every row is the same kind would look correct either way.
 */
export const twoCalendarEntries = {
  items: [
    {
      id: 31,
      ownerDepartment: 'ED',
      visibility: 'Public',
      kind: 'meeting',
      title: { en: 'Staff meeting', it: 'Riunione dello staff' },
      startsAtUtc: '2026-09-15T14:00:00Z',
      endsAtUtc: null,
      allDay: false,
      url: '',
      isProjection: false,
      updatedAt: '2026-09-06T09:00:00Z',
    },
    {
      id: 32,
      ownerDepartment: 'ED',
      visibility: 'Public',
      kind: 'event',
      title: { en: 'Night flight', it: 'Volo notturno' },
      startsAtUtc: '2026-09-20T19:00:00Z',
      endsAtUtc: null,
      allDay: false,
      url: '/events/night-flight',
      isProjection: true,
      updatedAt: '2026-09-06T09:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 2,
};

/** An empty page, for the "which contents use this file" filter of the content list. */
export const noContent = { items: [], page: 1, pageSize: 25, total: 0 };

/**
 * One page of documents: one with a file attached and one without. Two rows and not one, because
 * what the file column has to get right is the difference between them — a column that drew
 * something for every row would look correct on a list where every row has a file.
 */
export const twoDocuments = {
  items: [
    {
      id: 21,
      kind: 'Document',
      slug: 'joining-procedure',
      ownerDepartment: 'ED',
      visibility: 'Public',
      status: 'Published',
      isTemplate: false,
      title: { en: 'Joining procedure', it: 'Procedura di adesione' },
      category: 'guides',
      coverMediaId: null,
      pinned: false,
      sort: 0,
      fileMediaId: 9,
      publishedAt: '2026-09-04T12:00:00Z',
      updatedAt: '2026-09-04T12:00:00Z',
    },
    {
      id: 22,
      kind: 'Document',
      slug: 'read-in-the-browser',
      ownerDepartment: 'ED',
      visibility: 'Public',
      status: 'Published',
      isTemplate: false,
      title: { en: 'Read in the browser', it: 'Si legge nel browser' },
      category: 'guides',
      coverMediaId: null,
      pinned: false,
      sort: 1,
      fileMediaId: null,
      publishedAt: '2026-09-04T12:00:00Z',
      updatedAt: '2026-09-04T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 2,
};

/**
 * A real picture, 8 by 8 and red, so that a test can measure the box a browser gives it. A stub
 * answering a broken image would draw the alternative text instead, and the two look nothing alike
 * on screen but exactly alike to an assertion about text.
 */
const RED_8X8_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
  'base64',
);

/**
 * A published page, for a visitor who is nobody. The body is handed in, because what these tests
 * are about is what a body of blocks *looks like* once a browser has laid it out — which is the one
 * thing neither a unit test nor a screenshot-free assertion can see (implementation plan M1 §A.9).
 */
export async function stubThePublishedPage(page: Page, slug: string, body: unknown): Promise<void> {
  await stubTheApi(page);

  await page.route(`**/api/content/public/Page/${slug}`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        kind: 'Page',
        slug,
        title: { en: 'A page of blocks', it: 'Una pagina di blocchi' },
        summary: null,
        seo: null,
        body,
        schemaVersion: 1,
        version: 1,
        publishedAt: '2026-09-06T10:00:00Z',
      }),
    }),
  );

  // The pictures a block asks for. Served the way Kestrel serves them, at `/media/{id}/{name}`.
  await page.route('**/media/*/**', (route) =>
    route.fulfill({ status: 200, contentType: 'image/png', body: RED_8X8_PNG }),
  );
}

/**
 * What a live data block is answered with. Registered on top of `stubThePublishedPage`, because
 * the catch-all under `/api` answers anything else with a 500 on purpose — a screen that starts
 * calling something new has to say so.
 *
 * The answer is handed in rather than invented here for the same reason the body is: what these
 * tests measure is the shape a browser gives a known answer.
 */
export async function stubTheBlockData(page: Page, type: string, answer: unknown): Promise<void> {
  await page.route(`**/api/blocks/data/${type}**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(answer) }),
  );
}

/**
 * What the search answers with. Registered on top of either stub, because the catch-all under
 * `/api` answers anything else with a 500 on purpose — a screen that starts calling something new
 * has to say so.
 */
export async function stubTheSearch(page: Page, answer: unknown): Promise<void> {
  await page.route('**/api/search**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(answer) }),
  );
}

/**
 * The same stubbing, for a signed in member of the staff: `/api/me` answers with a coordinator and
 * `/api/links` with one page. Anything else under `/api` still fails the test rather than being
 * quietly answered, so a screen that started calling something new says so.
 */
export async function stubTheApiAsStaff(page: Page): Promise<void> {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(staffBootstrap),
    }),
  );

  await page.route('**/api/links**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneLink) }),
  );

  await page.route('**/api/media/*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(oneMediaDetail),
    }),
  );

  await page.route('**/api/media?**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneMedia) }),
  );

  // The content list, answered by `kind`: the documents screen is the one that has rows, because
  // it is the one whose columns this suite is about.
  await page.route('**/api/content**', (route) => {
    const documents = route.request().url().includes('filter%5Bkind%5D=Document');

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(documents ? twoDocuments : noContent),
    });
  });

  // The vocabulary a department files its news and documents under. Empty: a division decides its
  // own shelves and a fresh one has none, which is the state the screens have to survive.
  await page.route('**/api/calendar**', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(twoCalendarEntries),
    }),
  );

  // ⚠️ **After** the entries and not before them, and the order is the whole point: Playwright
  // matches routes in **reverse** registration order, so `**/api/calendar**` — which also matches
  // `/api/calendar-kinds` — would answer this one with a page of entries. It did, and the screen
  // drew two rows of empty cells until this moved down here.
  await page.route('**/api/calendar-kinds**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(theVocabulary) }),
  );

  await page.route('**/api/categories**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(noContent) }),
  );

  // Not under /api, so the guard below never sees it: the file route is served by Kestrel.
  await page.route('**/media/9/**', (route) =>
    route.fulfill({ status: 200, contentType: 'image/png', body: RED_8X8_PNG }),
  );

  await page.route('**/api/**', (route) => {
    const url = route.request().url();
    if (
      url.includes('/api/me') ||
      url.includes('/api/links') ||
      url.includes('/api/media') ||
      url.includes('/api/content') ||
      url.includes('/api/categories') ||
      url.includes('/api/calendar')
    ) {
      return route.fallback();
    }

    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: `Unexpected call in the smoke suite: ${url}` }),
    });
  });
}
