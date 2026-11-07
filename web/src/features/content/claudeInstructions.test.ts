import { describe, expect, test } from 'vitest';

import { registry } from '../../app/registry';
import type { Body } from '../../blocks';

import {
  bodyFingerprint,
  canonicalJson,
  CLAUDE_FORMAT,
  composeInstructions,
  envelopeSchema,
  instructionsHeader,
  type InstructionsInput,
} from './claudeInstructions';
import type { SiteAddress } from './siteAddresses';
import type { SectionRule } from './templateRules';

/**
 * «Download for Claude» (PR 3.0a; note 2026-10-30-una-pagina-scritta-con-claude §3, §4). What is checked is what the
 * import of PR 3.0b will lean on: the header and its two fingerprints — which rules they cover and which they do
 * not — and that the instructions say what the page offers and nothing it does not.
 */

const block = (type: string) => {
  const found = registry.blocks.find((each) => each.type === type);
  if (found === undefined) {
    throw new Error(`No block ${type}`);
  }
  return found;
};

const body: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 'sec-intro',
      key: 'intro',
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [
        {
          id: 'blk-text',
          type: 'text',
          version: 1,
          props: { markdown: { en: 'Hello', xx: 'Hallo' } },
          renderMode: null,
          frozen: null,
          column: null,
          span: null,
          source: null,
        },
      ],
      sections: [],
    },
  ],
};

const addresses: SiteAddress[] = [
  {
    id: 1,
    path: '/about',
    label: 'About',
    group: 'Web',
    kind: 'page',
    published: true,
    visibility: 'Public',
  },
  {
    id: 2,
    path: '/secret-draft',
    label: 'Draft',
    group: 'Web',
    kind: 'page',
    published: false,
    visibility: 'Public',
  },
  {
    id: null,
    path: '/news',
    label: 'News',
    group: 'Screens',
    kind: 'screen',
    published: true,
    visibility: 'Public',
  },
  {
    id: 3,
    path: '/news/launch',
    label: 'Launch',
    group: 'Events',
    kind: 'news',
    published: true,
    visibility: 'Public',
  },
  {
    id: null,
    path: 'https://forum.example',
    label: 'Forum',
    group: 'Links',
    kind: 'link',
    published: true,
    visibility: 'Members',
  },
];

function input(overrides: Partial<InstructionsInput> = {}): InstructionsInput {
  return {
    content: { id: 412, kind: 'Page' },
    rowVersion: '2026-10-31T09:12:44.0000000',
    title: { en: 'Welcome', xx: 'Willkommen' },
    summary: null,
    body,
    visibility: 'Public',
    locales: ['en', 'xx'],
    blocks: [block('heading'), block('text'), block('image')],
    template: new Map<string, SectionRule>([
      ['intro', { required: true, locked: false, allowedBlocks: ['text'] }],
    ]),
    addresses,
    embedGuidelines: null,
    words: { blockName: (each) => `name of ${each.type}`, blockAbout: (each) => `about ${each.type}` },
    style: null,
    ...overrides,
  };
}

describe('the fingerprints', () => {
  test('canonical JSON does not depend on the order the keys were written in', () => {
    expect(canonicalJson({ b: 1, a: { d: [1, { f: 2, e: 3 }], c: null } })).toBe(
      canonicalJson({ a: { c: null, d: [1, { e: 3, f: 2 }] }, b: 1 }),
    );
    expect(canonicalJson({ a: undefined, b: 1 })).toBe('{"b":1}');
  });

  test('a body read with its defaults has the fingerprint of the same body written without them', async () => {
    const sparse = {
      schemaVersion: 1,
      sections: [
        {
          id: 'sec-intro',
          key: 'intro',
          blocks: [{ id: 'blk-text', type: 'text', props: { markdown: { xx: 'Hallo', en: 'Hello' } } }],
        },
      ],
    };

    expect(await bodyFingerprint(sparse)).toBe(await bodyFingerprint(body));
    expect(await bodyFingerprint(body)).toMatch(/^sha256:[0-9a-f]{64}$/);
  });

  test('a body that says something else has another fingerprint', async () => {
    const changed = structuredClone(body);
    (changed.sections[0]!.blocks[0]!.props as { markdown: Record<string, string> }).markdown.en = 'Goodbye';

    expect(await bodyFingerprint(changed)).not.toBe(await bodyFingerprint(body));
  });

  test('the rules are the same in every language of the screen, and whatever the style says', async () => {
    const english = await instructionsHeader(input());
    const other = await instructionsHeader(
      input({
        words: { blockName: () => 'Name', blockAbout: () => 'Wozu' },
        style: 'Write short sentences.',
        addresses: [],
      }),
    );

    expect(other.instructions.rules).toBe(english.instructions.rules);
  });

  test.each<[string, Partial<InstructionsInput>]>([
    ['the readers', { visibility: 'Staff' }],
    ['the languages', { locales: ['en'] }],
    ['the kind', { content: { id: 412, kind: 'News' } }],
    ['the blocks offered', { blocks: [block('heading'), block('text')] }],
    ['the template', { template: new Map() }],
    ["a block's version", { blocks: [block('heading'), { ...block('text'), version: 2 }, block('image')] }],
  ])('the rules change with %s', async (_what, change) => {
    expect((await instructionsHeader(input(change))).instructions.rules).not.toBe(
      (await instructionsHeader(input())).instructions.rules,
    );
  });

  test('the header names the row, its kind and the draft', async () => {
    const header = await instructionsHeader(input());

    expect(header).toMatchObject({
      format: CLAUDE_FORMAT,
      version: 1,
      instructions: { content: 412, kind: 'Page' },
      draft: { rowVersion: '2026-10-31T09:12:44.0000000', body: await bodyFingerprint(body) },
    });
  });
});

describe('what the instructions offer', () => {
  test('the envelope does not ask a new section or block for the identifier it must not have', () => {
    const schema = envelopeSchema() as {
      $defs: Record<
        string,
        { required?: string[]; properties: { blocks: { items: { required?: string[] } } } }
      >;
    };
    const [section] = Object.values(schema.$defs);

    expect(section?.required ?? []).not.toContain('id');
    expect(section?.properties.blocks.items.required).toEqual(['type']);
  });

  test("the envelope of a page leaves out the template's rules and keeps what a block of the draft carries", () => {
    const text = JSON.stringify(envelopeSchema());

    for (const member of ['"required":{', '"locked"', '"allowedBlocks"']) {
      expect(text).not.toContain(member);
    }
    // A block already in the draft keeps its mode, its width and its capture (review of #449, finding 5).
    for (const member of ['"renderMode"', '"span"', '"frozen"']) {
      expect(text).toContain(member);
    }
    expect(text).toContain('"background"');
    expect(text).toContain('"source"');
    expect(text).not.toContain('9007199254740991');
  });

  test('every block offered is there with its name, its sentence, its schema and its example', async () => {
    const { markdown } = await composeInstructions(input());

    for (const type of ['heading', 'text', 'image']) {
      expect(markdown).toContain(`### name of ${type} — \`${type}\`, version 1`);
      expect(markdown).toContain(`about ${type}`);
    }
    expect(markdown).toContain(JSON.stringify(block('image').example, null, 2));
    expect(markdown).not.toContain('`hero`');
  });

  test('an address is marked in the schema of a block, and the fixed part says what the mark means', async () => {
    const { markdown } = await composeInstructions(input({ blocks: [block('buttonGroup')] }));

    expect(markdown).toContain('A field marked `"address": true` holds an address');
    expect(markdown).toContain('"address": true\n');
  });

  test('the draft is there with its identifiers, and the header to copy at the end', async () => {
    const { markdown, header } = await composeInstructions(input());

    expect(markdown).toContain('"id": "blk-text"');
    expect(markdown).toContain('"key": "intro"');
    expect(markdown.slice(markdown.lastIndexOf('## The header to copy'))).toContain(
      JSON.stringify(header, null, 2),
    );
  });

  test('only the published addresses of the site, and the links of the library', async () => {
    const { markdown } = await composeInstructions(input());

    // The whole site the person may read, news and documents included
    // (decisions/2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere.md).
    expect(markdown).toContain('An address that is not here');
    expect(markdown).toContain('| `/news/launch` | news | Launch | Public |');

    expect(markdown).toContain('`/about`');
    expect(markdown).toContain('`/news`');
    expect(markdown).not.toContain('/secret-draft');
    expect(markdown).toContain('https://forum.example');
  });

  test("the template's rules, and the languages to write", async () => {
    const { markdown } = await composeInstructions(input());

    expect(markdown).toContain('| `intro` | yes | no | `text` |');
    expect(markdown).toContain('`en`, `xx`');
  });

  test('the style of the division is added at the end only when there is one', async () => {
    expect((await composeInstructions(input())).markdown).not.toContain('## The style of the division');
    expect((await composeInstructions(input({ style: 'Write short sentences.' }))).markdown).toContain(
      'Write short sentences.',
    );
  });

  test("the interactive block points at the site's guidelines instead of repeating them", async () => {
    const { markdown } = await composeInstructions(
      input({ blocks: [block('interactive')], embedGuidelines: 'https://hub.example/embed/guidelines' }),
    );

    expect(markdown).toContain('https://hub.example/embed/guidelines');
    expect(markdown).toContain('`source`');
  });
});
