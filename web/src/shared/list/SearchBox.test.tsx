import { queryOptions } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
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
