import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';

import englishCommon from '../../../locales/en/common.json';
import { renderWithProviders } from '../test/harness';

import { GalleryBlock, ImageBlock } from './blocks';
import { ContentRenderer } from './ContentRenderer';
import { readBody, type Body } from './envelope';
import { PickingContext } from './picking';
import { PrintContext } from './print';
import { GALLERY_LAYOUTS, gallerySchema } from './schemas';

/**
 * The gallery of PR 3.11 (design of the editor's phase 3, §5.2): three layouts, the words of every picture, the full
 * screen with the arrows to the others, and paper. jsdom does no layout, so what is read here is what is drawn and what
 * it is called; the scrolling and the measures are in `e2e/gallery.spec.ts`.
 */

const en = (value: string) => ({ en: value, it: value });
const words = englishCommon.blocks.gallery;
const image = englishCommon.blocks.image;

const images = [
  { mediaId: 1, alt: en('Linate at dawn'), caption: en('Runway 35') },
  { mediaId: 2, alt: en('The tower') },
  { mediaId: 3, caption: en('Only a caption') },
];

const enlarge = (name: string) => image.enlargeNamed.replace('{{name}}', name);
const position = (index: number, count: number) =>
  words.position.replace('{{index}}', String(index)).replace('{{count}}', String(count));

describe('the layout', () => {
  test('of a body written before has none, and draws the grid it was, its pictures decoration', () => {
    // The shape of a gallery saved before PR 3.11: the schema reads it, and the grid is its default.
    const old = { images: [{ mediaId: 4 }, { mediaId: 5 }], columns: 4, lightbox: false };
    expect(gallerySchema.parse(old)).toEqual({ ...old, layout: 'grid' });

    const { container } = renderWithProviders(<GalleryBlock props={old} />);

    const grid = screen.getByRole('list');
    expect(grid).toHaveClass('grid', '@view-md:grid-cols-4');
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
    // Without words a picture is decoration, which it always was: never the name of a file read aloud.
    expect(container.querySelectorAll('img[alt=""]')).toHaveLength(2);
  });

  test('of the grid says what every picture shows, with its caption under it', () => {
    renderWithProviders(<GalleryBlock props={{ images, layout: 'grid', columns: 3, lightbox: false }} />);

    expect(screen.getByRole('img', { name: 'Linate at dawn' })).toHaveAttribute('src', '/media/1/file');
    expect(screen.getByRole('img', { name: 'The tower' })).toBeInTheDocument();
    expect(screen.getByText('Runway 35').tagName).toBe('FIGCAPTION');
    expect(screen.getByText('Only a caption')).toBeInTheDocument();
  });

  test('of the carousel is a named carousel of named slides, with arrows in the language on screen', () => {
    renderWithProviders(<GalleryBlock props={{ images, layout: 'carousel', columns: 3, lightbox: true }} />);

    const carousel = screen.getByRole('region', { name: words.label });
    expect(carousel).toHaveAttribute('aria-roledescription', 'carousel');
    const slides = within(carousel).getAllByRole('group');
    expect(slides.map((slide) => slide.getAttribute('aria-label'))).toEqual([
      position(1, 3),
      position(2, 3),
      position(3, 3),
    ]);
    // Atmosphere's arrows say English words written into the library; the name given here is the one read.
    expect(within(carousel).getByRole('button', { name: words.previous })).toBeInTheDocument();
    expect(within(carousel).getByRole('button', { name: words.next })).toBeInTheDocument();
    // The columns are the grid's alone.
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  test('of the strip is a region that scrolls and takes the focus, even when its pictures do not open', () => {
    renderWithProviders(<GalleryBlock props={{ images, layout: 'strip', columns: 3, lightbox: false }} />);

    const strip = screen.getByRole('region', { name: words.label });
    expect(strip).toHaveAttribute('tabindex', '0');
    expect(strip).toHaveClass('overflow-x-auto', 'snap-x');
    expect(within(strip).getAllByRole('listitem')).toHaveLength(3);
    expect(within(strip).getAllByRole('listitem')[0]).toHaveClass('snap-start');
  });

  test('on paper is the grid, whichever was chosen: nothing scrolls there, and every picture is printed', () => {
    for (const layout of GALLERY_LAYOUTS) {
      const { unmount } = renderWithProviders(
        <PrintContext.Provider value={true}>
          <GalleryBlock props={{ images, layout, columns: 2, lightbox: true }} />
        </PrintContext.Provider>,
      );

      expect(screen.getByRole('list'), layout).toHaveClass('grid', '@view-sm:grid-cols-2');
      expect(screen.queryByRole('region'), layout).not.toBeInTheDocument();
      expect(screen.getAllByRole('listitem'), layout).toHaveLength(3);
      unmount();
    }
  });

  test('an entry without a file draws nothing, and a gallery with none draws nothing at all', () => {
    const { unmount } = renderWithProviders(
      <GalleryBlock props={{ images: [{}, { mediaId: 2, alt: en('The tower') }], layout: 'strip' }} />,
    );
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
    unmount();

    const { container } = renderWithProviders(<GalleryBlock props={{ images: [{}], layout: 'carousel' }} />);
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });
});

describe('at full screen', () => {
  test('opens on the picture clicked, goes through the others both ways and round, and gives the focus back', async () => {
    const user = userEvent.setup();
    renderWithProviders(<GalleryBlock props={{ images, layout: 'grid', columns: 3, lightbox: true }} />);

    const opener = screen.getByRole('button', { name: enlarge('The tower') });
    await user.click(opener);

    let dialog = await screen.findByRole('dialog', { name: 'The tower' });
    expect(within(dialog).getByRole('img', { name: 'The tower' })).toHaveAttribute('src', '/media/2/file');
    expect(within(dialog).getByText(position(2, 3))).toHaveAttribute('aria-live', 'polite');

    // Forward: the third picture has no alternative text, and its caption names the window.
    await user.click(within(dialog).getByRole('button', { name: words.next }));
    dialog = screen.getByRole('dialog', { name: 'Only a caption' });
    expect(within(dialog).getByText(position(3, 3))).toBeInTheDocument();
    expect(dialog.querySelector('img')).toHaveAttribute('src', '/media/3/file');

    // Past the last comes the first, with its caption under it.
    await user.click(within(dialog).getByRole('button', { name: words.next }));
    dialog = screen.getByRole('dialog', { name: 'Linate at dawn' });
    expect(within(dialog).getByText('Runway 35')).toBeInTheDocument();

    // The arrow keys, wherever the focus is in the window: back from the first is the last.
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('dialog', { name: 'Only a caption' })).toBeInTheDocument();
    await user.keyboard('{ArrowLeft}');
    expect(screen.getByRole('dialog', { name: 'The tower' })).toBeInTheDocument();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    // Back on the picture that opened it, as a dialog should.
    expect(opener).toHaveFocus();

    // Opened again from the first picture, it opens there, not where it was left.
    await user.click(screen.getByRole('button', { name: enlarge('Linate at dawn') }));
    expect(await screen.findByRole('dialog', { name: 'Linate at dawn' })).toBeInTheDocument();
  });

  // That the carousel behind does not turn with them is measured in a browser (`e2e/gallery.spec.ts`): here nothing scrolls.
  test('from the carousel, the arrow keys turn the window opened from a slide', async () => {
    const user = userEvent.setup();
    renderWithProviders(<GalleryBlock props={{ images, layout: 'carousel', lightbox: true }} />);

    await user.click(screen.getByRole('button', { name: enlarge('Linate at dawn') }));
    await screen.findByRole('dialog', { name: 'Linate at dawn' });

    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('dialog', { name: 'The tower' })).toBeInTheDocument();
  });

  test('of a picture alone has no arrows: the picture block hands it one picture', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ImageBlock props={{ mediaId: 7, alt: en('A runway') }} />);

    await user.click(screen.getByRole('button', { name: enlarge('A runway') }));
    const dialog = await screen.findByRole('dialog', { name: 'A runway' });

    expect(within(dialog).queryByRole('button', { name: words.next })).not.toBeInTheDocument();
    expect(within(dialog).queryByText(position(1, 1))).not.toBeInTheDocument();
    // And the arrow keys do nothing to it.
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('dialog', { name: 'A runway' })).toBeInTheDocument();
  });

  test('does not open while the page is composed: the click picks the block', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    const body: Body = readBody({
      schemaVersion: 1,
      sections: [
        {
          id: 's_1',
          blocks: [{ id: 'b_1', type: 'gallery', version: 1, props: { images, layout: 'carousel' } }],
        },
      ],
    });
    renderWithProviders(
      <PickingContext.Provider
        value={{ selected: null, onPick, target: null, onPickColumn: vi.fn(), accepts: () => true }}
      >
        <ContentRenderer body={body} staff />
      </PickingContext.Provider>,
    );

    await user.click(screen.getByRole('button', { name: enlarge('The tower') }));

    expect(onPick).toHaveBeenCalledWith('block', 'b_1');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
