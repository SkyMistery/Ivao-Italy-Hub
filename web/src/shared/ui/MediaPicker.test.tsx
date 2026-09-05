import { queryOptions } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { MediaPicker, type PickableMedia } from './MediaPicker';

/**
 * The picker is the reason a media identifier is never a number somebody types (design M1 §1.5), so
 * what these tests hold onto is that choosing is a gesture on a picture: a name that reads well
 * would be no better than a text box.
 *
 * They also pin the two things a test *can* see about a picture — that an image draws itself with
 * an alternative text and that a document does not pretend to be one — because a broken image and
 * a file that failed to upload look exactly alike on screen.
 */

const IMAGE: PickableMedia = {
  id: 7,
  fileName: 'banner.png',
  contentType: 'image/png',
  alt: { en: 'A runway at dawn', it: 'Una pista all alba' },
  url: '/media/7/banner.png',
};

const DOCUMENT: PickableMedia = {
  id: 8,
  fileName: 'briefing.pdf',
  contentType: 'application/pdf',
  alt: {},
  url: '/media/8/briefing.pdf',
};

function pageOf(items: PickableMedia[]) {
  return queryOptions({
    queryKey: ['media-picker-test', items.map((item) => item.id)] as const,
    queryFn: () => Promise.resolve({ items, total: items.length }),
  });
}

function renderPicker(items: PickableMedia[], value: number | null, onChange = vi.fn()) {
  renderWithProviders(
    <MediaPicker query={pageOf(items)} value={value} onChange={onChange} locale="en" defaultLocale="en" />,
  );

  return onChange;
}

test('a file is chosen by pressing its picture, not by typing its number', async () => {
  const onChange = renderPicker([IMAGE, DOCUMENT], null);

  const button = await screen.findByRole('button', { name: /banner\.png/ });
  await userEvent.click(button);

  expect(onChange).toHaveBeenCalledWith(IMAGE.id);
  // No way of setting an identifier by hand: the whole point is that a page cannot end up pointing
  // at a file that was deleted years ago.
  expect(screen.queryByRole('spinbutton')).toBeNull();
});

test('the chosen file says so, and pressing it again lets go of it', async () => {
  const onChange = renderPicker([IMAGE, DOCUMENT], IMAGE.id);

  const chosen = await screen.findByRole('button', { name: /banner\.png/ });
  expect(chosen).toHaveAttribute('aria-pressed', 'true');

  const other = screen.getByRole('button', { name: /briefing\.pdf/ });
  expect(other).toHaveAttribute('aria-pressed', 'false');

  await userEvent.click(chosen);
  expect(onChange).toHaveBeenCalledWith(null);
});

test('an image carries its alternative text and a document is not drawn as one', async () => {
  renderPicker([IMAGE, DOCUMENT], null);

  // The alternative text written next to the file is what a screen reader is given, in the language
  // on screen: it is written once and inherited by every block that shows the file.
  expect(await screen.findByRole('img', { name: 'A runway at dawn' })).toBeInTheDocument();

  // One image on the screen, not two: the PDF shows what it is instead of a picture that failed.
  expect(screen.getAllByRole('img')).toHaveLength(1);
});

test('a file with no alternative text yet is still announced by its name', async () => {
  renderPicker([{ ...IMAGE, alt: {} }], null);

  expect(await screen.findByRole('img', { name: 'banner.png' })).toBeInTheDocument();
});

test('an empty library says so instead of showing an empty grid', async () => {
  renderPicker([], null);

  await waitFor(() => expect(screen.getByText('No files yet')).toBeInTheDocument());
  expect(screen.queryByRole('list')).toBeNull();
});

test('the picker lets go of a file through a control of its own, not only by pressing it again', async () => {
  // A grid with nothing selected offers no gesture for "none": without this the first choice a
  // coordinator makes would be permanent unless they guessed that pressing again undoes it.
  const onChange = vi.fn();
  renderWithProviders(
    <MediaPicker
      query={pageOf([IMAGE])}
      value={IMAGE.id}
      onChange={onChange}
      locale="en"
      defaultLocale="en"
    />,
  );

  await userEvent.click(await screen.findByRole('button', { name: 'Choose none' }));

  expect(onChange).toHaveBeenCalledWith(null);
});
