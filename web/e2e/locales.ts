import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/**
 * The English language file of the division, read from disk.
 *
 * Read rather than imported because Playwright's ESM loader wants an import attribute for JSON that
 * the rest of the toolchain does not; and read rather than retyped because a test carrying its own
 * copy of a sentence passes while the screen shows a raw key. It is the same file the browser
 * fetches from `/locales/en/common.json`.
 */
interface CommonStrings {
  readonly footer: { readonly rights: string };
  readonly theme: { readonly toggle: string };
  readonly nav: {
    readonly staff: string;
    readonly modules: string;
    readonly sidebar: { readonly collapse: string; readonly expand: string };
  };
  readonly auth: { readonly login: string };
  readonly common: { readonly edit: string; readonly save: string; readonly delete: string };
  readonly forbidden: { readonly title: string };
  readonly departments: { readonly WD: string; readonly ED: string };
  readonly liveStatus: { readonly title: string; readonly updatedAt: string };
  readonly search: {
    readonly title: string;
    readonly label: string;
    readonly open: string;
    readonly empty: string;
    readonly termsTooShort: string;
  };
  readonly menu: {
    readonly title: string;
    readonly create: string;
    readonly fields: {
      readonly label: string;
      readonly path: string;
      readonly sort: string;
      readonly visibility: string;
    };
    readonly screensGroup: string;
    readonly linksGroup: string;
    readonly screens: Readonly<Record<string, string>>;
  };
  readonly form: { readonly suggest: { readonly emptyClosed: string } };
  readonly dashboard: { readonly edit: string };
  readonly list: { readonly file: string };
  readonly calendar: {
    readonly title: string;
    readonly create: string;
    readonly projected: string;
    readonly fields: { readonly kind: string };
  };
  readonly calendarKinds: { readonly title: string };
  readonly admin: { readonly title: string };
  readonly links: {
    readonly title: string;
    readonly create: string;
    readonly fields: { readonly url: string };
  };
  readonly media: {
    readonly title: string;
    readonly upload: string;
    readonly fields: { readonly category: string };
  };
  readonly notFound: { readonly title: string };
  readonly news: { readonly title: string; readonly create: string };
  readonly documents: { readonly title: string };
  readonly categories: {
    readonly title: string;
    readonly create: string;
    readonly fields: { readonly key: string };
  };
  readonly content: {
    readonly title: string;
    readonly create: string;
    readonly slugPlaceholder: string;
    readonly fields: {
      readonly template: string;
      readonly slug: string;
      readonly visibility: string;
      readonly pinned: string;
    };
    readonly options: {
      readonly visibility: { readonly Public: string };
      readonly kind: { readonly Page: string; readonly Document: string };
    };
    readonly section: {
      readonly fields: { readonly key: string; readonly allowedBlocks: string };
    };
    readonly editor: {
      readonly saveDraft: string;
      readonly publish: string;
      readonly publishDialog: {
        readonly changelog: string;
        readonly airac: string;
        readonly confirm: string;
      };
      readonly page: string;
      readonly setKey: string;
      readonly addBlock: string;
      readonly addSection: string;
      readonly components: string;
      readonly outline: string;
      readonly onThePage: string;
      readonly componentsHint: string;
      readonly structure: string;
      readonly properties: string;
      readonly preview: string;
      readonly previewWidths: { readonly phone: string; readonly desktop: string };
      readonly autosave: { readonly title: string; readonly saved: string; readonly unsaved: string };
      readonly dropHere: string;
      readonly remove: string;
      readonly removeSection: string;
      readonly reorder: string;
      readonly template: {
        readonly differences: string;
        readonly added: string;
        readonly apply: { readonly added: string; readonly removed: string };
      };
    };
  };
  readonly templates: { readonly title: string; readonly create: string };
  readonly blocks: {
    readonly groups: {
      readonly content: string;
      readonly layout: string;
      readonly interactive: string;
      readonly structure: string;
      readonly data: string;
    };
    readonly subgroups: {
      readonly text: string;
      readonly media: string;
      readonly tables: string;
      readonly grids: string;
      readonly containers: string;
    };
    readonly networkStats: {
      // Spelled out rather than an index signature: a caption read from a record is
      // `string | undefined`, and a spec asserting on `undefined` is a spec asserting on nothing.
      readonly captions: { readonly divisionAtc: string; readonly divisionPilots: string };
    };
    readonly staffList: { readonly rosterNote: string };
    readonly heading: {
      readonly label: string;
      readonly fields: { readonly text: string; readonly level: string };
      readonly options: { readonly level: Readonly<Record<string, string>> };
    };
    readonly text: { readonly label: string; readonly fields: { readonly markdown: string } };
    readonly callout: {
      readonly label: string;
      readonly fields: { readonly tone: string; readonly title: string; readonly text: string };
      readonly options: { readonly tone: Readonly<Record<string, string>> };
    };
  };
}

/**
 * The words the system templates and the seeded pages are written with. They are what the template
 * picker shows, and — since M1 G8 — what a visitor actually reads on a page nobody has rewritten
 * yet, so a spec asserting on a seeded page reads them from here rather than retyping them.
 */
interface SeedStrings {
  readonly seed: {
    readonly templates: Readonly<
      Record<
        string,
        {
          readonly title: string;
          readonly hero?: { readonly heading: string };
          readonly welcome?: { readonly heading: string };
          /** The free section of a template, which is the one a page adds its own blocks to. */
          readonly body?: { readonly section: string };
        }
      >
    >;
    readonly pages: Readonly<
      Record<string, { readonly title: string; readonly intro?: { readonly heading: string } }>
    >;
  };
}

export const englishCommon = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
) as CommonStrings;

export const englishSeed = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/seed.json', import.meta.url)), 'utf8'),
) as SeedStrings;
