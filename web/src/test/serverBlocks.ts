import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * The C# half of every block, as text: what `new BlockDescriptor(…)` says of each type, comments left out, in the core's
 * `BlockRegistry.cs` and in every module's `*Module.cs`. For the tests that hold a declared exception of the C# half to its
 * schema: `RequiredMedia` (`app/blockRequiredMedia.test.ts`) and `FramedAddress` (`app/blockFramedAddress.test.ts`).
 *
 * It is read with a narrow pattern, like `modules/manifest.test.ts`; a type it cannot name is reported as `unread in …`,
 * so a test that asks every registered block to be read fails rather than concluding that a block declares nothing.
 */

// Vitest runs with `web/` as the working directory.
const source = resolve(process.cwd(), '../src').split('\\').join('/');

/** What `new BlockDescriptor(…)` says of each block in one C# file, up to the next one. */
function descriptorsIn(path: string): [string, string][] {
  const code = readFileSync(path, 'utf8').replace(/^\s*\/\/.*$/gm, '');
  const constants = new Map(
    [...code.matchAll(/public const string (\w+) = "([^"]+)";/g)].map((match) => [match[1]!, match[2]!]),
  );

  return code
    .split('new BlockDescriptor(')
    .slice(1)
    .map((descriptor) => {
      const named = /^\s*(?:"([^"]+)"|(\w+))/.exec(descriptor);
      return [named?.[1] ?? constants.get(named?.[2] ?? '') ?? `unread in ${path}`, descriptor];
    });
}

/** Every block the server declares, by type, with the text of its descriptor. */
export function readServerDescriptors(): Map<string, string> {
  const modules = readdirSync(source, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.startsWith('IvaoHub.Modules.'))
    .flatMap((project) =>
      readdirSync(`${source}/${project.name}`, { withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name.endsWith('Module.cs'))
        .map((entry) => `${source}/${project.name}/${entry.name}`),
    );

  return new Map([`${source}/IvaoHub.Core/Content/BlockRegistry.cs`, ...modules].flatMap(descriptorsIn));
}
