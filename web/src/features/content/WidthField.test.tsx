import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { BlockEnvelope } from '../../blocks';
import { createTestI18n } from '../../test/harness';

import { BlockProperties } from './BlockProperties';

/**
 * The width of a picture in its panel (PR 3.10, design of the editor's phase 3, §5.3): a slider beside the generated
 * form, the keyboard's road to what the handle on the page does; a picture of an older body shown at its old width and
 * moved to the new property by the first thing the panel writes, never by opening it; and the width the handle wrote
 * while the panel was open never written back by the form.
 */

const label = englishCommon.blocks.image.fields.widthPercent;
const en = (value: string) => ({ en: value, it: value });

function blockOf(props: Record<string, unknown>): BlockEnvelope {
  return {
    id: 'b_1',
    type: 'image',
    version: 1,
    props,
    renderMode: null,
    frozen: null,
    column: 0,
    source: null,
    span: null,
    align: null,
  };
}

function panel(block: BlockEnvelope, handlers: ReturnType<typeof spies>) {
  return (
    <BlockProperties
      block={block}
      section={{
        id: 's_1',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [block],
        sections: [],
      }}
      sections={[{ value: 's_1', label: 'Intro' }]}
      locales={['it', 'en']}
      division={{ defaultLocale: 'it', timezone: 'Europe/Rome' }}
      mediaLibrary={{
        queryKey: ['media'],
        queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 20, total: 0 }),
      }}
      onApplyProps={handlers.onApplyProps}
      onEnvelope={handlers.onEnvelope}
      onMoveTo={vi.fn()}
      onResize={handlers.onResize}
    />
  );
}

function spies() {
  return {
    onEnvelope: vi.fn<(patch: Partial<BlockEnvelope>) => void>(),
    onApplyProps: vi.fn<(props: Record<string, unknown>) => void>(),
    onResize: vi.fn<(width: number, gesture: number) => void>(),
  };
}

function draw(block: BlockEnvelope) {
  const handlers = spies();
  // The providers as a wrapper, which a rerender keeps: the panel is drawn again with a newer block, as the editor does.
  const i18n = createTestI18n();
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </I18nextProvider>
  );

  const rendered = render(panel(block, handlers), { wrapper });
  return { ...handlers, rerender: (next: BlockEnvelope) => rendered.rerender(panel(next, handlers)) };
}

/** The caption's box in the language on screen: the second translated text of the form, after the alternative text. */
const captionBox = () => screen.getAllByRole('textbox')[1]!;

test('a picture has a slider for its width, and the form draws no second field for it', () => {
  draw(blockOf({ mediaId: 7, widthPercent: 45 }));

  const slider = screen.getByRole('slider', { name: label });
  expect(slider).toHaveAttribute('aria-valuenow', '45');
  expect(slider).toHaveAttribute('aria-valuemin', '20');
  expect(slider).toHaveAttribute('aria-valuemax', '100');
  expect(slider).toHaveAttribute('aria-valuetext', '45%');
  expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
});

test('a key is one step of five, and each key a gesture of its own', () => {
  const { onResize } = draw(blockOf({ mediaId: 7, widthPercent: 45 }));

  const slider = screen.getByRole('slider', { name: label });
  fireEvent.keyDown(slider, { key: 'ArrowRight' });
  fireEvent.keyDown(slider, { key: 'ArrowLeft' });

  expect(onResize.mock.calls.map(([width]) => width)).toEqual([50, 40]);
  expect(onResize.mock.calls[0]![1]).not.toBe(onResize.mock.calls[1]![1]);
});

test('a picture written before shows its old width, and opening it writes nothing', () => {
  const { onApplyProps, onEnvelope, onResize } = draw(blockOf({ mediaId: 7, width: 'half' }));

  expect(screen.getByRole('slider', { name: label })).toHaveAttribute('aria-valuenow', '50');
  // Opening a block never changes a page by itself (design §2, principle 4).
  expect(onApplyProps).not.toHaveBeenCalled();
  expect(onEnvelope).not.toHaveBeenCalled();
  expect(onResize).not.toHaveBeenCalled();
});

test('its first write moves the old width onto the new property, with the props being written', async () => {
  const user = userEvent.setup();
  const { onApplyProps } = draw(blockOf({ mediaId: 7, width: 'third', alt: en('A runway') }));

  await user.type(captionBox(), 'At dawn');

  await vi.waitFor(() => expect(onApplyProps).toHaveBeenCalled());
  const written = onApplyProps.mock.calls.at(-1)![0];
  expect(written.widthPercent).toBe(33);
  expect(written).not.toHaveProperty('width');
  expect(written.caption).toMatchObject({ en: 'At dawn' });
});

test('the width the handle wrote while the panel was open is not written back by the form', async () => {
  const user = userEvent.setup();
  const { onApplyProps, rerender } = draw(blockOf({ mediaId: 7, widthPercent: 100 }));

  // The handle on the page writes the block, and the panel is drawn again with it; the live form keeps what it was
  // born with.
  rerender(blockOf({ mediaId: 7, widthPercent: 60 }));
  expect(screen.getByRole('slider', { name: label })).toHaveAttribute('aria-valuenow', '60');

  await user.type(captionBox(), 'At dawn');

  await vi.waitFor(() => expect(onApplyProps).toHaveBeenCalled());
  expect(onApplyProps.mock.calls.at(-1)![0].widthPercent).toBe(60);
});

test('a block without a width of its own has no slider', () => {
  draw({ ...blockOf({ level: 2, text: en('A heading') }), type: 'heading' });

  expect(screen.queryByRole('slider')).not.toBeInTheDocument();
});
