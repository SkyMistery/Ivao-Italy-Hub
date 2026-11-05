import { queryOptions } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState, type FormEvent } from 'react';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { DataList } from './DataList';
import { SearchBox } from './SearchBox';
import { listSearchSchema, type ListSearch } from './search';

/**
 * The box two kinds of list share: the generated list of a screen, and the grid of `MediaPicker`, which
 * is drawn inside the form of a field. What Enter does has to be right in both.
 */

test('Enter commits what is written at once, and never submits a form around the box', async () => {
  const onCommit = vi.fn();
  const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
  render(
    <form onSubmit={onSubmit}>
      <SearchBox initial="" placeholder="Search" onCommit={onCommit} />
    </form>,
  );

  await userEvent.type(screen.getByRole('searchbox'), 'tower{Enter}');

  expect(onCommit).toHaveBeenCalledWith('tower');
  expect(onSubmit).not.toHaveBeenCalled();
});

test('Enter on what has already been searched asks nothing more', async () => {
  const onCommit = vi.fn();
  const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
  render(
    <form onSubmit={onSubmit}>
      <SearchBox initial="tower" placeholder="Search" onCommit={onCommit} />
    </form>,
  );

  await userEvent.type(screen.getByRole('searchbox'), '{Enter}');

  expect(onCommit).not.toHaveBeenCalled();
  expect(onSubmit).not.toHaveBeenCalled();
});

test('the typing still commits by itself when it stops', async () => {
  const onCommit = vi.fn();
  render(<SearchBox initial="" placeholder="Search" onCommit={onCommit} />);

  await userEvent.type(screen.getByRole('searchbox'), 'tower');

  expect(onCommit).not.toHaveBeenCalled();
  await vi.waitFor(() => expect(onCommit).toHaveBeenCalledWith('tower'));
});

function ListWithItsOwnSearch({ onSearch }: { onSearch: (search: Partial<ListSearch>) => void }) {
  const [search, setSearch] = useState(listSearchSchema.parse({}));

  return (
    <DataList
      columns={[]}
      query={queryOptions({
        queryKey: ['search-box', search] as const,
        queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 25, total: 0 }),
      })}
      search={search}
      onSearchChange={(patch) => {
        onSearch(patch);
        setSearch((now) => ({ ...now, ...patch }));
      }}
      labels="test"
      locale="en"
      defaultLocale="en"
      timezone="UTC"
    />
  );
}

test('in a generated list, Enter asks the list at once and from its first page', async () => {
  const onSearch = vi.fn();
  renderWithProviders(<ListWithItsOwnSearch onSearch={onSearch} />);

  await userEvent.type(await screen.findByRole('searchbox'), 'tower{Enter}');

  expect(onSearch).toHaveBeenCalledWith({ q: 'tower', page: 1 });
});

test('in a generated list, the box keeps the focus when the search goes, and the next keys are added', async () => {
  const onSearch = vi.fn();
  renderWithProviders(<ListWithItsOwnSearch onSearch={onSearch} />);

  const box = await screen.findByRole('searchbox');
  await userEvent.type(box, 'tow');
  await waitFor(() => expect(onSearch).toHaveBeenCalledWith({ q: 'tow', page: 1 }));

  // The same box and not only a box with the focus: the keyboard below goes wherever the focus is.
  expect(screen.getByRole('searchbox')).toBe(box);
  expect(box).toHaveFocus();

  await userEvent.keyboard('er');
  expect(box).toHaveValue('tower');
  await waitFor(() => expect(onSearch).toHaveBeenCalledWith({ q: 'tower', page: 1 }));
  expect(box).toHaveFocus();
});

test('in a generated list, the box keeps the focus when Enter sends the search', async () => {
  const onSearch = vi.fn();
  renderWithProviders(<ListWithItsOwnSearch onSearch={onSearch} />);

  const box = await screen.findByRole('searchbox');
  await userEvent.type(box, 'tower{Enter}');

  expect(onSearch).toHaveBeenCalledWith({ q: 'tower', page: 1 });
  expect(screen.getByRole('searchbox')).toBe(box);
  expect(box).toHaveFocus();
});

test('the term that arrives while somebody goes on writing does not take back what they wrote', async () => {
  const onCommit = vi.fn();
  const box = (initial: string) => <SearchBox initial={initial} placeholder="Search" onCommit={onCommit} />;
  const { rerender } = render(box(''));

  const search = screen.getByRole('searchbox');
  await userEvent.type(search, 'tow');
  await waitFor(() => expect(onCommit).toHaveBeenCalledWith('tow'));

  // The answer to `tow` is on its way, and the next letter gets there first.
  await userEvent.keyboard('e');
  rerender(box('tow'));

  expect(search).toHaveValue('towe');
  expect(search).toHaveFocus();
  // And it is not left behind: it has its own search, then the box is at rest again.
  await waitFor(() => expect(onCommit).toHaveBeenCalledWith('towe'));
  rerender(box('towe'));
  expect(search).toHaveValue('towe');
});

test('the back button brings the term of the address into the box, and forward brings it back', () => {
  const onCommit = vi.fn();
  const box = (initial: string) => <SearchBox initial={initial} placeholder="Search" onCommit={onCommit} />;
  const { rerender } = render(box('tower'));

  rerender(box('tow'));
  expect(screen.getByRole('searchbox')).toHaveValue('tow');

  rerender(box(''));
  expect(screen.getByRole('searchbox')).toHaveValue('');

  rerender(box('tow'));
  expect(screen.getByRole('searchbox')).toHaveValue('tow');

  // Following the address is not a search of its own: that would add one more step to go back over.
  expect(onCommit).not.toHaveBeenCalled();
});
