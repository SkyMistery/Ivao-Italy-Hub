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

/**
 * The same coordinator, of the department that owns the site.
 *
 * ⚠️ It exists because the menu is **not** a screen every department has: it belongs to the web
 * team, which is the whole point of the resource (design M1 §8.1), so the ordinary staff fixture —
 * a coordinator of events, on purpose — is answered "this is not for you" there. A test of the menu
 * that used it would be testing the guard.
 */
export const siteStaffBootstrap = {
  ...staffBootstrap,
  user: { ...staffBootstrap.user, departments: ['WD'] },
  permissions: [
    ...staffBootstrap.permissions.filter((permission) => permission.department !== 'ED'),
    { name: 'Menu.View', department: 'WD' },
    { name: 'Menu.Edit', department: 'WD' },
    // ⚠️ Held here and **not** by the ordinary staff fixture, which is what makes the pair useful:
    // every staff member may read a template, and only this one may open the screen that changes
    // them. The events coordinator is the other half of that test.
    { name: 'Content.ManageTemplates', department: 'WD' },
  ],
  navigation: {
    ...staffBootstrap.navigation,
    staff: [{ key: 'nav.menu', path: '/staff/wd/menu', label: null, children: [] }],
  },
};

/**
 * A page nobody would find without asking the server: the hundred and first row, which no single
 * request returns because the list engine caps a page at a hundred. The address of a menu entry is
 * a **closed** set, so a row the form never offers is a row the menu can never point at — which is
 * why the field searches instead of filtering what it already holds.
 */
export const thePageBeyondTheHundredth = {
  items: [
    {
      id: 909,
      kind: 'Page',
      slug: 'oltre-la-centesima',
      ownerDepartment: 'WD',
      visibility: 'Public',
      status: 'Published',
      isTemplate: false,
      title: { en: 'Beyond the hundredth', it: 'Oltre la centesima' },
      category: null,
      coverMediaId: null,
      pinned: false,
      sort: 0,
      fileMediaId: null,
      publishedAt: '2026-09-04T12:00:00Z',
      updatedAt: '2026-09-04T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 100,
  total: 1,
};

/** One page of templates, as the department's templates screen asks for them. */
export const twoTemplates = {
  items: [
    {
      id: 5,
      kind: 'Page',
      slug: 'section-page',
      ownerDepartment: 'WD',
      visibility: 'Staff',
      status: 'Draft',
      isTemplate: true,
      title: { en: 'Section page', it: 'Pagina di sezione' },
      category: null,
      coverMediaId: null,
      pinned: false,
      sort: 0,
      fileMediaId: null,
      publishedAt: null,
      updatedAt: '2026-09-04T12:00:00Z',
    },
    {
      id: 6,
      kind: 'Document',
      slug: 'policy',
      ownerDepartment: 'WD',
      visibility: 'Staff',
      status: 'Draft',
      isTemplate: true,
      title: { en: 'Policy', it: 'Regolamento' },
      category: null,
      coverMediaId: null,
      pinned: false,
      sort: 0,
      fileMediaId: null,
      publishedAt: null,
      updatedAt: '2026-09-04T12:00:00Z',
    },
  ],
  page: 1,
  pageSize: 25,
  total: 2,
};

/** The first of them in full, as the editor loads it. */
export const oneTemplate = {
  ...twoTemplates.items[0],
  summary: null,
  seo: null,
  templateId: null,
  body: { schemaVersion: 1, sections: [] },
  schemaVersion: 1,
  createdAt: '2026-09-04T12:00:00Z',
  rowVersion: '2026-09-04T12:00:00',
};

/** One entry of the site menu, as the list answers and as the detail answers. */
export const oneMenuItem = {
  id: 3,
  scope: 'Public',
  parentId: null,
  label: { en: 'Pilots', it: 'Piloti' },
  path: '/pilots',
  sort: 20,
  visibility: 'Public',
  isActive: true,
  createdAt: '2026-09-01T10:00:00Z',
  createdBy: 111111,
  updatedAt: '2026-09-06T09:00:00Z',
  updatedBy: 111111,
  rowVersion: '2026-09-06T09:00:00',
};

export const oneMenuPage = { items: [oneMenuItem], page: 1, pageSize: 20, total: 1 };

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
export async function stubTheApiAsStaff(page: Page, bootstrap: unknown = staffBootstrap): Promise<void> {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(bootstrap),
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

  // The content list, answered by what it was asked for: the documents screen is the one that has
  // rows, because it is the one whose columns this suite is about — and since the templates screen
  // exists, two more questions arrive at the same address.
  await page.route('**/api/content**', (route) => {
    const url = route.request().url();

    const answer = url.includes('filter%5BisTemplate%5D=true')
      ? twoTemplates
      : // "How many rows were made from this template?" — a page of one, read for its `total`.
        url.includes('filter%5BtemplateId%5D=')
        ? { ...noContent, total: 4 }
        : // ⚠️ A search, and the only way to reach the row it answers with: the unfiltered call
          // below returns nothing, exactly as a real first page of a hundred returns everything
          // except what is past it.
          url.includes('q=oltre')
          ? thePageBeyondTheHundredth
          : url.includes('filter%5Bkind%5D=Document')
            ? twoDocuments
            : noContent;

    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(answer) });
  });

  // ⚠️ **After** the list, so that it wins for a single row: Playwright matches in reverse
  // registration order, and without this the editor of a template would be handed a page of rows
  // where it expects one. The publish problems of a row are a segment deeper and get their own.
  await page.route('**/api/content/*', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneTemplate) }),
  );

  await page.route('**/api/content/*/publish-problems', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ errors: {}, localized: {} }),
    }),
  );

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
  await page.route('**/api/menu**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(oneMenuPage) }),
  );

  // After the list, so that it wins for a single row: the detail a cell reads before it writes, and
  // the write itself, which answers with the row as it now stands.
  await page.route('**/api/menu/*', async (route) => {
    const request = route.request();

    if (request.method() === 'PUT') {
      const sent = JSON.parse(request.postData() ?? '{}') as Record<string, unknown>;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...oneMenuItem, ...sent }),
      });
    }

    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(oneMenuItem),
    });
  });

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
      url.includes('/api/calendar') ||
      url.includes('/api/menu')
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
