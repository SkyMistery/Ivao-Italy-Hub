import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import englishCommon from '../../../locales/en/common.json';
import { registry } from '../app/registry';
import type { Resizable } from '../shared/modules';
import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import { readBody, type Align, type Body } from './envelope';
import { PickingContext, type Picking } from './picking';
import { IMAGE_WIDTH } from './schemas';
import { carriesOldSize, sizeOf, sizedAt, snapped } from './size';

/**
 * The width of a picture in its column (PR 3.10, design of the editor's phase 3, §5.3): `widthPercent`, drawn by the
 * renderer's wrapper for a block that declares `resizable`, changed from a handle on its edge while composing, and the
 * `width` of an older body still drawn where it was. And the click that opens the picture at full screen.
 *
 * jsdom does no layout, so what is read here is what the wrapper is told, and the handle is given the room it measures.
 */

const en = (value: string) => ({ en: value, it: value });

function page(props: Record<string, unknown>, align: Align | null = null): Body {
  return readBody({
    schemaVersion: 1,
    sections: [{ id: 's_1', blocks: [{ id: 'b_1', type: 'image', version: 1, props, align }] }],
  });
}

const picture = { mediaId: 7, alt: en('A runway at dawn') };

/** The wrapper that takes the block's share of its column. */
const wrapperOf = (container: HTMLElement) => container.querySelector<HTMLElement>('[data-resizable]');

function composing(onResize: Picking['onResize'], selected: string | null = 'b_1'): Picking {
  return {
    selected,
    onPick: vi.fn(),
    target: null,
    onPickColumn: vi.fn(),
    accepts: () => true,
    ...(onResize === undefined ? {} : { onResize }),
  };
}

describe('the width', () => {
  test('is a share of the column between a fifth and all of it, in steps of five from the handle', () => {
    expect(snapped(IMAGE_WIDTH, 47)).toBe(45);
    expect(snapped(IMAGE_WIDTH, 48)).toBe(50);
    expect(snapped(IMAGE_WIDTH, 3)).toBe(20);
    expect(snapped(IMAGE_WIDTH, 140)).toBe(100);
  });

  test('of an older body is read from its `width`, until the property is written', () => {
    const old = { mediaId: 7, width: 'third' };

    expect(carriesOldSize(IMAGE_WIDTH, old)).toBe(true);
    expect(sizeOf(IMAGE_WIDTH, old)).toBe(33);
    expect(sizeOf(IMAGE_WIDTH, { mediaId: 7, width: 'half' })).toBe(50);
    // The new one wins over the old, and a value that is no width is none at all.
    expect(sizeOf(IMAGE_WIDTH, { ...old, widthPercent: 70 })).toBe(70);
    expect(carriesOldSize(IMAGE_WIDTH, { ...old, widthPercent: 70 })).toBe(false);
    expect(sizeOf(IMAGE_WIDTH, { widthPercent: 7 })).toBe(100);
    expect(sizeOf(IMAGE_WIDTH, { width: 'huge' })).toBe(100);
    expect(sizeOf(IMAGE_WIDTH, {})).toBe(100);
  });

  test('written takes the old one out, so that it cannot come back from under the new', () => {
    expect(sizedAt(IMAGE_WIDTH, { mediaId: 7, width: 'half', rounded: true }, 60)).toEqual({
      mediaId: 7,
      rounded: true,
      widthPercent: 60,
    });
  });

  test('every resizable block holds it in a whole number its schema keeps within the limits', () => {
    const resizable = registry.blocks.filter((block) => block.resizable !== undefined);
    expect(resizable.map((block) => block.type)).toEqual(['image']);

    for (const block of resizable) {
      const { prop, min, max, old } = block.resizable as Resizable;
      const at = (value: unknown) => block.schema.safeParse({ ...block.example, [prop]: value }).success;

      expect(at(min), block.type).toBe(true);
      expect(at(max), block.type).toBe(true);
      expect(at(min - 1), block.type).toBe(false);
      expect(at(max + 1), block.type).toBe(false);
      expect(at(min + 0.5), block.type).toBe(false);
      // A fresh block takes its whole column, as every block did before it had a width.
      expect(block.schema.parse({ ...block.example, [prop]: undefined })[prop], block.type).toBe(max);
      // What an older body said is a width the schema accepts: it is written as it is, by the first write.
      for (const value of Object.values(old?.values ?? {})) {
        expect(at(value), `${block.type}: ${value}`).toBe(true);
      }
    }
  });
});

describe('the renderer', () => {
  test('gives a picture its share of the column, at least 10 rem, and all of it in a narrow column', () => {
    const { container } = renderWithProviders(
      <ContentRenderer body={page({ ...picture, widthPercent: 45 })} />,
    );

    const wrapper = wrapperOf(container)!;
    expect(wrapper.style.getPropertyValue('--block-width')).toBe('45%');
    expect(wrapper.className).toContain('w-(--block-width)');
    expect(wrapper.className).toContain('min-w-[min(100%,10rem)]');
    expect(wrapper.className).toContain('@max-sm:w-full');
    expect(within(wrapper).getByRole('img', { name: 'A runway at dawn' })).toBeInTheDocument();
  });

  test('draws a picture of an older body as wide as its `width` said', () => {
    const { container } = renderWithProviders(<ContentRenderer body={page({ ...picture, width: 'half' })} />);

    expect(wrapperOf(container)!.style.getPropertyValue('--block-width')).toBe('50%');
  });

  test('puts the share where the alignment says, inside it', () => {
    const { container } = renderWithProviders(
      <ContentRenderer body={page({ ...picture, widthPercent: 40 }, 'center')} />,
    );

    // The alignment pushes the share to its side: the share is its direct child.
    expect(wrapperOf(container)!.parentElement).toHaveAttribute('data-align', 'center');
  });

  test('gives a visitor no handle', () => {
    const { container } = renderWithProviders(
      <ContentRenderer body={page({ ...picture, widthPercent: 40 })} />,
    );

    expect(container.querySelector('[data-resize-handle]')).toBeNull();
  });

  test('draws the handle only on the picked block, and only where the body may be written', () => {
    const draw = (picking: Picking) =>
      renderWithProviders(
        <PickingContext.Provider value={picking}>
          <ContentRenderer body={page({ ...picture, widthPercent: 40 })} staff />
        </PickingContext.Provider>,
      );

    const picked = draw(composing(vi.fn()));
    const handle = picked.container.querySelector('[data-resize-handle]');
    expect(handle).not.toBeNull();
    // The pointer's: the keyboard's road is the slider of the panel, and the same value is not read twice.
    expect(handle).toHaveAttribute('aria-hidden', 'true');
    expect(handle).toHaveAttribute('data-chrome');
    expect(handle?.className).toContain('@max-sm:hidden');
    picked.unmount();

    const other = draw(composing(vi.fn(), 'b_other'));
    expect(other.container.querySelector('[data-resize-handle]')).toBeNull();
    other.unmount();

    const locked = draw(composing(undefined));
    expect(locked.container.querySelector('[data-resize-handle]')).toBeNull();
  });
});

describe('the handle', () => {
  /** A drag from the handle to `to`, in a room 400 pixels wide that starts at 100. */
  function drag(align: Align | null, to: number[]) {
    const onResize = vi.fn<(id: string, width: number, gesture: number) => void>();
    const { container } = renderWithProviders(
      <PickingContext.Provider value={composing(onResize)}>
        <ContentRenderer body={page({ ...picture, widthPercent: 50 }, align)} staff />
      </PickingContext.Provider>,
    );

    const room = wrapperOf(container)!.parentElement!;
    room.getBoundingClientRect = () =>
      ({ left: 100, right: 500, width: 400, top: 0, bottom: 300, height: 300, x: 100, y: 0 }) as DOMRect;

    const handle = container.querySelector('[data-resize-handle]')!;
    fireEvent.pointerDown(handle, { clientX: 300 });
    for (const clientX of to) {
      window.dispatchEvent(new MouseEvent('pointermove', { clientX }));
    }
    window.dispatchEvent(new MouseEvent('pointerup'));

    return onResize;
  }

  test('on the left of its column, makes it as wide as the pointer is from the left edge', () => {
    const onResize = drag(null, [280, 281, 400]);

    // 180 of 400 is 45; a pixel more is still 45, and says nothing; 300 of 400 is 75.
    expect(onResize.mock.calls.map(([id, width]) => [id, width])).toEqual([
      ['b_1', 45],
      ['b_1', 75],
    ]);
  });

  test('in the middle, moves both edges: the pointer is half the width from the middle', () => {
    // The middle of the room is at 300: 120 pixels from it is 240 of 400.
    expect(drag('center', [420]).mock.calls.at(-1)?.[1]).toBe(60);
    expect(drag('center', [460]).mock.calls.at(-1)?.[1]).toBe(80);
    // Where it already is, nothing is written.
    expect(drag('center', [400])).not.toHaveBeenCalled();
  });

  test('on the right, is on the left edge, and the width is what is left of the pointer', () => {
    expect(drag('right', [340]).mock.calls.at(-1)?.[1]).toBe(40);
  });

  test('is one gesture from the press to the release, and the next drag another', () => {
    const first = drag(null, [200, 300]);
    const gestures = new Set(first.mock.calls.map(([, , gesture]) => gesture));
    expect(gestures.size).toBe(1);

    const second = drag(null, [200]);
    expect(gestures.has(second.mock.calls[0]![2])).toBe(false);
  });

  test('swallows the click its release makes, so the release does not pick what is under the pointer', () => {
    const onPick = vi.fn();
    const { container } = renderWithProviders(
      <PickingContext.Provider value={{ ...composing(vi.fn()), onPick }}>
        <ContentRenderer body={page({ ...picture, widthPercent: 50 })} staff />
      </PickingContext.Provider>,
    );

    const handle = container.querySelector('[data-resize-handle]')!;
    fireEvent.pointerDown(handle, { clientX: 300 });
    window.dispatchEvent(new MouseEvent('pointerup'));
    fireEvent.click(container.querySelector('section')!);

    expect(onPick).not.toHaveBeenCalled();
  });
});

describe('at full screen', () => {
  const words = englishCommon.blocks.image;

  test('a click opens the picture in a dialog named by its alternative text, and Esc closes it', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <ContentRenderer body={page({ ...picture, caption: en('Runway 34 at Linate'), widthPercent: 40 })} />,
    );

    await user.click(screen.getByRole('button', { name: `Show at full screen: A runway at dawn` }));

    const dialog = await screen.findByRole('dialog', { name: 'A runway at dawn' });
    expect(within(dialog).getByRole('img', { name: 'A runway at dawn' })).toHaveClass('object-contain');
    expect(within(dialog).getByText('Runway 34 at Linate')).toBeInTheDocument();
    // The «X» says its word in the language on screen (the core's `Dialog`).
    expect(within(dialog).getByRole('button', { name: englishCommon.common.close })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  test('a decorative picture still opens, and its window is named by the caption or by the block', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ContentRenderer body={page({ mediaId: 7 })} />);

    await user.click(screen.getByRole('button', { name: words.enlarge }));
    expect(await screen.findByRole('dialog', { name: words.label })).toBeInTheDocument();
  });

  test('is a choice of the block: switched off, the picture is a picture and nothing else', () => {
    renderWithProviders(<ContentRenderer body={page({ ...picture, fullscreen: false })} />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'A runway at dawn' })).toBeInTheDocument();
  });

  test('does not open while the page is composed: the click picks the block', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    renderWithProviders(
      <PickingContext.Provider value={{ ...composing(vi.fn(), null), onPick }}>
        <ContentRenderer body={page(picture)} staff />
      </PickingContext.Provider>,
    );

    await user.click(screen.getByRole('button', { name: `Show at full screen: A runway at dawn` }));

    expect(onPick).toHaveBeenCalledWith('block', 'b_1');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
