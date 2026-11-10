import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';
import { z } from 'zod';

import { readFields, type FieldNode } from '../shared/forms';

import { registry } from './registry';

/**
 * A picture a block cannot do without is said twice, and this test is what keeps that safe. The schema says it by not
 * being `.optional()`; the C# half says it in `RequiredMedia`, because a key that is not there cannot be found by
 * walking a saved body, and publication has to refuse it. That is a declared exception to "the back end never
 * replicates the schemas in C#", for this one fact and this one purpose (note 2026-11-07-la-foto-rimasta-da-scegliere),
 * and this comparison is part of the decision: it fails when either side changes, and it fails for a new block with a
 * required picture the C# does not declare.
 *
 * The C# is read with a narrow pattern, like `modules/manifest.test.ts`, and the test fails when the pattern stops
 * finding a block rather than concluding that the block declares nothing.
 */

interface Registered {
  readonly type: string;
  readonly schema: z.ZodType;
}

// Vitest runs with `web/` as the working directory.
const source = resolve(process.cwd(), '../src').split('\\').join('/');

/** `images[].mediaId`, as `RequiredMedia` writes it: the required pictures of one schema. */
function picturesOf(fields: readonly FieldNode[], prefix = ''): string[] {
  return fields.flatMap((field) => {
    const name = field.path.split('.').at(-1)!;

    if (field.kind === 'list') {
      return picturesOf(field.children, `${prefix}${name}[].`);
    }

    if (field.kind === 'object') {
      return picturesOf(field.children, `${prefix}${name}.`);
    }

    return field.meta.media === true && !field.optional ? [`${prefix}${name}`] : [];
  });
}

/** Every block with a required picture, according to its schema. */
function requiredBySchemas(blocks: readonly Registered[]): Record<string, string[]> {
  return Object.fromEntries(
    blocks
      .map((block) => [block.type, picturesOf(readFields(block.schema)).sort()] as const)
      .filter(([, pictures]) => pictures.length > 0),
  );
}

/** What `new BlockDescriptor(…)` says of each block in one C# file, comments left out. */
function descriptorsIn(path: string): Map<string, string[]> {
  const code = readFileSync(path, 'utf8').replace(/^\s*\/\/.*$/gm, '');
  const constants = new Map(
    [...code.matchAll(/public const string (\w+) = "([^"]+)";/g)].map((match) => [match[1]!, match[2]!]),
  );

  return new Map(
    code
      .split('new BlockDescriptor(')
      .slice(1)
      .map((descriptor) => {
        const named = /^\s*(?:"([^"]+)"|(\w+))/.exec(descriptor);
        const type = named?.[1] ?? constants.get(named?.[2] ?? '') ?? `unread in ${path}`;
        // Up to the bracket that closes the list: a path has brackets of its own (`images[].mediaId`).
        const declared = /RequiredMedia:\s*\[((?:\s*"[^"]*"\s*,?)*)\s*\]/.exec(descriptor)?.[1] ?? '';

        return [type, [...declared.matchAll(/"([^"]+)"/g)].map((match) => match[1]!).sort()];
      }),
  );
}

function readServerBlocks(): Map<string, string[]> {
  const modules = readdirSync(source, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.startsWith('IvaoHub.Modules.'))
    .flatMap((project) =>
      readdirSync(`${source}/${project.name}`, { withFileTypes: true })
        .filter((entry) => entry.isFile() && entry.name.endsWith('Module.cs'))
        .map((entry) => `${source}/${project.name}/${entry.name}`),
    );

  return new Map(
    [`${source}/IvaoHub.Core/Content/BlockRegistry.cs`, ...modules].flatMap((path) => [
      ...descriptorsIn(path),
    ]),
  );
}

function requiredByServer(server: ReadonlyMap<string, string[]>): Record<string, string[]> {
  return Object.fromEntries([...server].filter(([, pictures]) => pictures.length > 0));
}

const server = readServerBlocks();

test('every block this build registers is read from the C#, or the comparison below would prove nothing', () => {
  expect(registry.blocks.map((block) => block.type).filter((type) => !server.has(type))).toEqual([]);
});

test('the server declares exactly the pictures the schemas require', () => {
  expect(requiredByServer(server)).toEqual(requiredBySchemas(registry.blocks));
});

test('the exception stays as narrow as it was decided: these three blocks', () => {
  expect(requiredBySchemas(registry.blocks)).toEqual({
    gallery: ['images[].mediaId'],
    image: ['mediaId'],
    logoGrid: ['items[].mediaId'],
  });
});

test.each<[string, z.ZodType, string]>([
  ['a picture of its own', z.object({ mediaId: z.number().int().meta({ media: true }) }), 'mediaId'],
  [
    'a picture in each entry of a list',
    z.object({ slides: z.array(z.object({ mediaId: z.number().int().meta({ media: true }) })) }),
    'slides[].mediaId',
  ],
])('a new block with %s that the C# does not declare is a disagreement', (_, schema, path) => {
  const blocks = [...registry.blocks, { type: 'made.up', schema }];

  expect(requiredBySchemas(blocks)['made.up']).toEqual([path]);
  expect(requiredByServer(server)).not.toEqual(requiredBySchemas(blocks));
});

test('a new block whose picture is optional asks nothing of the C#', () => {
  const schema = z.object({ mediaId: z.number().int().optional().meta({ media: true }) });

  expect(requiredByServer(server)).toEqual(
    requiredBySchemas([...registry.blocks, { type: 'made.up', schema }]),
  );
});
