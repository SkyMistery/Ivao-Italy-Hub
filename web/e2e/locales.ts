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
  readonly home: { readonly heading: string };
  readonly footer: { readonly version: string };
  readonly theme: { readonly toggle: string };
  readonly auth: { readonly login: string };
  readonly common: { readonly edit: string };
  readonly list: { readonly file: string };
  readonly calendar: {
    readonly title: string;
    readonly create: string;
    readonly projected: string;
    readonly fields: { readonly kind: string };
  };
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
    readonly options: { readonly visibility: { readonly Public: string } };
    readonly editor: {
      readonly saveDraft: string;
      readonly publish: string;
      readonly applyBlock: string;
      readonly addBlock: string;
    };
  };
  readonly blocks: {
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

/** The titles the system templates are seeded with, which is what the template picker shows. */
interface SeedStrings {
  readonly seed: {
    readonly templates: Readonly<
      Record<string, { readonly title: string; readonly hero: { readonly heading: string } }>
    >;
  };
}

export const englishCommon = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
) as CommonStrings;

export const englishSeed = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/seed.json', import.meta.url)), 'utf8'),
) as SeedStrings;
