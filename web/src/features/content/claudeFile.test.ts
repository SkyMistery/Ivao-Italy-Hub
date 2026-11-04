import type { TFunction } from 'i18next';
import { beforeEach, describe, expect, test } from 'vitest';

import { registry } from '../../app/registry';
import { allBlocks, allSections, type Body } from '../../blocks';

import {
  forgetImported,
  pendingNow,
  readClaudeFile,
  readLeftovers,
  rememberImported,
  rememberLeftovers,
  versionLine,
  type ImportContext,
} from './claudeFile';
import { instructionsHeader } from './claudeInstructions';
import { offeredBlocks } from './palette';
import type { SectionRule } from './templateRules';

/**
 * «Import from Claude» (PR 3.0b; note 2026-10-30-una-pagina-scritta-con-claude §3, §4). Every row of the note's table,
 * and what the review of #449 left to Claude's word: the header is the one `instructionsHeader` writes for the download,
 * never a copy of it, so that the two cannot drift.
 */

const text = (id: string, en = 'Hello') => ({
  id,
  type: 'text',
  version: 1,
  props: { markdown: { en, xx: `${en} xx` } },
  renderMode: null,
  frozen: null,
  column: null,
  span: null,
  source: null,
});

const draft: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 'sec-a',
      key: null,
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [
        text('blk-a'),
        {
          id: 'blk-data',
          type: 'newsList',
          version: 1,
          props: { category: 'updates', limit: 3 },
          renderMode: 'frozen',
          frozen: { items: [1, 2] },
          column: null,
          span: 6,
          source: null,
        },
      ],
      sections: [],
    },
  ],
};

const context = (over: Partial<ImportContext> = {}): ImportContext => ({
  content: { id: 7, kind: 'Page' },
  rowVersion: '2026-11-04T10:00:00',
  body: draft,
  visibility: 'Public',
  locales: ['en', 'xx'],
  blocks: offeredBlocks(registry.blocks, () => true, [], 'Public'),
  template: new Map<string, SectionRule>(),
  dirty: false,
  registered: registry.blocks,
  addresses: new Set(['/', '/about', '/news']),
  holds: () => true,
  ...over,
});

/** The file Claude would return for a context: the header the download writes, and the draft as it is. */
async function fileFor(ctx: ImportContext, change: Partial<Record<string, unknown>> = {}) {
  return {
    ...(await instructionsHeader(ctx)),
    title: { en: 'Title', xx: 'Titolo' },
    summary: null,
    body: structuredClone(ctx.body),
    ...change,
  };
}

const read = async (file: unknown, ctx = context()) =>
  readClaudeFile(typeof file === 'string' ? file : JSON.stringify(file), ctx);

const refused = async (file: unknown, ctx = context()) => {
  const answer = await read(file, ctx);
  if (answer.outcome !== 'refused') {
    throw new Error('The file was taken.');
  }
  return answer;
};

const ready = async (file: unknown, ctx = context()) => {
  const answer = await read(file, ctx);
  if (answer.outcome !== 'ready') {
    throw new Error(`Refused: ${JSON.stringify(answer)}`);
  }
  return answer;
};

const withSections = (sections: unknown[]) => ({ schemaVersion: 1, sections });

const section = (over: Record<string, unknown> = {}) => ({
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: [],
  sections: [],
  ...over,
});

describe('the identifiers of the note, §4', () => {
  test('a file answering these instructions, of this page, on this draft, is taken', async () => {
    const answer = await ready(await fileFor(context()));

    expect(answer.title).toEqual({ en: 'Title', xx: 'Titolo' });
    expect(answer.startedOver).toBe(false);
    expect(answer.pending).toEqual([]);
  });

  test.each([
    ['not JSON at all', 'hello'],
    ['JSON without a header', { title: {}, body: {} }],
    ['another format', { format: 'something', version: 1 }],
  ])('%s does not come from the instructions', async (_name, file) => {
    expect((await refused(file)).message).toBe('content.import.notFromInstructions');
  });

  test('a version of the format it does not know is refused', async () => {
    const file = { ...(await fileFor(context())), version: 2 };
    expect((await refused(file)).message).toBe('content.import.notFromInstructions');
  });

  test("another row's file, or another kind's, is for another page", async () => {
    const other = await fileFor(context({ content: { id: 8, kind: 'Page' } }));
    expect((await refused(other)).message).toBe('content.import.otherContent');

    const news = await fileFor(context({ content: { id: 7, kind: 'News' } }));
    expect((await refused(news)).message).toBe('content.import.otherContent');
  });

  test.each([
    ['the visibility', { visibility: 'Staff' }],
    ['the languages', { locales: ['en'] }],
    ['the blocks', { blocks: [] }],
    [
      'the template',
      { template: new Map([['intro', { required: true, locked: false, allowedBlocks: null }]]) },
    ],
  ])('a change of %s after the download turns the file away', async (_name, change) => {
    const file = await fileFor(context());
    expect((await refused(file, context(change as Partial<ImportContext>))).message).toBe(
      'content.import.rulesChanged',
    );
  });

  test('a draft that moved on, or that has changes not yet stored, turns the file away', async () => {
    const file = await fileFor(context());

    const moved = context({ rowVersion: '2026-11-04T11:00:00' });
    expect((await refused(file, moved)).message).toBe('content.import.draftChanged');
    expect((await refused(file, context({ dirty: true }))).message).toBe('content.import.draftChanged');
  });

  test('a fingerprint that is not of that draft says Claude did not work on it', async () => {
    const base = await fileFor(context());
    const file = { ...base, draft: { ...base.draft, body: 'sha256:0000' } };

    expect((await refused(file)).message).toBe('content.import.draftNotRead');
  });

  test('an identifier the draft did not have, or used twice, is refused beside its block', async () => {
    const unknown = await fileFor(context(), {
      body: withSections([section({ id: 'sec-a', blocks: [text('blk-a'), text('blk-invented')] })]),
    });
    expect((await refused(unknown)).problems).toContainEqual({
      path: 'sections[0].blocks[1]',
      key: 'content.import.idUnknown',
      values: { id: 'blk-invented' },
    });

    const twice = await fileFor(context(), {
      body: withSections([section({ id: 'sec-a', blocks: [text('blk-a'), text('blk-a')] })]),
    });
    expect((await refused(twice)).problems.map((problem) => problem.key)).toContain(
      'content.import.idUnknown',
    );
  });

  test('nothing of a draft that had something is the one thing the import asks', async () => {
    const file = await fileFor(context(), {
      body: withSections([section({ blocks: [{ type: 'text', version: 1, props: text('').props }] })]),
    });

    expect((await ready(file)).startedOver).toBe(true);
  });

  test('a draft that was empty is not started over', async () => {
    const empty = context({ body: { schemaVersion: 1, sections: [] } });
    const file = await fileFor(empty, {
      body: withSections([section({ blocks: [{ type: 'text', version: 1, props: text('').props }] })]),
    });

    expect((await ready(file, empty)).startedOver).toBe(false);
  });
});

describe('what is new gets what the editor would give it', () => {
  test('a new section and a new block get an identifier, and a kept one keeps its own', async () => {
    const answer = await ready(
      await fileFor(context(), {
        body: withSections([
          section({ id: 'sec-a', blocks: [text('blk-a')] }),
          section({ blocks: [{ type: 'text', version: 1, props: text('').props }] }),
        ]),
      }),
    );

    const ids = allSections(answer.body).map((each) => each.id);
    expect(ids[0]).toBe('sec-a');
    expect(ids[1]).toMatch(/^s_/);
    expect(allBlocks(answer.body).map((each) => each.id)[1]).toMatch(/^b_/);
  });

  test('a new data block starts live and with nothing captured, whatever the file says', async () => {
    const answer = await ready(
      await fileFor(context(), {
        body: withSections([
          section({
            id: 'sec-a',
            blocks: [
              text('blk-a'),
              {
                type: 'newsList',
                version: 9,
                props: { category: 'updates' },
                renderMode: 'frozen',
                frozen: { invented: true },
                span: 6,
              },
            ],
          }),
        ]),
      }),
    );

    const added = allBlocks(answer.body)[1];
    expect(added).toMatchObject({ renderMode: 'live', frozen: null, span: null });
    expect(added?.version).toBe(registry.blocks.find((block) => block.type === 'newsList')?.version);
  });

  test('a block not offered here is refused, and a block of the draft no longer offered stays', async () => {
    const offered = context({ blocks: registry.blocks.filter((block) => block.type === 'text') });

    const added = await fileFor(offered, {
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [text('blk-a'), { type: 'heading', version: 1, props: { level: 2, text: { en: 'x' } } }],
        }),
      ]),
    });
    expect((await refused(added, offered)).problems.map((problem) => problem.key)).toEqual([
      'content.import.blockNotOffered',
    ]);

    expect((await ready(await fileFor(offered), offered)).startedOver).toBe(false);
  });
});

describe('a block of the draft keeps what was chosen in the editor (review of #449, finding 5)', () => {
  const kept = (over: Record<string, unknown>) => ({
    id: 'blk-data',
    type: 'newsList',
    version: 1,
    props: { category: 'updates', limit: 5 },
    ...over,
  });

  const bodyWith = (block: Record<string, unknown>) =>
    withSections([section({ id: 'sec-a', blocks: [text('blk-a'), block] })]);

  test.each([
    ['left out', {}],
    ['changed', { renderMode: 'live', span: 12, frozen: { invented: true }, version: 9 }],
    ['emptied', { renderMode: null, span: null, frozen: null }],
  ])('%s by the file, they survive', async (_name, over) => {
    const answer = await ready(await fileFor(context(), { body: bodyWith(kept(over)) }));

    const block = allBlocks(answer.body)[1];
    expect(block).toMatchObject({
      renderMode: 'frozen',
      span: 6,
      frozen: { items: [1, 2] },
      version: 1,
      // And what the file was there to change, changed.
      props: { category: 'updates', limit: 5 },
    });
  });

  test('an unchanged block in a form its schema no longer takes is not asked anything', async () => {
    const old = context({
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [{ ...text('blk-a'), props: { written: 'by an older version' } }],
        }),
      ]) as Body,
    });

    expect((await ready(await fileFor(old), old)).pending).toEqual([]);
  });

  test('a block of the draft may not become another block', async () => {
    const file = await fileFor(context(), {
      body: bodyWith({ id: 'blk-data', type: 'text', version: 1, props: text('').props }),
    });

    expect((await refused(file)).problems.map((problem) => problem.key)).toContain(
      'content.import.blockChangedType',
    );
  });
});

describe('the schema of every block', () => {
  test('a wrong value is refused, with the path of the field and what zod said', async () => {
    const file = await fileFor(context(), {
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [{ type: 'callout', version: 1, props: { tone: 'purple', text: { en: 'x' } } }],
        }),
      ]),
    });

    const [problem] = (await refused(file)).problems;
    expect(problem?.path).toBe('sections[0].blocks[0].props.tone');
    expect(problem?.key).toBe('content.import.propInvalid');
    expect(problem?.detail).toBeTruthy();
  });

  test('members the schema does not have do not reach the draft', async () => {
    const file = await fileFor(context(), {
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [{ ...text('blk-a'), props: { ...text('').props, colour: 'red' } }],
        }),
      ]),
    });

    expect(allBlocks((await ready(file)).body)[0]?.props).not.toHaveProperty('colour');
  });

  test('an envelope that is not one is refused before anything else is read', async () => {
    const file = await fileFor(context(), { body: withSections([section({ layout: 'circle' })]) });

    const answer = await refused(file);
    expect(answer.problems[0]).toMatchObject({
      path: 'sections[0].layout',
      key: 'content.import.envelopeInvalid',
    });
  });

  test('a title with nothing in it is refused, in any language', async () => {
    const file = await fileFor(context(), { title: { en: ' ', xx: '' } });

    expect((await refused(file)).problems).toContainEqual({ path: 'title', key: 'errors.required' });
  });
});

describe('what only the person can give (note §3.5)', () => {
  const image = (props: Record<string, unknown>) => ({ type: 'image', version: 1, props });
  const asFile = (blocks: unknown[]) =>
    fileFor(context(), { body: withSections([section({ id: 'sec-a', blocks })]) });

  test('a block whose only fault is a picture is taken, and says so', async () => {
    const answer = await ready(await asFile([image({ alt: { en: 'The tower', xx: 'La torre' } })]));

    expect(answer.pending).toMatchObject([{ kind: 'picture', field: 'mediaId' }]);
    expect(allBlocks(answer.body)[0]?.props).not.toHaveProperty('mediaId');
  });

  test('a picture the draft does not have is not a picture: it is taken out', async () => {
    const answer = await ready(
      await asFile([image({ mediaId: 123456, alt: { en: 'The tower', xx: 'La torre' } })]),
    );

    expect(answer.pending).toMatchObject([{ kind: 'picture', field: 'mediaId' }]);
    expect(allBlocks(answer.body)[0]?.props).not.toHaveProperty('mediaId');
  });

  test('a picture of the draft stays, wherever it was', async () => {
    const withPicture = context({
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [{ ...text('blk-a'), type: 'image', props: { mediaId: 55, alt: { en: 'a', xx: 'a' } } }],
        }),
      ]) as Body,
    });

    const file = await fileFor(withPicture, {
      body: withSections([
        section({
          id: 'sec-a',
          blocks: [
            { ...text('blk-a'), type: 'image', props: { mediaId: 55, alt: { en: 'Better', xx: 'Meglio' } } },
          ],
        }),
      ]),
    });

    const answer = await ready(file, withPicture);
    expect(answer.pending).toEqual([]);
    expect(allBlocks(answer.body)[0]?.props).toMatchObject({ mediaId: 55 });
  });

  test('a value of a data block Claude cannot know is left to complete, empty or missing', async () => {
    const missing = await ready(await asFile([{ type: 'newsList', version: 1, props: {} }]));
    expect(missing.pending).toMatchObject([{ kind: 'value', field: 'category' }]);

    const empty = await ready(await asFile([{ type: 'newsList', version: 1, props: { category: '' } }]));
    expect(empty.pending).toMatchObject([{ kind: 'value', field: 'category' }]);
  });

  test('a link to nowhere the instructions listed is said, not refused', async () => {
    const button = (href: string) => ({ label: { en: 'Go', xx: 'Vai' }, href });
    const answer = await ready(
      await asFile([
        {
          type: 'buttonGroup',
          version: 1,
          props: {
            buttons: [
              button('/nowhere'),
              button('/about/#team'),
              button('https://example.org/x'),
              button(''),
            ],
          },
        },
        {
          type: 'text',
          version: 1,
          props: { markdown: { en: 'See [the team](/team) and [us](/about).', xx: 'Vedi [noi](/about).' } },
        },
      ]),
    );

    expect(answer.pending.map(({ kind, address }) => [kind, address])).toEqual([
      ['link', '/nowhere'],
      ['link', ''],
      ['link', '/team'],
    ]);
  });
});

describe('the rules of the template', () => {
  const templated: Body = {
    schemaVersion: 1,
    sections: [
      {
        id: 'sec-need',
        key: 'need',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [text('blk-need')],
        sections: [],
      },
      {
        id: 'sec-lock',
        key: 'lock',
        title: { en: 'Fixed', xx: 'Fisso' },
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [text('blk-lock')],
        sections: [],
      },
      {
        id: 'sec-few',
        key: 'few',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [],
        sections: [],
      },
    ],
  };

  const rules = new Map<string, SectionRule>([
    ['need', { required: true, locked: false, allowedBlocks: null }],
    ['lock', { required: false, locked: true, allowedBlocks: null }],
    ['few', { required: false, locked: false, allowedBlocks: ['heading'] }],
  ]);

  const ctx = context({ body: templated, template: rules });
  const keys = async (file: unknown) => (await refused(file, ctx)).problems.map((problem) => problem.key);

  test('a required section may not go, nor a locked one', async () => {
    const file = await fileFor(ctx, {
      body: withSections([section({ id: 'sec-few' })]),
    });

    expect(await keys(file)).toEqual(['content.import.sectionRequired', 'content.import.sectionRequired']);
  });

  test('a locked section keeps its shape and its blocks, and its texts may change', async () => {
    const changed = structuredClone(templated);
    changed.sections[1]!.blocks[0] = text('blk-lock', 'Rewritten');
    changed.sections[1]!.title = { en: 'Claude renamed it', xx: 'Rinominata' };
    const answer = await ready(await fileFor(ctx, { body: changed }), ctx);
    expect(allBlocks(answer.body).find((block) => block.id === 'blk-lock')?.props).toMatchObject({
      markdown: { en: 'Rewritten' },
    });
    expect(answer.body.sections[1]?.title).toEqual({ en: 'Fixed', xx: 'Fisso' });

    const added = structuredClone(templated) as unknown as { sections: { blocks: unknown[] }[] };
    added.sections[1]!.blocks.push({ type: 'text', version: 1, props: text('').props });
    expect(await keys(await fileFor(ctx, { body: added }))).toEqual(['content.import.sectionLocked']);

    const wider = structuredClone(templated);
    wider.sections[1]!.width = 'wide';
    expect(await keys(await fileFor(ctx, { body: wider }))).toEqual(['content.import.sectionLocked']);
  });

  test('a section that lists its blocks takes only those', async () => {
    const wrong = structuredClone(templated) as unknown as { sections: { blocks: unknown[] }[] };
    wrong.sections[2]!.blocks.push({ type: 'text', version: 1, props: text('').props });

    expect(await keys(await fileFor(ctx, { body: wrong }))).toEqual(['content.import.blockNotAllowed']);
  });

  test("a section keeps its key, and a page writes none of the template's members", async () => {
    const changed = structuredClone(templated) as unknown as {
      sections: Record<string, unknown>[];
    };
    changed.sections[0]!['key'] = 'something-else';
    changed.sections[0]!['required'] = false;
    changed.sections[2]!['locked'] = true;
    changed.sections[2]!['allowedBlocks'] = ['text'];

    const answer = await ready(await fileFor(ctx, { body: changed }), ctx);
    expect(answer.body.sections[0]?.key).toBe('need');
    expect(answer.body.sections[0]).not.toHaveProperty('required');
    expect(answer.body.sections[2]).not.toHaveProperty('locked');
    expect(answer.body.sections[2]).not.toHaveProperty('allowedBlocks');
  });
});

describe('what is left to do, as the body moves on', () => {
  const body = (mediaId?: number): Body =>
    withSections([
      section({ id: 'sec-b' }),
      section({
        id: 'sec-a',
        blocks: [{ ...text('blk-a'), type: 'image', props: mediaId === undefined ? {} : { mediaId } }],
      }),
    ]) as Body;

  const pending = [{ owner: 'blk-a', field: 'mediaId', kind: 'picture' as const }];

  test('is named by where the block is now', () => {
    expect(pendingNow(body(), pending)).toEqual([
      { path: 'sections[1].blocks[0].props.mediaId', entry: pending[0] },
    ]);
  });

  test('leaves the list when it is done, or when the block is gone', () => {
    expect(pendingNow(body(12), pending)).toEqual([]);
    expect(pendingNow({ schemaVersion: 1, sections: [] }, pending)).toEqual([]);
  });
});

describe('the line of the version (answer 6)', () => {
  const t = ((key: string) => key) as unknown as TFunction;

  beforeEach(() => localStorage.clear());

  test('starts from the import, as long as the row has not been published since', () => {
    rememberImported({ id: 7, publishedAt: null });

    expect(versionLine({ id: 7, publishedAt: null }, 'Two blocks.', t)).toBe(
      'content.import.versionLine Two blocks.',
    );
    expect(versionLine({ id: 7, publishedAt: '2026-11-05T10:00:00' }, 'Two blocks.', t)).toBe('Two blocks.');
    expect(versionLine({ id: 8, publishedAt: null }, 'Two blocks.', t)).toBe('Two blocks.');
  });

  test('an import undone is no import', () => {
    rememberImported({ id: 7, publishedAt: null });
    forgetImported(7);

    expect(versionLine({ id: 7, publishedAt: null }, 'Two blocks.', t)).toBe('Two blocks.');
  });
});

describe('the review of #453', () => {
  const interactive = (id: string, source: string | null) => ({
    id,
    type: 'interactive',
    version: 1,
    props: {},
    renderMode: null,
    frozen: null,
    column: null,
    span: null,
    source,
  });

  const withCode = (over: Partial<ImportContext> = {}) =>
    context({
      body: withSections([section({ id: 'sec-a', blocks: [interactive('blk-code', 'CODE')] })]) as Body,
      ...over,
    });

  describe('the code of an interactive block', () => {
    const mayNot = (permission: string) => permission !== 'Content.EmbedCode';
    const dropped = (ctx: ImportContext) =>
      fileFor(ctx, { body: withSections([section({ id: 'sec-a', blocks: [] })]) });

    test('a file may not take away what the person could not put back', async () => {
      const ctx = withCode({ holds: mayNot });

      expect((await refused(await dropped(ctx), ctx)).problems).toContainEqual({
        path: null,
        key: 'content.import.codeNotTheirs',
        values: { type: 'interactive' },
      });
    });

    test('whoever may write code may take it away', async () => {
      const ctx = withCode();

      expect((await ready(await dropped(ctx), ctx)).outcome).toBe('ready');
    });

    test('left out of a kept block, it stays; written, it goes to the server, which decides', async () => {
      const ctx = withCode({ holds: mayNot });

      const omitted = await ready(
        await fileFor(ctx, {
          body: withSections([
            section({
              id: 'sec-a',
              blocks: [{ id: 'blk-code', type: 'interactive', version: 1, props: {} }],
            }),
          ]),
        }),
        ctx,
      );
      expect(allBlocks(omitted.body)[0]?.source).toBe('CODE');

      const written = await ready(
        await fileFor(withCode(), {
          body: withSections([section({ id: 'sec-a', blocks: [interactive('blk-code', 'NEW')] })]),
        }),
        withCode(),
      );
      expect(allBlocks(written.body)[0]?.source).toBe('NEW');
    });

    test('a new block keeps it only if it is the block that carries code', async () => {
      const answer = await ready(
        await fileFor(context(), {
          body: withSections([
            section({
              id: 'sec-a',
              blocks: [
                { type: 'text', version: 1, props: text('').props, source: 'SMUGGLED' },
                {
                  type: 'interactive',
                  version: 1,
                  props: { title: { en: 'A', xx: 'B' } },
                  source: 'WRITTEN',
                },
              ],
            }),
          ]),
        }),
      );

      expect(allBlocks(answer.body).map((block) => block.source)).toEqual([null, 'WRITTEN']);
    });
  });

  test('a kept section newly set on a picture waits for one; one already waiting is not the file’s doing', async () => {
    const newly = await ready(
      await fileFor(context(), {
        body: withSections([section({ id: 'sec-a', background: 'image', blocks: [text('blk-a')] })]),
      }),
    );
    expect(newly.pending).toEqual([{ owner: 'sec-a', field: null, kind: 'picture' }]);

    const waiting = context({
      body: withSections([section({ id: 'sec-a', background: 'image' })]) as Body,
    });
    expect((await ready(await fileFor(waiting), waiting)).pending).toEqual([]);
  });

  test('what is neither of the site nor a whole address is said, and a whole one is not', async () => {
    const button = (href: string) => ({ label: { en: 'Go', xx: 'Vai' }, href });
    const answer = await ready(
      await fileFor(context(), {
        body: withSections([
          section({
            id: 'sec-a',
            blocks: [
              {
                type: 'buttonGroup',
                version: 1,
                props: {
                  buttons: [
                    button('about/team'),
                    button('www.example.org'),
                    button('javascript:alert(1)'),
                    button('//example.org/x'),
                    button('https://example.org/x'),
                    button('mailto:staff@example.org'),
                  ],
                },
              },
              {
                type: 'text',
                version: 1,
                props: { markdown: { en: '[a](about/team) [b](https://example.org)', xx: '[c](/about)' } },
              },
            ],
          }),
        ]),
      }),
    );

    expect(answer.pending.map(({ address }) => address)).toEqual([
      'about/team',
      'www.example.org',
      'javascript:alert(1)',
      '//example.org/x',
      'about/team',
    ]);
  });

  test('a link leaves the list when it is another one, even one that begins the same', () => {
    const bodyWith = (href: string, markdown: string) =>
      withSections([
        section({
          id: 'sec-a',
          blocks: [
            { ...text('blk-b'), type: 'buttonGroup', props: { buttons: [{ label: {}, href }] } },
            { ...text('blk-t'), props: { markdown: { en: markdown } } },
          ],
        }),
      ]) as Body;

    const pending = [
      { owner: 'blk-b', field: 'buttons.0.href', kind: 'link' as const, address: '/about' },
      { owner: 'blk-t', field: 'markdown.en', kind: 'link' as const, address: '/about' },
    ];

    expect(pendingNow(bodyWith('/about', '[a](/about)'), pending)).toHaveLength(2);
    expect(pendingNow(bodyWith('/about/team', '[a](/about/team)'), pending)).toEqual([]);

    const empty = [{ owner: 'blk-b', field: 'buttons.0.href', kind: 'link' as const, address: '' }];
    expect(pendingNow(bodyWith('', ''), empty)).toHaveLength(1);
    expect(pendingNow(bodyWith('/about', ''), empty)).toEqual([]);
  });

  test('the version of a kept block follows what read its props', async () => {
    const older = context({
      body: withSections([section({ id: 'sec-a', blocks: [{ ...text('blk-a'), version: 0 }] })]) as Body,
    });

    const untouched = await ready(await fileFor(older), older);
    expect(allBlocks(untouched.body)[0]?.version).toBe(0);

    const changed = await ready(
      await fileFor(older, {
        body: withSections([section({ id: 'sec-a', blocks: [text('blk-a', 'Rewritten')] })]),
      }),
      older,
    );
    expect(allBlocks(changed.body)[0]?.version).toBe(
      registry.blocks.find((block) => block.type === 'text')?.version,
    );
  });

  test('a member called __proto__ pollutes nothing and reaches no draft', async () => {
    const base = await fileFor(context());
    const file = JSON.stringify(base).replace(
      '"props":{"markdown":{"en":"Hello","xx":"Hello xx"}}',
      '"props":{"__proto__":{"polluted":true},"markdown":{"__proto__":"x","en":"Rewritten","xx":"Riscritta"}}',
    );
    expect(file).toContain('__proto__');

    const answer = await ready(file);

    expect(({} as Record<string, unknown>)['polluted']).toBeUndefined();
    expect(JSON.stringify(answer.body)).not.toContain('polluted');
  });

  test('what an import left survives a reload, and anything else in its place is nothing', () => {
    const pending = [{ owner: 'blk-a', field: 'mediaId', kind: 'picture' as const }];

    rememberLeftovers(7, pending);
    expect(readLeftovers(7)).toEqual(pending);
    expect(readLeftovers(8)).toEqual([]);

    rememberLeftovers(7, []);
    expect(readLeftovers(7)).toEqual([]);

    localStorage.setItem('hub.content.imported.7.left', '{not json');
    expect(readLeftovers(7)).toEqual([]);
    localStorage.setItem('hub.content.imported.7.left', '[{"owner":1}]');
    expect(readLeftovers(7)).toEqual([]);
  });
});
