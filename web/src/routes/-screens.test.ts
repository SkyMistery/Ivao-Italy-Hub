import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

/**
 * The pages of the site written in code (design `13-design-struttura-del-sito.md` §3.1): the server declares them once —
 * the core's in `SiteDestinations.CoreScreens`, each module's in its `SiteScreens` — and the bootstrap carries them to
 * the browser, which offers them in the menu, in the address fields of the blocks and in the instructions for Claude.
 * Until 4.1 the browser kept a copy of its own, agreed by hand. Now there is nothing to agree with, so what is left to
 * check is that each declared screen is an address this browser answers: a screen the router does not know would be
 * offered everywhere and lead to «not found». This reads the server's sources, as `-reserved.test.ts` does.
 */

const repository = resolve(process.cwd(), '..');

function screensOfTheCore(): string[] {
  const source = readFileSync(resolve(repository, 'src/IvaoHub.Core/Content/SiteDestinations.cs'), 'utf8');
  const block = /CoreScreens\s*=\s*\[([\s\S]*?)\];/.exec(source)?.[1];
  expect(block, 'SiteDestinations.CoreScreens is no longer a literal list').toBeDefined();
  return [...block!.matchAll(/new\("([^"]+)"/g)].map((match) => match[1]!);
}

/** Every module's screens, by the folder of the module, from the one line each module writes. */
function screensOfTheModules(): { module: string; path: string }[] {
  const source = resolve(repository, 'src');
  const screens: { module: string; path: string }[] = [];

  for (const folder of readdirSync(source).filter((name) => name.startsWith('IvaoHub.Modules.'))) {
    for (const file of readdirSync(resolve(source, folder)).filter((name) => name.endsWith('Module.cs'))) {
      const text = readFileSync(resolve(source, folder, file), 'utf8');
      const line = /SiteScreens\s*=>\s*\[([\s\S]*?)\];/.exec(text)?.[1];
      for (const match of line?.matchAll(/new\("([^"]+)"/g) ?? []) {
        screens.push({ module: folder, path: match[1]! });
      }
    }
  }

  return screens;
}

/** The addresses of the router's own tree; an index route is written with its last slash (`/news/`). */
function routesOfTheTree(): string[] {
  const tree = readFileSync(resolve(process.cwd(), 'src/routeTree.gen.ts'), 'utf8');
  const block = /interface FileRoutesByFullPath\s*\{([\s\S]*?)\}/.exec(tree)?.[1];
  expect(block, 'the generated route tree has no FileRoutesByFullPath').toBeDefined();
  return [...block!.matchAll(/'([^']+)':/g)].map((match) =>
    match[1] === '/' ? '/' : match[1]!.replace(/\/$/, ''),
  );
}

/** The public routes the modules' manifests declare: `area: 'public'` followed by its `path`. */
function publicRoutesOfTheModules(): string[] {
  const modules = resolve(process.cwd(), 'src/modules');
  return readdirSync(modules, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => {
      const manifest = readFileSync(resolve(modules, entry.name, 'index.ts'), 'utf8');
      return [...manifest.matchAll(/area:\s*'public',\s*path:\s*'([^']+)'/g)].map((match) => match[1]!);
    });
}

test("every screen of the core is a route of the browser's tree", () => {
  const routes = routesOfTheTree();
  const screens = screensOfTheCore();

  expect(screens).toContain('/');
  expect(screens.filter((path) => !routes.includes(path))).toEqual([]);
});

test('every screen a module declares is one of its public routes', () => {
  const routes = publicRoutesOfTheModules();
  const screens = screensOfTheModules();

  expect(screens.length).toBeGreaterThan(0);
  expect(screens.filter((screen) => !routes.includes(screen.path))).toEqual([]);
});
