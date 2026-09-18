import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { CORE_BLOCK_TYPES, type Body } from '../../blocks';
import type { MediaLibraryQuery } from '../../shared/ui';
import { renderWithProviders } from '../../test/harness';

import { BodyEditor } from './BodyEditor';

/**
 * The editor of a body on its own, as a tour's briefing uses it (T6b): a body goes in, the body changed comes out, and
 * the way back comes out too. Nothing here knows about a row of the content — no metadata, no address, no template, no
 * save — which is the whole point of having taken it out of `ContentEditor`.
 */

const editor = englishCommon.content.editor;

const initial: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's1',
      key: null,
      title: { en: 'Intro' },
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [],
      sections: [],
    },
  ],
} as unknown as Body;

const mediaLibrary = {
  queryKey: ['media', 'test'],
  queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 50, total: 0 }),
} as unknown as MediaLibraryQuery;

function draw(holds: (permission: string) => boolean = () => false) {
  const onChange = vi.fn<(body: Body) => void>();

  renderWithProviders(
    <BodyEditor
      initial={initial}
      onChange={onChange}
      toolbar={(tools) => <div role="toolbar">{tools}</div>}
      locales={['en']}
      division={{ defaultLocale: 'en', timezone: 'UTC' }}
      mediaLibrary={mediaLibrary}
      holds={holds}
    />,
  );

  return { onChange, last: () => onChange.mock.lastCall?.[0] };
}

test('a body goes in, and the body with a block added comes out', async () => {
  const user = userEvent.setup();
  const { onChange, last } = draw();

  // Nothing is said before anything changes: the owner already has the body it handed in.
  expect(onChange).not.toHaveBeenCalled();
  // With no fields of its own to show, the panel says what to do instead.
  expect(screen.getByText(editor.pickHint)).toBeVisible();

  await user.click(screen.getByRole('button', { name: editor.outline }));
  await user.click(screen.getByRole('button', { name: 'Intro' }));
  await user.click(screen.getByRole('button', { name: englishCommon.blocks.heading.label }));

  const changed = last();
  expect(changed?.sections[0]?.blocks.map((block) => block.type)).toEqual([CORE_BLOCK_TYPES.heading]);
  // The body handed in is not the body changed: the owner's copy is its own.
  expect(initial.sections[0]?.blocks).toEqual([]);
});

test('undo gives the body back, and says so', async () => {
  const user = userEvent.setup();
  const { last } = draw();

  await user.click(screen.getByRole('button', { name: editor.outline }));
  await user.click(screen.getByRole('button', { name: 'Intro' }));
  await user.click(screen.getByRole('button', { name: englishCommon.blocks.heading.label }));
  await user.click(screen.getByRole('button', { name: editor.undo }));

  expect(last()).toEqual(initial);

  await user.click(screen.getByRole('button', { name: editor.redo }));
  expect(last()?.sections[0]?.blocks).toHaveLength(1);
});

test.each([
  [true, 1],
  [false, 0],
])(
  'the palette offers a block that asks for a permission only to whoever holds it (%s)',
  async (held, shown) => {
    const user = userEvent.setup();
    draw(() => held);

    // Found by name rather than by opening its drawer: the search opens every drawer that has a match. A briefing
    // answers no to every permission, and the interactive block — the one that asks — is not offered there.
    await user.type(screen.getByRole('searchbox', { name: editor.searchComponents }), 'interactive');

    expect(screen.queryAllByRole('button', { name: englishCommon.blocks.interactive.label })).toHaveLength(
      shown,
    );
  },
);
