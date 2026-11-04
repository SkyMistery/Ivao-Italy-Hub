import { z } from 'zod';

import { bodySchema, readBody, type Body } from '../../blocks';
import type { LocalizedString } from '../../shared/api/bootstrap';
import { ICON_NAMES } from '../../shared/icons';
import type { BlockRegistration } from '../../shared/modules';

import fixedPart from './claudeInstructions.md?raw';
import type { ContentKind } from './queries';
import type { SiteAddress } from './siteAddresses';
import type { SectionRule } from './templateRules';

/**
 * «Download for Claude» (PR 3.0a; `decisions/2026-10-30-una-pagina-scritta-con-claude.md` §3 and §4, decided by
 * Carmine on 31 October 2026; design `12-design-editor-fase-3.md` §3).
 *
 * The instructions an assistant is given to write one page of `cms_contents`: **composed** at the moment of the
 * download, never written by hand. The part that does not change is `claudeInstructions.md`, a file of the core like
 * `EmbedGuidelines.md`; the rest is read from the code that the import (PR 3.0b) checks the answer against — the JSON
 * Schema of every block offered, from its zod schema; its `example`, already proved valid by `uiKit.test.ts`; its
 * `blocks.<type>.about`; the envelope, `bodySchema` — and from the row: its kind, its readers, the languages of the
 * division, the template's rules, the addresses of the site and the draft as it is now.
 *
 * ⚠️ **Composed in the browser, not on the server**, because the schemas of the blocks exist only in zod (`CLAUDE.md`
 * §2): the server knows the envelope and never the `props`. That is also why the identifiers are fingerprints taken
 * here and checked here (note §4): they say that a file answers **these instructions, of this page, on this draft**,
 * which are the mistakes that happen — they are not a signature, and do not prove the rules were kept. The checks of
 * the import and of the server prove that, for every file.
 */

/** The format of the file an assistant returns, and of the header the instructions ask it to copy (note §4). */
export const CLAUDE_FORMAT = 'hub-content';
export const CLAUDE_FORMAT_VERSION = 1;

/** The kinds a page written with Claude can be (note §3: a page, a piece of news, a document). */
export const CLAUDE_KINDS: readonly ContentKind[] = ['Page', 'News', 'Document'];

/**
 * The header the file carries, copied unchanged from the instructions (note §4).
 *
 * `instructions` says which instructions the file is the child of — the row, its kind, and the fingerprint of the
 * **rules** they gave; `draft` says which draft it worked on — the version of the row the draft is based on
 * (`useDraftBase`, the one the editor already writes its saves over) and the fingerprint of the body the instructions
 * showed. Both are there for a page that is still empty too: "I looked at the page and worked on it" holds there.
 */
export interface ClaudeHeader {
  readonly format: typeof CLAUDE_FORMAT;
  readonly version: typeof CLAUDE_FORMAT_VERSION;
  readonly instructions: { readonly content: number; readonly kind: ContentKind; readonly rules: string };
  readonly draft: { readonly rowVersion: string; readonly body: string };
}

/**
 * JSON with its keys in order and no white space: two objects that mean the same thing give the same text, and so
 * the same fingerprint, whichever order their keys were written in. `undefined` members are left out, as
 * `JSON.stringify` leaves them out.
 */
export function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== 'object') {
    return JSON.stringify(value) ?? 'null';
  }

  if (Array.isArray(value)) {
    return `[${value.map((entry: unknown) => (entry === undefined ? 'null' : canonicalJson(entry))).join(',')}]`;
  }

  const entries = Object.entries(value as Record<string, unknown>)
    .filter(([, member]) => member !== undefined)
    .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0));

  return `{${entries.map(([key, member]) => `${JSON.stringify(key)}:${canonicalJson(member)}`).join(',')}}`;
}

/** `sha256:<hex>` of a text, with the browser's own digest. */
export async function sha256(text: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
  const hex = Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `sha256:${hex}`;
}

/**
 * The fingerprint of a body, read the way the editor reads one (`readBody`, which fills in the defaults): the same
 * draft gives the same fingerprint whether it comes from the screen or from the server. The import (PR 3.0b) takes it
 * of the draft its version names, and compares.
 */
export function bodyFingerprint(body: unknown): Promise<string> {
  return sha256(canonicalJson(readBody(body)));
}

/**
 * Two numbers zod writes on every integer — the bounds of a safe integer — which say nothing to anybody and make every
 * schema longer. Taken out of what the instructions show and of the rules alike.
 */
function withoutSafeIntegerBounds(node: unknown): unknown {
  if (Array.isArray(node)) {
    return node.map(withoutSafeIntegerBounds);
  }

  if (node === null || typeof node !== 'object') {
    return node;
  }

  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
    if (
      (key === 'minimum' && value === Number.MIN_SAFE_INTEGER) ||
      (key === 'maximum' && value === Number.MAX_SAFE_INTEGER) ||
      key === '$schema'
    ) {
      continue;
    }
    result[key] = withoutSafeIntegerBounds(value);
  }
  return result;
}

/** The JSON Schema of what an editor writes: the input side, so that a property with a default may be left out. */
export function jsonSchemaOf(schema: z.ZodType): unknown {
  return withoutSafeIntegerBounds(z.toJSONSchema(schema, { io: 'input', unrepresentable: 'any' }));
}

/**
 * The members of the envelope a page never writes: the template's rules, refused on a row that is not a template
 * (`templateRules.ts`). Everything else stays in the schema — the mode of a data block, the width of a tile, a capture
 * — because a block already in the draft keeps them as they are, and the fixed part says so (review of #449, finding 5).
 */
const SECTION_MEMBERS_NOT_WRITTEN = ['required', 'locked', 'allowedBlocks'] as const;

/** The JSON Schema of the envelope of a page: `bodySchema`, without the template's rules. */
export function envelopeSchema(): unknown {
  const schema = jsonSchemaOf(bodySchema) as {
    $defs?: Record<string, { properties?: Record<string, unknown> }>;
  };

  for (const section of Object.values(schema.$defs ?? {})) {
    const properties = section.properties ?? {};
    for (const member of SECTION_MEMBERS_NOT_WRITTEN) {
      delete properties[member];
    }
  }

  return schema;
}

/** What the instructions are composed from. Everything already read: composing asks nobody anything. */
export interface InstructionsInput {
  readonly content: { readonly id: number; readonly kind: ContentKind };
  /** The version of the row the draft on screen is based on, after it was stored. */
  readonly rowVersion: string;
  readonly title: LocalizedString;
  readonly summary: LocalizedString | null;
  readonly body: Body;
  /** Who reads the page, as the row says. */
  readonly visibility: string;
  /** The languages of the division, `division.json → locales`. */
  readonly locales: readonly string[];
  /** The blocks this page offers to this person, in the order of the registry (`palette.ts`: `addableBlocks`, `tooFewReaders`). */
  readonly blocks: readonly BlockRegistration[];
  /** The rules of the template the page was made from, by section `key`; empty without one. */
  readonly template: ReadonlyMap<string, SectionRule>;
  /** The addresses of the site, `siteAddresses.ts`; only the published ones are offered. */
  readonly addresses: readonly SiteAddress[];
  /** Where the interactive block's own guidelines are served, when it is offered. */
  readonly embedGuidelines: string | null;
  /** The words of the screen: a block's name and its `about`, already resolved. */
  readonly words: {
    readonly blockName: (block: BlockRegistration) => string;
    readonly blockAbout: (block: BlockRegistration) => string;
  };
  /** The page of the staff that describes the style of the division, as Markdown; null when none is chosen. */
  readonly style: string | null;
}

/**
 * The rules the instructions give, as one object: the format, the kind, the readers, the languages, every block offered
 * with its version and its JSON Schema, the template's sections with their rules, the envelope. **Only the rules, not
 * the prose** (note §4): the same page downloaded by somebody reading the editor in another language has the same
 * fingerprint, and a sentence changed in the style of the division does not turn a file away.
 */
export function instructionRules(input: InstructionsInput): unknown {
  return {
    format: CLAUDE_FORMAT,
    version: CLAUDE_FORMAT_VERSION,
    kind: input.content.kind,
    visibility: input.visibility,
    locales: [...input.locales],
    blocks: input.blocks.map((block) => ({
      type: block.type,
      version: block.version,
      schema: jsonSchemaOf(block.schema),
    })),
    template: [...input.template.entries()]
      .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0))
      .map(([key, rule]) => ({ key, ...rule })),
    envelope: envelopeSchema(),
  };
}

/** The header of the file, with both fingerprints taken. */
export async function instructionsHeader(input: InstructionsInput): Promise<ClaudeHeader> {
  return {
    format: CLAUDE_FORMAT,
    version: CLAUDE_FORMAT_VERSION,
    instructions: {
      content: input.content.id,
      kind: input.content.kind,
      rules: await sha256(canonicalJson(instructionRules(input))),
    },
    draft: { rowVersion: input.rowVersion, body: await bodyFingerprint(input.body) },
  };
}

const fence = (value: unknown) => `\`\`\`json\n${JSON.stringify(value, null, 2)}\n\`\`\``;

/** A cell of a Markdown table, which a pipe or a line break in a title would otherwise split. */
const cell = (text: string) => text.replaceAll('|', '\\|').replaceAll(/\s+/g, ' ').trim();

function templateSection(input: InstructionsInput): string {
  if (input.template.size === 0) {
    return '## The template\n\nThe page was not made from a template: every section is free.';
  }

  const rows = [...input.template.entries()].map(([key, rule]) => {
    const allowed =
      rule.allowedBlocks === null ? 'any' : rule.allowedBlocks.map((type) => `\`${type}\``).join(', ');
    return `| \`${key}\` | ${rule.required ? 'yes' : 'no'} | ${rule.locked ? 'yes' : 'no'} | ${allowed} |`;
  });

  return [
    '## The template',
    '',
    'The page was made from a template. Its sections are matched by `key`; a section of the page with one of these keys',
    'keeps the rules written here.',
    '',
    '| key | required (may not be removed) | locked (only the texts of its blocks change) | blocks it takes |',
    '|---|---|---|---|',
    ...rows,
  ].join('\n');
}

function addressesSection(input: InstructionsInput): string {
  const offered = input.addresses.filter((address) => address.published && address.kind !== 'link');
  const links = input.addresses.filter((address) => address.kind === 'link');

  const lines = [
    '## The addresses of the site',
    '',
    // ⚠️ Not the whole site: the pages are the ones the list of the content gives whoever downloads — their own
    // departments', a hundred at most — and the text says so (review of #449, finding 2). A read of every page they may
    // read is a question for a core change of its own.
    'The addresses of this site you may link to, with who reads them. They are the published pages of the departments of',
    'the person who downloaded these instructions (a hundred at most), the screens of the site and the links of the library:',
    'the site may have more pages than these. When the page you need is not here, do not guess its address: in a button or',
    'a card leave `href` empty (`""`), in a text write the words without a link, and say so to the person.',
    '',
    '| address | name | read by |',
    '|---|---|---|',
    ...offered.map((address) => `| \`${address.path}\` | ${cell(address.label)} | ${address.visibility} |`),
  ];

  if (links.length > 0) {
    lines.push(
      '',
      'The links of the library, addresses outside the site that the division already keeps:',
      '',
      '| address | name | read by |',
      '|---|---|---|',
      ...links.map((address) => `| ${address.path} | ${cell(address.label)} | ${address.visibility} |`),
    );
  }

  return lines.join('\n');
}

function blocksSection(input: InstructionsInput): string {
  const entries = input.blocks.map((block) =>
    [
      `### ${input.words.blockName(block)} — \`${block.type}\`, version ${block.version}`,
      '',
      input.words.blockAbout(block),
      ...(block.kind === 'Data'
        ? [
            '',
            'A data block: the site fills it with what it knows when the page is read. Its properties are the question, ' +
              'not the content.',
          ]
        : []),
      ...(block.carriesSource === true && input.embedGuidelines !== null
        ? [
            '',
            `Its code goes in the block's \`source\`, not in \`props\`. How to write it is in the site's own guidelines, ` +
              `${input.embedGuidelines}: if you have not been given them, ask for them before writing any code.`,
          ]
        : []),
      '',
      'Its `props`:',
      '',
      fence(jsonSchemaOf(block.schema)),
      '',
      'An example:',
      '',
      fence(block.example),
    ].join('\n'),
  );

  return [
    '## The blocks',
    '',
    'The blocks this page may use, and only these.',
    '',
    entries.join('\n\n'),
  ].join('\n');
}

/**
 * The instructions, whole: the fixed part, then what this page is, its rules, its addresses, its blocks, the envelope,
 * the draft, the style of the division and the header to copy.
 */
export async function composeInstructions(
  input: InstructionsInput,
): Promise<{ markdown: string; header: ClaudeHeader }> {
  const header = await instructionsHeader(input);

  const parts = [
    fixedPart.trim(),
    [
      '## This page',
      '',
      `- Kind: ${input.content.kind}.`,
      `- Read by: ${input.visibility}.`,
      `- Languages of the division, every one to be written: ${input.locales.map((locale) => `\`${locale}\``).join(', ')}.`,
    ].join('\n'),
    templateSection(input),
    addressesSection(input),
    blocksSection(input),
    `## Icons\n\nThe names a field marked \`"icon": true\` takes: ${ICON_NAMES.map((name) => `\`${name}\``).join(', ')}.`,
    [
      '## The envelope',
      '',
      'The JSON Schema of `body`: its sections, and the frame around the `props` of a block.',
      '',
      fence(envelopeSchema()),
    ].join('\n'),
    [
      '## The draft as it is now',
      '',
      'Integrate what is new into this, keeping the identifiers of what you keep.',
      '',
      fence({ title: input.title, summary: input.summary, body: input.body }),
    ].join('\n'),
    ...(input.style === null
      ? []
      : [
          `## The style of the division\n\nHow this division writes, as its staff describe it:\n\n${input.style.trim()}`,
        ]),
    [
      '## The header to copy',
      '',
      'The first members of your file, exactly as they are here:',
      '',
      fence(header),
      '',
      'Then `title`, `summary` and `body`.',
    ].join('\n'),
  ];

  return { markdown: `${parts.join('\n\n')}\n`, header };
}
