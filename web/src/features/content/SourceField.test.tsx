import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { BlockEnvelope } from '../../blocks';
import { renderWithProviders } from '../../test/harness';

import { BlockProperties } from './BlockProperties';

/**
 * The code of an interactive block, and the file a coordinator hands over instead of pasting
 * (12 September 2026, `decisions/2026-09-12-il-blocco-interattivo.md`).
 *
 * ⚠️ Nothing is uploaded. The file is read in the browser and its text becomes the field, which is
 * the whole design: the fragment is a field of the row, and a file living somewhere on the server
 * would be a second place for the same text — the option Carmine ruled out before any other.
 *
 * What is worth a test is the two refusals, because both are somebody's afternoon otherwise: a file
 * too big is caught here rather than by the server at save, and a **whole page** instead of a
 * fragment is caught here rather than by a frame that comes up blank and says nothing.
 */

const words = englishCommon.blocks.interactive;

const block: BlockEnvelope = {
  id: 'b_1',
  type: 'interactive',
  version: 1,
  props: { title: { en: 'A circuit', it: 'Un circuito' }, minHeight: 320 },
  renderMode: null,
  frozen: null,
  column: 0,
  source: null,
};

const section = {
  id: 's_1',
  layout: 'stacked' as const,
  background: 'none' as const,
  padding: 'md' as const,
  width: 'default' as const,
  blocks: [block],
  sections: [],
};

function draw(onEnvelope: (patch: Partial<BlockEnvelope>) => void) {
  renderWithProviders(
    <BlockProperties
      block={block}
      section={section}
      sections={[{ value: 's_1', label: 'Intro' }]}
      locales={['it', 'en']}
      division={{ defaultLocale: 'it', timezone: 'Europe/Rome' }}
      mediaLibrary={{
        queryKey: ['media'],
        queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 20, total: 0 }),
      }}
      onApplyProps={vi.fn()}
      onEnvelope={onEnvelope}
      onMoveTo={vi.fn()}
    />,
  );
}

function fileOf(content: string, name = 'circuit.html') {
  return new File([content], name, { type: 'text/html' });
}

test('a file from the machine becomes the code, and is applied at once', async () => {
  const user = userEvent.setup();
  const onEnvelope = vi.fn();
  draw(onEnvelope);

  const fragment = '<svg viewBox="0 0 10 10"><title>A circuit</title></svg>';
  await user.upload(screen.getByLabelText(words.fromFile), fileOf(fragment));

  // At once, and not on blur like typing: choosing a file is a finished act rather than a keystroke
  // in the middle of a script.
  expect(onEnvelope).toHaveBeenCalledWith({ source: fragment });
  expect(screen.getByLabelText(words.source)).toHaveValue(fragment);
});

test('a whole page is refused, with the reason a blank frame would never give', async () => {
  const user = userEvent.setup();
  const onEnvelope = vi.fn();
  draw(onEnvelope);

  await user.upload(
    screen.getByLabelText(words.fromFile),
    fileOf('<!doctype html><html><body><svg /></body></html>'),
  );

  expect(await screen.findByText(words.fileIsAPage)).toBeInTheDocument();
  expect(onEnvelope).not.toHaveBeenCalled();
});

test('a file over the ceiling is refused here rather than by the server', async () => {
  const user = userEvent.setup();
  const onEnvelope = vi.fn();
  draw(onEnvelope);

  await user.upload(screen.getByLabelText(words.fromFile), fileOf('x'.repeat(64 * 1024 + 1)));

  expect(
    await screen.findByText(words.fileTooLarge.replace('{{max}}', String(64 * 1024))),
  ).toBeInTheDocument();
  expect(onEnvelope).not.toHaveBeenCalled();
});

test('and a whole page pasted into the box is refused the same way', async () => {
  const user = userEvent.setup();
  const onEnvelope = vi.fn();
  draw(onEnvelope);

  // ⚠️ The same content, the other road in. The file had this check and the box did not, so an
  // assistant's fragment was refused when chosen and accepted when pasted — and a page pasted that
  // way reached the frame, which drew nothing and said nothing about why.
  const box = screen.getByLabelText(words.source);
  await user.click(box);
  await user.paste('<!doctype html><html><body><svg /></body></html>');
  await user.tab();

  expect(await screen.findByText(words.fileIsAPage)).toBeInTheDocument();
  expect(onEnvelope).not.toHaveBeenCalled();
});
