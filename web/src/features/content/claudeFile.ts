import type { TFunction } from 'i18next';
import { z } from 'zod';

import {
  allBlocks,
  allSections,
  bodySchema,
  newId,
  type BlockEnvelope,
  type Body,
  type SectionEnvelope,
} from '../../blocks';
import type { LocalizedString } from '../../shared/api/bootstrap';
import { readFields, type FieldNode } from '../../shared/forms';
import type { BlockRegistration } from '../../shared/modules';

import { CHANGELOG_MAX } from './changeSummary';
import {
  canonicalJson,
  CLAUDE_FORMAT,
  CLAUDE_FORMAT_VERSION,
  instructionsHeader,
  type HeaderInput,
} from './claudeInstructions';
import { ruleFor, type SectionRule } from './templateRules';

/**
 * «Import from Claude» (PR 3.0b; `decisions/2026-10-30-una-pagina-scritta-con-claude.md` §3 and §4, with the answers of
 * its §8; design `12-design-editor-fase-3.md` §3).
 *
 * The file an assistant returned, read in the browser and checked in the note's order: the header, the envelope, every
 * block with its schema, the rules of the template, the identifiers. ⚠️ This is the only check the `props` of a body
 * ever get as a whole (note §2): by hand the generated form does not let a wrong one be written, and the server never
 * reads them. What passes is then saved like any draft, over the version it worked on, and the server's own checks run.
 *
 * **Refusing and not warning** (note §4): the import replaces the draft, so a warning clicked through is somebody's work
 * lost. The one question is the page started over, which may be wanted.
 *
 * What the assistant was told to leave alone is not left to its word: a block of the draft keeps its `renderMode`, its
 * `span`, its `frozen` and its `version` whatever the file says, and a section its `key` (review of #449, finding 5). The
 * editor gives the same answer for a block it adds: the file does not choose either.
 */

/** Far above the 1 MiB of a body (`BlockDocumentWalker`): what is refused here is a file that was never a page. */
export const MAX_IMPORT_BYTES = 8 * 1024 * 1024;

const fileSchema = z.object({
  format: z.literal(CLAUDE_FORMAT),
  version: z.literal(CLAUDE_FORMAT_VERSION),
  instructions: z.object({ content: z.number(), kind: z.string(), rules: z.string() }),
  draft: z.object({ rowVersion: z.string(), body: z.string() }),
});

const titleSchema = z.record(z.string(), z.string());
const summarySchema = titleSchema.nullish();

/** One thing wrong with a file. `path` is spelled as the server spells it, for `useBodyPathDescription`. */
export interface ImportProblem {
  readonly path: string | null;
  /** An i18n key. */
  readonly key: string;
  readonly values?: Readonly<Record<string, string | number>>;
  /** What zod said, for somebody to hand back to Claude; it is not a sentence of the hub's. */
  readonly detail?: string;
}

/** What an import leaves for the person to do (note §3.5): it does not stop the file, and it is said beside the block. */
export interface ImportPending {
  /** The block, or the section, by id: an id stays while the indices of a body move. */
  readonly owner: string;
  /** Where inside it, dotted (`images.0.mediaId`); null for the picture of a section. */
  readonly field: string | null;
  readonly kind: 'picture' | 'value' | 'link';
  /** The address a link leads to; empty when it is left for the person to choose. */
  readonly address?: string;
}

export interface ImportRefusal {
  readonly outcome: 'refused';
  /** An i18n key: the whole answer when `problems` is empty, the heading of the list otherwise. */
  readonly message: string;
  readonly values?: Readonly<Record<string, string | number>>;
  readonly problems: readonly ImportProblem[];
  /** The body the paths read against, once there is one. */
  readonly body: Body | null;
}

export interface ImportReady {
  readonly outcome: 'ready';
  readonly title: LocalizedString;
  readonly summary: LocalizedString | null;
  readonly body: Body;
  /** The version of the row this was checked against. */
  readonly rowVersion: string;
  /** Nothing of a draft that had something: the one thing the import asks (note §4). */
  readonly startedOver: boolean;
  readonly pending: readonly ImportPending[];
}

export type ImportAnswer = ImportRefusal | ImportReady;

/** What a file is checked against: the header's own input, today, and what only the import needs. */
export interface ImportContext extends HeaderInput {
  /** Whether what is on screen differs from what was last stored. */
  readonly dirty: boolean;
  /** Every block the build knows: a block of the draft may be one no longer offered, and is read with its own schema. */
  readonly registered: readonly BlockRegistration[];
  /** Where an address of this site may lead (`knownAddresses`). */
  readonly addresses: ReadonlySet<string>;
}

const refuse = (
  message: string,
  values: Readonly<Record<string, string | number>> = {},
  problems: readonly ImportProblem[] = [],
  body: Body | null = null,
): ImportRefusal => ({ outcome: 'refused', message, values, problems, body });

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/** `['sections', 0, 'blocks', 2, 'props', 'text']` as the server writes it: `sections[0].blocks[2].props.text`. */
function spelled(path: readonly PropertyKey[]): string {
  return path.reduce<string>((written, part) => {
    if (typeof part === 'number') {
      return `${written}[${part}]`;
    }
    return written === '' ? String(part) : `${written}.${String(part)}`;
  }, '');
}

/** A path inside `props`, dotted, as the form spells it and as `valueAt` reads it. */
const dotted = (path: readonly PropertyKey[]) => path.map(String).join('.');

function valueAt(root: unknown, field: string): unknown {
  return field.split('.').reduce<unknown>((value, part) => {
    if (Array.isArray(value)) {
      return value[Number(part)];
    }
    return isRecord(value) ? value[part] : undefined;
  }, root);
}

function setAt(root: unknown, path: readonly PropertyKey[], value: unknown, remove = false) {
  let holder: unknown = root;
  for (const part of path.slice(0, -1)) {
    holder = (holder as Record<string, unknown>)[String(part)];
    if (typeof holder !== 'object' || holder === null) {
      return;
    }
  }

  const last = String(path.at(-1));
  if (remove) {
    delete (holder as Record<string, unknown>)[last];
  } else {
    (holder as Record<string, unknown>)[last] = value;
  }
}

const blank = (value: unknown) => value === undefined || value === null || value === '';

/**
 * Every section and block of the file with an identifier: a new one has none, and gets one here as the editor would
 * give it (`newId`, note §3.4). The envelope asks for one, so this comes before it; what it finds wrong with the
 * identifiers kept is said with the rest.
 */
function giveIdentifiers(
  raw: unknown,
  draft: Body,
  problems: ImportProblem[],
): { body: unknown; kept: number } {
  const known = {
    s: new Set(allSections(draft).map((section) => section.id)),
    b: new Set(allBlocks(draft).map((block) => block.id)),
  };
  const seen = new Set<string>();
  let kept = 0;

  const identified = (item: Record<string, unknown>, prefix: 's' | 'b', path: string) => {
    if (blank(item['id'])) {
      return { ...item, id: newId(prefix) };
    }

    if (typeof item['id'] === 'string') {
      if (!known[prefix].has(item['id']) || seen.has(item['id'])) {
        problems.push({ path, key: 'content.import.idUnknown', values: { id: item['id'] } });
      } else {
        kept += 1;
      }
      seen.add(item['id']);
    }

    return item;
  };

  const inSection = (section: unknown, path: string): unknown => {
    if (!isRecord(section)) {
      return section;
    }

    return {
      ...identified(section, 's', path),
      ...(Array.isArray(section['blocks'])
        ? {
            blocks: section['blocks'].map((block: unknown, index) =>
              isRecord(block) ? identified(block, 'b', `${path}.blocks[${index}]`) : block,
            ),
          }
        : {}),
      ...(Array.isArray(section['sections'])
        ? {
            sections: section['sections'].map((nested: unknown, index) =>
              inSection(nested, `${path}.sections[${index}]`),
            ),
          }
        : {}),
    };
  };

  if (!isRecord(raw) || !Array.isArray(raw['sections'])) {
    return { body: raw, kept };
  }

  return {
    body: {
      ...raw,
      sections: raw['sections'].map((section: unknown, index) => inSection(section, `sections[${index}]`)),
    },
    kept,
  };
}

/**
 * The identifiers of the pictures of a value, by the name every file of a block goes by (`docs/UI-GUIDELINES.md`, «How
 * a block names a file»), so that the server, too, reads them without reading `props`.
 */
function picturesOf(value: unknown, found: Set<number>) {
  if (Array.isArray(value)) {
    value.forEach((entry) => picturesOf(entry, found));
  } else if (isRecord(value)) {
    for (const [key, member] of Object.entries(value)) {
      if (key === 'mediaId' && typeof member === 'number') {
        found.add(member);
      } else {
        picturesOf(member, found);
      }
    }
  }
}

function picturesOfTheDraft(draft: Body): ReadonlySet<number> {
  const found = new Set<number>();
  for (const section of allSections(draft)) {
    if (typeof section.mediaId === 'number') {
      found.add(section.mediaId);
    }
    section.blocks.forEach((block) => picturesOf(block.props, found));
  }
  return found;
}

/**
 * A picture the draft does not have is not a picture: an assistant cannot upload anything and has seen no library, so
 * the number is the one in an example, or an invention, and somebody else's photo would go on the page unnoticed. It
 * comes out, and the field waits like any other left empty.
 */
function withoutInventedPictures(value: unknown, pictures: ReadonlySet<number>) {
  if (Array.isArray(value)) {
    value.forEach((entry) => withoutInventedPictures(entry, pictures));
  } else if (isRecord(value)) {
    for (const [key, member] of Object.entries(value)) {
      if (key === 'mediaId') {
        if (typeof member === 'number' && !pictures.has(member)) {
          delete value[key];
        }
      } else {
        withoutInventedPictures(member, pictures);
      }
    }
  }
}

/** Every empty required text of a data block: what it asks and only the person knows (a category, note §3.5). */
function blankTexts(fields: readonly FieldNode[], values: unknown, prefix: string): string[] {
  const nameOf = (field: FieldNode) => field.path.split('.').at(-1) ?? field.path;

  return fields.flatMap((field) => {
    const name = prefix === '' ? nameOf(field) : `${prefix}.${nameOf(field)}`;
    const value = isRecord(values) ? values[nameOf(field)] : undefined;

    if (field.kind === 'object') {
      return blankTexts(field.children, value, name);
    }

    if (field.kind === 'list') {
      return Array.isArray(value)
        ? value.flatMap((entry, index) => blankTexts(field.children, entry, `${name}.${index}`))
        : [];
    }

    return (field.kind === 'text' || field.kind === 'suggest') && !field.optional && blank(value)
      ? [name]
      : [];
  });
}

const MARKDOWN_LINK = /\]\((\/[^)\s]*)/g;

/** An address of this site as the list has it: no query, no anchor, no slash at the end. */
function addressOf(link: string): string {
  const path = link.split(/[?#]/, 1)[0] ?? link;
  return path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path;
}

/**
 * The links of a block's properties that lead nowhere the instructions listed (note §3.5), by the two conventions the
 * server's check will read (design §5.9): a key `href`, and a Markdown link. Said, not refused: the site's own check is
 * at publication (PR 3.15), and until then a draft may point at a page that comes out later.
 */
function linksToCheck(
  value: unknown,
  field: string,
  addresses: ReadonlySet<string>,
  found: { field: string; address: string }[],
) {
  if (Array.isArray(value)) {
    value.forEach((entry, index) => linksToCheck(entry, `${field}.${index}`, addresses, found));
  } else if (isRecord(value)) {
    for (const [key, member] of Object.entries(value)) {
      const inner = field === '' ? key : `${field}.${key}`;

      if (key === 'href' && typeof member === 'string') {
        // Left empty on purpose: the instructions tell Claude to, when the page it needs does not exist.
        if (
          member === '' ||
          (member.startsWith('/') && !member.startsWith('//') && !addresses.has(addressOf(member)))
        ) {
          found.push({ field: inner, address: member });
        }
      } else {
        linksToCheck(member, inner, addresses, found);
      }
    }
  } else if (typeof value === 'string') {
    for (const [, link] of value.matchAll(MARKDOWN_LINK)) {
      if (link !== undefined && !link.startsWith('//') && !addresses.has(addressOf(link))) {
        found.push({ field, address: link });
      }
    }
  }
}

/** A locked section may have its blocks' properties changed, and nothing else about it (`templateRules.ts`). */
function shapeOf(section: SectionEnvelope): string {
  return canonicalJson({
    layout: section.layout,
    background: section.background,
    mediaId: section.mediaId ?? null,
    padding: section.padding,
    width: section.width,
    blocks: section.blocks.map((block) => [block.id, block.type, block.column ?? 0]),
    sections: section.sections.map((nested) => nested.id),
  });
}

const nameOfSection = (section: SectionEnvelope) => section.key ?? section.id;

/** The whole of the body's check, after the header. Refusals gather, so that one trip to Claude can mend them all. */
function settleBody(
  parsed: Body,
  context: ImportContext,
  problems: ImportProblem[],
): { body: Body; pending: ImportPending[] } {
  const { template, registered } = context;
  const draft = context.body;
  const offered = new Set(context.blocks.map((block) => block.type));
  const pictures = picturesOfTheDraft(draft);
  const pending: ImportPending[] = [];

  const draftSections = new Map(allSections(draft).map((section) => [section.id, section]));
  const draftBlocks = new Map(
    allSections(draft).flatMap((section) =>
      section.blocks.map((block) => [block.id, { block, section: section.id }] as const),
    ),
  );

  // The keys the page keeps: a new section takes one of the template's only if nothing else holds it.
  const keysTaken = new Set(
    allSections(parsed).flatMap((section) => {
      const key = draftSections.get(section.id)?.key;
      return typeof key === 'string' ? [key] : [];
    }),
  );

  const links = (block: BlockEnvelope) => {
    const found: { field: string; address: string }[] = [];
    linksToCheck(block.props, '', context.addresses, found);
    for (const { field, address } of found) {
      pending.push({ owner: block.id, field, kind: 'link', address });
    }
  };

  /** The block's properties against its schema; what only a person can give is left for them, and said. */
  const checkProps = (
    block: BlockEnvelope,
    registration: BlockRegistration,
    path: string,
  ): Record<string, unknown> => {
    const written = structuredClone(block.props);
    withoutInventedPictures(written, pictures);

    const first = registration.schema.safeParse(written);
    const issues = first.success ? [] : first.error.issues;

    const isPicture = (issue: z.core.$ZodIssue) => issue.path.at(-1) === 'mediaId';
    const isUnknowable = (issue: z.core.$ZodIssue) =>
      registration.kind === 'Data' &&
      issue.code === 'invalid_type' &&
      issue.expected === 'string' &&
      valueAt(written, dotted(issue.path)) === undefined;

    const unmet = issues.filter((issue) => !isPicture(issue) && !isUnknowable(issue));
    for (const issue of unmet) {
      problems.push({
        path: `${path}.props.${dotted(issue.path)}`,
        key: 'content.import.propInvalid',
        detail: issue.message,
      });
    }
    if (unmet.length > 0) {
      return block.props;
    }

    // What is missing stands in for a moment, so that the rest is read by the schema — its defaults filled in, members
    // it does not have left out — exactly as a block whose every field was written.
    const pictureFields = issues.filter(isPicture).map((issue) => issue.path);
    pictureFields.forEach((field) => setAt(written, field, 0));
    issues.filter(isUnknowable).forEach((issue) => setAt(written, issue.path, ''));

    const second = registration.schema.safeParse(written);
    if (!second.success) {
      problems.push({
        path: `${path}.props`,
        key: 'content.import.propInvalid',
        detail: second.error.issues[0]?.message ?? '',
      });
      return block.props;
    }

    const props = second.data;
    pictureFields.forEach((field) => setAt(props, field, undefined, true));

    for (const field of pictureFields) {
      pending.push({ owner: block.id, field: dotted(field), kind: 'picture' });
    }

    if (registration.kind === 'Data') {
      for (const field of blankTexts(readFields(registration.schema), props, '')) {
        pending.push({ owner: block.id, field, kind: 'value' });
      }
    }

    return props;
  };

  const settleBlock = (
    block: BlockEnvelope,
    path: string,
    section: string,
    rule: SectionRule,
  ): BlockEnvelope => {
    const before = draftBlocks.get(block.id);
    const registration = registered.find((candidate) => candidate.type === block.type);

    if (before !== undefined && before.block.type !== block.type) {
      problems.push({ path, key: 'content.import.blockChangedType', values: { type: block.type } });
      return block;
    }

    if (before === undefined && !offered.has(block.type)) {
      problems.push({ path, key: 'content.import.blockNotOffered', values: { type: block.type } });
      return block;
    }

    if (
      rule.allowedBlocks !== null &&
      before?.section !== section &&
      !rule.allowedBlocks.includes(block.type)
    ) {
      problems.push({ path, key: 'content.import.blockNotAllowed', values: { type: block.type } });
    }

    const unchanged =
      before !== undefined && canonicalJson(block.props) === canonicalJson(before.block.props);

    // A block nobody touched is not asked anything: it may be one no longer offered, or in the form an older version of
    // it wrote, which its panel translates when it is opened (design §2, principle 4).
    if (!unchanged && registration === undefined) {
      problems.push({ path, key: 'content.import.blockNotOffered', values: { type: block.type } });
      return block;
    }

    const props =
      unchanged || registration === undefined
        ? (before?.block.props ?? block.props)
        : checkProps(block, registration, path);

    const settled: BlockEnvelope =
      before === undefined
        ? {
            ...block,
            props,
            // As the editor makes a block it adds (`addBlockTo`): capturing is a decision somebody takes later.
            version: registration?.version ?? 1,
            renderMode: registration?.kind === 'Data' ? 'live' : null,
            frozen: null,
            span: null,
            source: registration?.carriesSource === true ? block.source : null,
          }
        : {
            ...block,
            props,
            // Choices made in the editor, and the site's own capture: never the file's.
            version: before.block.version,
            renderMode: before.block.renderMode,
            frozen: before.block.frozen,
            span: before.block.span,
            source: block.source ?? before.block.source,
          };

    if (!unchanged) {
      links(settled);
    }

    return settled;
  };

  const settleSection = (section: SectionEnvelope, path: string): SectionEnvelope => {
    const before = draftSections.get(section.id);

    let key: string | null | undefined = before?.key;
    if (
      before === undefined &&
      typeof section.key === 'string' &&
      template.has(section.key) &&
      !keysTaken.has(section.key)
    ) {
      keysTaken.add(section.key);
      key = section.key;
    }

    const rule = ruleFor(template, key);
    const settled: SectionEnvelope = {
      ...section,
      key: key ?? null,
      // A locked section's name is the template's too (`SectionProperties` is not even drawn).
      title: rule.locked && before !== undefined ? before.title : section.title,
      blocks: section.blocks.map((block, index) =>
        settleBlock(block, `${path}.blocks[${index}]`, section.id, rule),
      ),
      sections: section.sections.map((nested, index) => settleSection(nested, `${path}.sections[${index}]`)),
    };

    // A template's own members, refused on a page by the server: a page may not lift its own restrictions.
    delete settled.required;
    delete settled.locked;
    delete settled.allowedBlocks;

    if (typeof settled.mediaId === 'number' && !pictures.has(settled.mediaId)) {
      delete settled.mediaId;
    }

    if (
      settled.background === 'image' &&
      blank(settled.mediaId) &&
      (before === undefined || !blank(before.mediaId))
    ) {
      pending.push({ owner: settled.id, field: null, kind: 'picture' });
    }

    if (rule.locked && (before === undefined || shapeOf(before) !== shapeOf(settled))) {
      problems.push({ path, key: 'content.import.sectionLocked', values: { name: nameOfSection(settled) } });
    }

    return settled;
  };

  const body: Body = {
    ...parsed,
    sections: parsed.sections.map((section, index) => settleSection(section, `sections[${index}]`)),
  };

  // What the editor would not have let go: a required section, and a locked one, which has no «remove» either.
  const keysKept = new Set(allSections(body).map((section) => section.key));
  for (const section of allSections(draft)) {
    const rule = ruleFor(template, section.key);
    if ((rule.required || rule.locked) && !keysKept.has(section.key)) {
      problems.push({
        path: null,
        key: 'content.import.sectionRequired',
        values: { name: nameOfSection(section) },
      });
    }
  }

  return { body, pending };
}

/** The file, checked. Nothing here writes anything: the answer says what would be written, or why not. */
export async function readClaudeFile(text: string, context: ImportContext): Promise<ImportAnswer> {
  let file: unknown;
  try {
    file = JSON.parse(text);
  } catch {
    return refuse('content.import.notFromInstructions');
  }

  const header = fileSchema.safeParse(file);
  if (!header.success) {
    return refuse('content.import.notFromInstructions');
  }

  const { instructions, draft } = header.data;

  if (instructions.content !== context.content.id || instructions.kind !== context.content.kind) {
    return refuse('content.import.otherContent', { content: instructions.content });
  }

  // Taken again now, with the function that wrote them: what may have changed since is exactly what they are for.
  const today = await instructionsHeader(context);

  if (instructions.rules !== today.instructions.rules) {
    return refuse('content.import.rulesChanged');
  }

  if (context.dirty || draft.rowVersion !== today.draft.rowVersion) {
    return refuse('content.import.draftChanged');
  }

  if (draft.body !== today.draft.body) {
    return refuse('content.import.draftNotRead');
  }

  const { title, summary, body } = file as { title?: unknown; summary?: unknown; body?: unknown };
  const problems: ImportProblem[] = [];

  const readTitle = titleSchema.safeParse(title);
  if (!readTitle.success || Object.values(readTitle.data).every((value) => value.trim() === '')) {
    problems.push({ path: 'title', key: 'errors.required' });
  }

  const readSummary = summarySchema.safeParse(summary);
  if (!readSummary.success) {
    problems.push({
      path: 'summary',
      key: 'content.import.propInvalid',
      detail: readSummary.error.issues[0]?.message ?? '',
    });
  }

  const identified = giveIdentifiers(body, context.body, problems);

  const envelope = bodySchema.safeParse(identified.body);
  if (!envelope.success) {
    return refuse('content.import.refused', {}, [
      ...problems,
      ...envelope.error.issues.map((issue) => ({
        path: spelled(issue.path),
        key: 'content.import.envelopeInvalid',
        detail: issue.message,
      })),
    ]);
  }

  const settled = settleBody(envelope.data, context, problems);

  if (problems.length > 0 || !readTitle.success || !readSummary.success) {
    return refuse('content.import.refused', {}, problems, settled.body);
  }

  return {
    outcome: 'ready',
    title: readTitle.data,
    summary: readSummary.data ?? null,
    body: settled.body,
    rowVersion: today.draft.rowVersion,
    startedOver: identified.kept === 0 && allSections(context.body).length > 0,
    pending: settled.pending,
  };
}

/**
 * What is still to do of what an import left, against the body as it is now: an entry leaves the list when its block is
 * gone or its field is filled in, and each one is given the path that names it today.
 */
export function pendingNow(
  body: Body,
  pending: readonly ImportPending[],
): readonly { path: string; entry: ImportPending }[] {
  const places = new Map<string, { path: string; item: SectionEnvelope | BlockEnvelope }>();

  const visit = (sections: readonly SectionEnvelope[], prefix: string) => {
    sections.forEach((section, index) => {
      const path = `${prefix}sections[${index}]`;
      places.set(section.id, { path, item: section });
      section.blocks.forEach((block, blockIndex) => {
        places.set(block.id, { path: `${path}.blocks[${blockIndex}]`, item: block });
      });
      visit(section.sections, `${path}.`);
    });
  };
  visit(body.sections, '');

  return pending.flatMap((entry) => {
    const place = places.get(entry.owner);
    if (place === undefined) {
      return [];
    }

    if (entry.field === null) {
      return blank((place.item as SectionEnvelope).mediaId) ? [{ path: place.path, entry }] : [];
    }

    const value = valueAt((place.item as BlockEnvelope).props, entry.field);
    const open =
      entry.kind === 'link' && entry.address !== ''
        ? typeof value === 'string' && value.includes(entry.address ?? '')
        : blank(value);

    return open ? [{ path: `${place.path}.props.${entry.field}`, entry }] : [];
  });
}

const importedKey = (id: number) => `hub.content.imported.${id}`;

/**
 * That this row's draft came from a file, for the line of its next version (note §3.3, answer 6: there and nowhere else,
 * the audit stays as it is). ⚠️ Kept in this browser, because nothing on the server holds it and the note wants no write
 * for it: it survives a reload, not another person. It holds as long as the row has not been published since.
 */
export function rememberImported(row: { readonly id: number; readonly publishedAt: string | null }) {
  try {
    localStorage.setItem(importedKey(row.id), row.publishedAt ?? '');
  } catch {
    // No storage, no first words on the line: the person writes it.
  }
}

export function forgetImported(id: number) {
  try {
    localStorage.removeItem(importedKey(id));
  } catch {
    // Nothing was kept, then.
  }
}

function keptImport(id: number): string | null {
  try {
    return localStorage.getItem(importedKey(id));
  } catch {
    return null;
  }
}

/** The line a publication starts from, with the import's words in front when its draft came from a file. */
export function versionLine(
  row: { readonly id: number; readonly publishedAt: string | null },
  line: string,
  t: TFunction,
): string {
  if (keptImport(row.id) !== (row.publishedAt ?? '')) {
    return line;
  }

  const written = `${t('content.import.versionLine')} ${line}`;
  return written.length <= CHANGELOG_MAX ? written : `${written.slice(0, CHANGELOG_MAX - 1)}…`;
}
