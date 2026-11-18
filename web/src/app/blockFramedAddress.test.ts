import { expect, test } from 'vitest';
import { z } from 'zod';

import { readFields } from '../shared/forms';
import { readServerDescriptors } from '../test/serverBlocks';

import { registry } from './registry';

/**
 * The field of a block that holds an address it frames is said twice, and this test is what keeps that safe. The schema
 * says it with `.meta({ framed: true })`; the C# half says it in `FramedAddress`, because publication has to refuse an
 * address the block would not frame, or none (#524), and the server reads no schema. That is the second declared
 * exception to "the back end never replicates the schemas in C#", next to `RequiredMedia`: this one field and this one
 * purpose (Carmine, 18 November 2026, "one JSON", note 2026-11-18-l-indirizzo-che-l-hub-non-incorpora).
 *
 * The list of hosts itself is not said twice: it is `blocks/embedHosts.json`, read by both sides.
 */

interface Registered {
  readonly type: string;
  readonly schema: z.ZodType;
}

/** The framed field of every block that has one, according to its schema. */
function framedBySchemas(blocks: readonly Registered[]): Record<string, string[]> {
  return Object.fromEntries(
    blocks
      .map(
        (block) =>
          [
            block.type,
            readFields(block.schema)
              .filter((field) => field.meta.framed === true)
              .map((field) => field.path),
          ] as const,
      )
      .filter(([, fields]) => fields.length > 0),
  );
}

const server = new Map(
  [...readServerDescriptors()].map(([type, descriptor]) => [
    type,
    /FramedAddress:\s*"([^"]+)"/.exec(descriptor)?.[1] ?? null,
  ]),
);

function framedByServer(): Record<string, string[]> {
  return Object.fromEntries(
    [...server].flatMap(([type, field]) => (field === null ? [] : [[type, [field]] as const])),
  );
}

test('every block this build registers is read from the C#, or the comparison below would prove nothing', () => {
  expect(registry.blocks.map((block) => block.type).filter((type) => !server.has(type))).toEqual([]);
});

test('the server declares exactly the framed addresses the schemas mark', () => {
  expect(framedByServer()).toEqual(framedBySchemas(registry.blocks));
});

test('the exception stays as narrow as it was decided: the address of a video and of an embed', () => {
  expect(framedBySchemas(registry.blocks)).toEqual({ embed: ['url'], video: ['url'] });
});

test('a new block with a framed address that the C# does not declare is a disagreement', () => {
  const schema = z.object({ src: z.string().meta({ framed: true }) });
  const blocks = [...registry.blocks, { type: 'made.up', schema }];

  expect(framedBySchemas(blocks)['made.up']).toEqual(['src']);
  expect(framedByServer()).not.toEqual(framedBySchemas(blocks));
});
