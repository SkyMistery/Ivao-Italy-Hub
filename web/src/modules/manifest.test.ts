import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, test } from 'vitest';

import { registry } from '../app/registry';
import { compareRegistries, registriesAgree } from '../features/admin/registryDiff';
import type { Bootstrap } from '../shared/api/bootstrap';

import { moduleManifests } from './index';

/**
 * "server ⇄ manifest ⇄ ui-kit" (design M0 §6.5).
 *
 * A module is two projects compiled separately, `IvaoHub.Modules.<Name>` and
 * `web/src/modules/<key>/`, and they can disagree without anything failing to build. The way they
 * disagree is quiet and late: a block only the server knows draws "unknown block" on a page
 * somebody already published, and a block only the browser knows is offered in the editor and then
 * refused on save. So the two sides are compared here, by reading them.
 *
 * The C# is read with two deliberately narrow patterns, and the test fails when a pattern stops
 * matching rather than concluding that a module declares nothing. That is the convention a module
 * signs up to: its key and its blocks are written as literals in its own `*Module.cs`.
 */

// Vitest runs with `web/` as the working directory, and `import.meta.url` is not a file URL under
// jsdom, so the repository is found from there rather than from this file.
const repositoryRoot = resolve(process.cwd(), '..').split('\\').join('/');
const modulesRoot = `${repositoryRoot}/src`;

interface ServerModule {
  readonly project: string;
  readonly key: string;
  readonly blocks: string[];
}

/** `public const string ModuleKey = "events";` */
const KEY = /ModuleKey\s*=\s*"([^"]+)"/;

/** `new BlockDescriptor("events.upcoming", …)` — a module names its blocks after itself. */
const BLOCK = /new BlockDescriptor\(\s*"([^"]+)"/g;

function readServerModules(): ServerModule[] {
  const projects = readdirSync(modulesRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.startsWith('IvaoHub.Modules.'))
    .map((entry) => entry.name);

  return projects.map((project) => {
    const sources = readdirSync(`${modulesRoot}/${project}`, { withFileTypes: true })
      .filter((entry) => entry.isFile() && entry.name.endsWith('Module.cs'))
      .map((entry) => readFileSync(`${modulesRoot}/${project}/${entry.name}`, 'utf8'));

    const source = sources.join('\n');
    const key = KEY.exec(source)?.[1];

    expect(
      key,
      `${project} declares no ModuleKey literal; see the convention at the top of this file`,
    ).toBeDefined();

    return {
      project,
      key: key!,
      blocks: [...source.matchAll(BLOCK)].map((match) => match[1]!),
    };
  });
}

const serverModules = readServerModules();

describe('the two halves of every module', () => {
  test('the same modules are listed on both sides', () => {
    expect([...moduleManifests].map((manifest) => manifest.key).sort()).toEqual(
      serverModules.map((module) => module.key).sort(),
    );
  });

  test.each(serverModules)('$key declares the same blocks on both sides', (module) => {
    const manifest = moduleManifests.find((candidate) => candidate.key === module.key);
    expect(manifest, `no manifest under web/src/modules/${module.key}/`).toBeDefined();

    expect(manifest!.blocks.map((block) => block.type).sort()).toEqual([...module.blocks].sort());
  });

  test.each(serverModules)('$key brings a language file for every language of the division', (module) => {
    const locales = readdirSync(`${repositoryRoot}/locales`, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name);

    for (const namespace of moduleManifests.find((candidate) => candidate.key === module.key)!
      .i18nNamespaces) {
      for (const locale of locales) {
        // `pnpm i18n:sync` is what puts them there, and CI fails on a diff: a namespace declared
        // and never synced would be a menu entry showing its own key.
        const copied = readFileSync(`${repositoryRoot}/locales/${locale}/${namespace}.json`, 'utf8');
        expect(JSON.parse(copied)).toHaveProperty('_source', `web/src/modules/${module.key}/locales`);
      }
    }
  });
});

/**
 * The kinds a module projects into the search, read from its C# the way its blocks are.
 *
 * `new SearchProjection(Kind: "tour", …)`, or a constant of the module in place of the literal
 * (`Event.SearchKind`, itself `EventsModule.ModuleKey`): a name is followed, by its last segment,
 * through the `const string` declarations of the same module until a literal is found. A kind
 * written any other way fails the test rather than passing unread.
 */
const SEARCH_PROJECTION = /new SearchProjection\(\s*(?:Kind:\s*)?([^,)]+?)\s*[,)]/g;

function readSources(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.cs'))
    .map((entry) => `${entry.parentPath.split('\\').join('/')}/${entry.name}`)
    .filter((file) => !/\/(Migrations|bin|obj)\//.test(file))
    .map((file) => readFileSync(file, 'utf8'));
}

function readSearchKinds(project: string): string[] {
  const source = readSources(`${modulesRoot}/${project}`).join('\n');

  const literal = (expression: string, hops = 0): string | undefined => {
    const quoted = /^"([^"]+)"$/.exec(expression);
    if (quoted) return quoted[1];
    if (hops > 3 || !/^[\w.]+$/.test(expression)) return undefined;

    const name = expression.split('.').at(-1)!;
    const declared = new RegExp(`const string ${name}\\s*=\\s*([^;]+?)\\s*;`).exec(source)?.[1];
    return declared === undefined ? undefined : literal(declared, hops + 1);
  };

  return [
    ...new Set(
      [...source.matchAll(SEARCH_PROJECTION)].map((match) => {
        const kind = literal(match[1]!);
        expect(
          kind,
          `${project} projects into the search with the kind \`${match[1]}\`, which this test cannot read: ` +
            'write it as a literal, or as a `const string` of the module',
        ).toBeDefined();
        return kind!;
      }),
    ),
  ];
}

describe('the words of what a module puts in the search', () => {
  test('the tours project a kind, so the pattern still reads the C#', () => {
    // Without this, a renamed `SearchProjection` would make every module "project nothing" and the
    // test below would pass by reading nothing.
    expect(readSearchKinds('IvaoHub.Modules.FlightOps')).toEqual(['tour']);
  });

  test.each(serverModules)('$key names every kind it projects, in every language', (module) => {
    const locales = readdirSync(`${repositoryRoot}/locales`, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name);

    for (const kind of readSearchKinds(module.project)) {
      for (const locale of locales) {
        // The result of a search asks `<module>:search.kinds.<kind>` (`features/search/kindLabel.ts`):
        // a kind with no word there is a badge showing its own key.
        const file = `locales/${locale}/${module.key}.json`;
        const words = JSON.parse(readFileSync(`${repositoryRoot}/${file}`, 'utf8')) as {
          search?: { kinds?: Record<string, unknown> };
        };

        expect(
          words.search?.kinds?.[kind],
          `${file} has no "search.kinds.${kind}": add it under web/src/modules/${module.key}/locales/ and run pnpm i18n:sync`,
        ).toEqual(expect.stringMatching(/\S/));
      }
    }
  });
});

describe('the third side, the gallery', () => {
  test('a registry that agrees with the server shows nothing to fix', () => {
    // What the server would answer for this build: every block and tile the composed registry
    // holds. The real comparison happens in the browser against `/api/me`; this fixes the shape of
    // the answer, so that a difference is reported rather than silently formatted away.
    const asServerWouldSay = bootstrapWith(
      registry.blocks.map((block) => ({
        type: block.type,
        version: block.version,
        kind: block.kind,
        alwaysLive: block.alwaysLive ?? false,
      })),
    );

    expect(registriesAgree(compareRegistries(asServerWouldSay, registry.blocks))).toBe(true);
  });

  test('a block the server knows and this build cannot draw is reported', () => {
    const difference = compareRegistries(
      bootstrapWith([{ type: 'events.upcoming', version: 1, kind: 'Data', alwaysLive: true }]),
      registry.blocks,
    );

    expect(difference.blocksMissingInBrowser).toEqual(['events.upcoming']);
    expect(difference.blocksMissingOnServer).toEqual(registry.blocks.map((block) => block.type));
  });
});

/** Only the part of the bootstrap the comparison reads; the rest is not this test's business. */
function bootstrapWith(blocks: Bootstrap['registries']['blocks']): Bootstrap {
  return { registries: { blocks, permissions: [] } } as unknown as Bootstrap;
}
