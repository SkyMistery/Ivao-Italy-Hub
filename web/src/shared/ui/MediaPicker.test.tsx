import { focusManager, queryOptions } from '@tanstack/react-query';
import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import userEvent from '@testing-library/user-event';
import type { FormEvent } from 'react';
import { expect, test, vi } from 'vitest';

import { ApiError } from '../api/problem';
import { renderWithProviders } from '../../test/harness';

import { MediaPicker, type MediaLibraryQuery, type PickableMedia } from './MediaPicker';
import { NOTHING_ASKED, type MediaBrowse } from './narrowLibrary';

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

/**
 * A library that looks for a file, in memory: it answers what the query of the real one would, and
 * remembers what it was asked, because what a picker asks is what reaches the server.
 */
function libraryOf(all: PickableMedia[], { pageSize = 2, categories = ['Logo', 'Image'] } = {}) {
  const asked: MediaBrowse[] = [];

  const read = (browse: MediaBrowse): MediaLibraryQuery => {
    const queryKey: readonly unknown[] = ['media-picker-library', browse];

    return queryOptions({
      queryKey,
      queryFn: () => {
        asked.push(browse);
        const found = all.filter((media) => media.fileName.includes(browse.q));
        const start = (browse.page - 1) * pageSize;

        return Promise.resolve({ items: found.slice(start, start + pageSize), total: found.length });
      },
      meta: { browse: read, categories, fileHref: (id: number) => `/staff/media/${id}` },
    });
  };

  return { query: read(NOTHING_ASKED), asked };
}

const file = (id: number, fileName: string): PickableMedia => ({ ...IMAGE, id, fileName });

function renderLibrary(
  query: MediaLibraryQuery,
  {
    onChange = vi.fn(),
    onUpload,
  }: { onChange?: (id: number | null) => void; onUpload?: (file: File) => Promise<number> } = {},
) {
  renderWithProviders(
    <MediaPicker
      query={query}
      value={null}
      onChange={onChange}
      onUpload={onUpload}
      locale="en"
      defaultLocale="en"
    />,
  );
}

test('Enter in the search asks at once and never submits the form the picker is drawn in', async () => {
  // Every picker of a form is inside its `<form>`: on a new tour, an implicit submit creates the row
  // and leaves the page, in the hands of somebody who was only looking for a file.
  const { query, asked } = libraryOf([file(1, 'runway.png'), file(2, 'tower.png')]);
  const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
  renderWithProviders(
    <form onSubmit={onSubmit}>
      <MediaPicker query={query} value={null} onChange={vi.fn()} locale="en" defaultLocale="en" />
    </form>,
  );

  await screen.findByRole('button', { name: /runway\.png/ });
  await userEvent.type(screen.getByRole('searchbox'), 'tower{Enter}');

  expect(onSubmit).not.toHaveBeenCalled();
  await waitFor(() => expect(asked).toContainEqual({ q: 'tower', category: null, page: 1 }));
});

test('after an upload the category says what the grid shows, and can be chosen again', async () => {
  const { query, asked } = libraryOf([file(1, 'runway.png')]);
  renderLibrary(query, { onUpload: () => Promise.resolve(1) });

  const select = await screen.findByRole('combobox', { name: 'Category' });
  await userEvent.click(select);
  await userEvent.click(await screen.findByRole('option', { name: 'Logo' }));
  await waitFor(() => expect(select).toHaveTextContent('Logo'));

  await userEvent.upload(
    screen.getByLabelText('Upload a file'),
    new File(['x'], 'new.png', { type: 'image/png' }),
  );

  // The grid is back to every category: a select still saying "Logo" would be saying something false.
  await waitFor(() => expect(asked.at(-1)).toEqual(NOTHING_ASKED));
  await waitFor(() => expect(select).toHaveTextContent('All categories'));

  await userEvent.click(select);
  await userEvent.click(await screen.findByRole('option', { name: 'Logo' }));
  await waitFor(() => expect(asked.at(-1)).toEqual({ q: '', category: 'Logo', page: 1 }));
});

test('the search asks the library, and a search that finds nothing leaves the box to change it', async () => {
  const { query, asked } = libraryOf([file(1, 'runway.png'), file(2, 'tower.png')]);
  renderLibrary(query);

  await screen.findByRole('button', { name: /runway\.png/ });
  const search = screen.getByRole('searchbox', { name: 'Search by name or alternative text' });

  await userEvent.type(search, 'tower');
  await waitFor(() => expect(screen.queryByRole('button', { name: /runway\.png/ })).toBeNull());
  expect(screen.getByRole('button', { name: /tower\.png/ })).toBeInTheDocument();
  expect(asked.at(-1)).toEqual({ q: 'tower', category: null, page: 1 });

  await userEvent.type(search, 'x');
  expect(await screen.findByText('No file matches.')).toBeInTheDocument();
  // Not the empty library: nothing is wrong with it, and the box has to stay or there is no way back.
  expect(screen.queryByText('No files yet')).toBeNull();
  expect(search).toHaveFocus();
});

test('the category is a choice among the closed ones, and a field that asks for one offers no choice', async () => {
  const offered = libraryOf([file(1, 'runway.png')]);
  renderLibrary(offered.query);

  const select = await screen.findByRole('combobox', { name: 'Category' });
  await userEvent.click(select);
  await userEvent.click(await screen.findByRole('option', { name: 'Logo' }));
  await waitFor(() => expect(offered.asked.at(-1)).toEqual({ q: '', category: 'Logo', page: 1 }));

  cleanup();

  // What `mediaPickerQuery` answers once a field has narrowed it to the logos.
  renderLibrary(libraryOf([file(1, 'runway.png')], { categories: [] }).query);
  await screen.findByRole('button', { name: /runway\.png/ });
  expect(screen.queryByRole('combobox', { name: 'Category' })).toBeNull();
});

test('"show more" adds the next page under the first, and goes away with the last', async () => {
  const { query, asked } = libraryOf([file(1, 'one.png'), file(2, 'two.png'), file(3, 'three.png')]);
  renderLibrary(query);

  await screen.findByRole('button', { name: /one\.png/ });
  expect(screen.queryByRole('button', { name: /three\.png/ })).toBeNull();

  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));

  await screen.findByRole('button', { name: /three\.png/ });
  expect(screen.getByRole('button', { name: /one\.png/ })).toBeInTheDocument();
  expect(asked.at(-1)).toEqual({ q: '', category: null, page: 2 });
  expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull();
});

test('a file that moved to the next page is not drawn twice, and a last page with nothing new ends it', async () => {
  // What happens when somebody uploads between two pages: the total grew, and the last row of the
  // first page is the first of the second.
  const pages = [
    { items: [file(1, 'one.png'), file(2, 'two.png')], total: 4 },
    { items: [file(2, 'two.png'), file(3, 'three.png')], total: 4 },
    { items: [], total: 4 },
  ];
  const read = (browse: MediaBrowse): MediaLibraryQuery => {
    const queryKey: readonly unknown[] = ['media-picker-moved', browse.page];
    return queryOptions({
      queryKey,
      queryFn: () => Promise.resolve(pages[browse.page - 1] ?? { items: [], total: 4 }),
      meta: { browse: read },
    });
  };
  renderLibrary(read(NOTHING_ASKED));

  await screen.findByRole('button', { name: /two\.png/ });
  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
  await screen.findByRole('button', { name: /three\.png/ });
  expect(screen.getAllByRole('button', { name: /two\.png/ })).toHaveLength(1);

  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull());
});

test('the upload stands above the grid, and the new file is chosen in sight of the search that hid it', async () => {
  const { query, asked } = libraryOf([file(1, 'runway.png'), file(2, 'tower.png')]);
  const onChange = vi.fn();
  const onUpload = vi.fn(() => Promise.resolve(2));
  renderLibrary(query, { onChange, onUpload });

  const upload = await screen.findByRole('button', { name: 'Upload a file' });
  const grid = await screen.findByRole('list');
  // Under two dozen pictures nobody saw it (design of the phase 3, §4.4).
  expect(upload.compareDocumentPosition(grid) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

  const search = screen.getByRole('searchbox');
  await userEvent.type(search, 'runway');
  await waitFor(() => expect(asked.at(-1)?.q).toBe('runway'));

  await userEvent.upload(
    screen.getByLabelText('Upload a file'),
    new File(['x'], 'new.png', { type: 'image/png' }),
  );

  await waitFor(() => expect(onChange).toHaveBeenCalledWith(2));
  // The box was emptied too: left with the word in it, it would ask the same search again.
  expect(screen.getByRole('searchbox')).toHaveValue('');
  await waitFor(() => expect(asked.at(-1)).toEqual(NOTHING_ASKED));
  await act(() => new Promise((resolve) => setTimeout(resolve, 350)));
  expect(asked.at(-1)).toEqual(NOTHING_ASKED);
});

test('a file dragged onto the grid is uploaded and chosen', async () => {
  const onChange = vi.fn();
  const onUpload = vi.fn(() => Promise.resolve(1));
  renderLibrary(libraryOf([file(1, 'runway.png')]).query, { onChange, onUpload });

  const grid = await screen.findByRole('list');
  const dragged = new File(['x'], 'dragged.png', { type: 'image/png' });
  const dataTransfer = { types: ['Files'], files: [dragged], dropEffect: 'none' };

  // The browser only lets go of a file where `dragover` was told to keep its hands off.
  expect(fireEvent.dragOver(grid, { dataTransfer })).toBe(false);
  expect(fireEvent.drop(grid, { dataTransfer })).toBe(false);

  expect(onUpload).toHaveBeenCalledWith(dragged);
  await waitFor(() => expect(onChange).toHaveBeenCalledWith(1));
});

test('what the editor itself drags is not a file, and the picker lets it pass', async () => {
  const onUpload = vi.fn(() => Promise.resolve(1));
  renderLibrary(libraryOf([file(1, 'runway.png')]).query, { onUpload });

  const grid = await screen.findByRole('list');
  const dataTransfer = { types: ['text/plain'], files: [] };

  expect(fireEvent.dragOver(grid, { dataTransfer })).toBe(true);
  expect(fireEvent.drop(grid, { dataTransfer })).toBe(true);
  expect(onUpload).not.toHaveBeenCalled();
});

test('a picker that may not upload takes no file either', async () => {
  renderLibrary(libraryOf([file(1, 'runway.png')]).query);

  const grid = await screen.findByRole('list');
  expect(fireEvent.dragOver(grid, { dataTransfer: { types: ['Files'], files: [] } })).toBe(true);
  expect(screen.queryByRole('button', { name: 'Upload a file' })).toBeNull();
});

test('the file just uploaded says its alternative text is to be written, and no other tile does', async () => {
  // Another file with none: maybe of another department, and nobody here can promise this person
  // may write it. The way there is for whoever has just put the file in the library.
  const other = { ...file(1, 'other.png'), alt: {} };
  const justUploaded = { ...file(2, 'new.png'), alt: {} };
  const { query } = libraryOf([justUploaded, other]);
  // A real router, in memory: a `Link` outside one throws.
  const router = createRouter({
    routeTree: createRootRoute({
      component: () => (
        <MediaPicker
          query={query}
          value={null}
          onChange={vi.fn()}
          onUpload={() => Promise.resolve(justUploaded.id)}
          locale="en"
          defaultLocale="en"
        />
      ),
    }),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  renderWithProviders(<RouterProvider router={router} />);

  await screen.findByRole('button', { name: /new\.png/ });
  expect(screen.queryByRole('link', { name: 'Alternative text to write' })).toBeNull();

  await userEvent.upload(
    screen.getByLabelText('Upload a file'),
    new File(['x'], 'new.png', { type: 'image/png' }),
  );

  const link = await screen.findByRole('link', { name: 'Alternative text to write' });
  expect(link).toHaveAttribute('href', '/staff/media/2');
  // A new tab: whoever reads this is in the middle of a page they have not saved.
  expect(link).toHaveAttribute('target', '_blank');
  expect(screen.getAllByRole('link', { name: 'Alternative text to write' })).toHaveLength(1);
});

/** A library whose reads fail while `failing` says so. */
function libraryThatFails(all: PickableMedia[], failing: (browse: MediaBrowse) => boolean, pageSize = 2) {
  const read = (browse: MediaBrowse): MediaLibraryQuery => {
    const queryKey: readonly unknown[] = ['media-picker-failing', browse];

    return queryOptions({
      queryKey,
      queryFn: () => {
        if (failing(browse)) {
          return Promise.reject(new Error('The read failed'));
        }

        const found = all.filter((media) => media.fileName.includes(browse.q));
        const start = (browse.page - 1) * pageSize;

        return Promise.resolve({ items: found.slice(start, start + pageSize), total: found.length });
      },
      meta: { browse: read },
    });
  };

  return read(NOTHING_ASKED);
}

test('a library whose first read failed is not an empty library', async () => {
  let down = true;
  renderLibrary(libraryThatFails([file(1, 'runway.png')], () => down));

  expect(await screen.findByRole('alert')).toHaveTextContent('The files could not be read.');
  expect(screen.queryByText('No files yet')).toBeNull();

  down = false;
  await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

  await screen.findByRole('button', { name: /runway\.png/ });
  expect(screen.queryByRole('alert')).toBeNull();
});

test('a search whose read failed says so, and does not say that no file matches', async () => {
  renderLibrary(libraryThatFails([file(1, 'runway.png')], (browse) => browse.q !== ''));

  await screen.findByRole('button', { name: /runway\.png/ });
  await userEvent.type(screen.getByRole('searchbox'), 'tower{Enter}');

  expect(await screen.findByRole('alert')).toHaveTextContent('The files could not be read.');
  expect(screen.queryByText('No file matches.')).toBeNull();
  // The box stays, or there is no way back.
  expect(screen.getByRole('searchbox')).toHaveValue('tower');
});

test('a next page that failed keeps what was read, says so, and can be tried again', async () => {
  let down = true;
  renderLibrary(
    libraryThatFails(
      [file(1, 'one.png'), file(2, 'two.png'), file(3, 'three.png')],
      (browse) => down && browse.page === 2,
    ),
  );

  await screen.findByRole('button', { name: /one\.png/ });
  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));

  expect(await screen.findByRole('alert')).toHaveTextContent('The files could not be read.');
  expect(screen.getByRole('button', { name: /one\.png/ })).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /two\.png/ })).toBeInTheDocument();

  down = false;
  await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

  await screen.findByRole('button', { name: /three\.png/ });
  expect(screen.getByRole('button', { name: /one\.png/ })).toBeInTheDocument();
  expect(screen.queryByRole('alert')).toBeNull();
});

test('a page already read follows the library, it is not a copy of what it said', async () => {
  // Written in another tab, say: the next read of the first page has to reach a tile that "show more"
  // has moved up.
  const all = [file(1, 'one.png'), file(2, 'two.png'), file(3, 'three.png')];
  const { query } = libraryOf(all);
  renderWithProviders(
    <MediaPicker query={query} value={null} onChange={vi.fn()} locale="en" defaultLocale="en" />,
  );

  await screen.findByRole('button', { name: /one\.png/ });
  await userEvent.click(screen.getByRole('button', { name: 'Show more' }));
  await screen.findByRole('button', { name: /three\.png/ });

  all[0] = { ...file(1, 'renamed.png') };
  await act(async () => {
    focusManager.setFocused(false);
    focusManager.setFocused(true);
    await Promise.resolve();
  });
  focusManager.setFocused(undefined);

  await screen.findByRole('button', { name: /renamed\.png/ });
  expect(screen.queryByRole('button', { name: /one\.png/ })).toBeNull();
});

test('an upload refused says what the server said, not a guess', async () => {
  const onUpload = (): Promise<number> =>
    Promise.reject(new ApiError(400, { errors: { file: ['errors.media.tooLarge'] } }));
  const refuser = libraryOf([file(1, 'runway.png')]);
  renderLibrary(refuser.query, { onUpload });

  await screen.findByRole('button', { name: /runway\.png/ });
  await userEvent.upload(
    screen.getByLabelText('Upload a file'),
    new File(['x'], 'huge.png', { type: 'image/png' }),
  );

  const alert = await screen.findByRole('alert');
  expect(alert.textContent).not.toBe('');
  expect(alert).not.toHaveTextContent('errors.media.tooLarge');

  cleanup();

  // Somebody the server does not let upload is not told the file is too large.
  renderLibrary(libraryOf([file(1, 'runway.png')]).query, {
    onUpload: () => Promise.reject(new ApiError(403, undefined)),
  });
  await screen.findByRole('button', { name: /runway\.png/ });
  await userEvent.upload(
    screen.getByLabelText('Upload a file'),
    new File(['x'], 'small.png', { type: 'image/png' }),
  );

  const forbidden = await screen.findByRole('alert');
  expect(forbidden.textContent).not.toBe(alert.textContent);
});

test('of several files let go on the grid the first is uploaded, and whoever dropped them is told', async () => {
  const onUpload = vi.fn(() => Promise.resolve(1));
  renderLibrary(libraryOf([file(1, 'runway.png')]).query, { onUpload });

  const grid = await screen.findByRole('list');
  const first = new File(['1'], 'first.png', { type: 'image/png' });
  const second = new File(['2'], 'second.png', { type: 'image/png' });

  fireEvent.drop(grid, { dataTransfer: { types: ['Files'], files: [first, second] } });

  expect(onUpload).toHaveBeenCalledTimes(1);
  expect(onUpload).toHaveBeenCalledWith(first);
  expect(await screen.findByText('You let go of several files: only the first is uploaded.')).toBeVisible();

  // The next one, on its own, has nothing to be told.
  fireEvent.drop(grid, { dataTransfer: { types: ['Files'], files: [second] } });
  await waitFor(() => expect(onUpload).toHaveBeenCalledTimes(2));
  await waitFor(() =>
    expect(screen.queryByText('You let go of several files: only the first is uploaded.')).toBeNull(),
  );
});

test('a library that cannot look for a file offers no next page it could not read', async () => {
  // `total` larger than the rows it answers, and no `browse` to ask for the rest.
  renderWithProviders(
    <MediaPicker
      query={queryOptions({
        queryKey: ['media-picker-fixed'] as const,
        queryFn: () => Promise.resolve({ items: [IMAGE], total: 40 }),
      })}
      value={null}
      onChange={vi.fn()}
      locale="en"
      defaultLocale="en"
    />,
  );

  await screen.findByRole('button', { name: /banner\.png/ });
  expect(screen.queryByRole('button', { name: 'Show more' })).toBeNull();
});
