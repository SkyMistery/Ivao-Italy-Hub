import { queryOptions } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { expect, test } from 'vitest';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { col, type ColumnSpec } from './columns';
import { DataList, type Page } from './DataList';
import { listSearchSchema, type ListSearch } from './search';

/**
 * The person column (note 2026-09-29-la-persona-cancellata-nel-nucleo): a list writes a person the way a page does, and says
 * "Deleted person" where the core wrote a pseudonym — which a name computed inside the query, where there is no language,
 * could not.
 */

interface Row {
  readonly id: number;
  readonly trainee: { readonly vid: number; readonly name: string | null };
  readonly trainer: { readonly vid: number; readonly name: string | null } | null;
}

const columns: readonly ColumnSpec<Row>[] = [col.person('trainee'), col.person('trainer')];

const page: Page<Row> = {
  items: [
    { id: 1, trainee: { vid: 790001, name: 'Test Trainee' }, trainer: null },
    { id: 2, trainee: { vid: -3, name: null }, trainer: { vid: 790002, name: null } },
  ],
  page: 1,
  pageSize: 25,
  total: 2,
};

test('a person column writes the name with the VID, the VID alone, nothing for nobody, and a deleted person for a pseudonym', async () => {
  renderWithProviders(
    <DataList
      columns={columns}
      query={queryOptions({ queryKey: ['test', 'people'] as const, queryFn: () => Promise.resolve(page) })}
      search={listSearchSchema.parse({})}
      onSearchChange={() => undefined}
      labels="test"
      locale="en"
      defaultLocale="en"
      timezone="UTC"
    />,
    { i18n: createTestI18n({ test: { fields: { trainee: 'Trainee', trainer: 'Trainer' } } }) },
  );

  expect(await screen.findByText('Test Trainee (790001)')).toBeInTheDocument();
  expect(screen.getByText('790002')).toBeInTheDocument();
  expect(screen.getByText('Deleted person')).toBeInTheDocument();
  expect(screen.queryByText('-3')).not.toBeInTheDocument();
});

/** A list whose search lives in the parent, as a route's does: what the box commits comes back to it as its initial term. */
function Searchable({ outside }: { readonly outside?: string }) {
  const [search, setSearch] = useState<ListSearch>(listSearchSchema.parse({ q: outside }));
  return (
    <>
      <button type="button" onClick={() => setSearch(listSearchSchema.parse({ q: 'back' }))}>
        back
      </button>
      <DataList
        columns={columns}
        query={queryOptions({
          queryKey: ['test', 'search', search.q ?? ''] as const,
          queryFn: () => Promise.resolve(page),
        })}
        search={search}
        onSearchChange={(change) => setSearch((current) => ({ ...current, ...change }))}
        labels="test"
        locale="en"
        defaultLocale="en"
        timezone="UTC"
      />
    </>
  );
}

test('the search box keeps its focus and what is typed when its own term comes back, and takes one from outside (issue #332, item 10)', async () => {
  const user = userEvent.setup();
  renderWithProviders(<Searchable />, {
    i18n: createTestI18n({ test: { fields: { trainee: 'Trainee', trainer: 'Trainer' } } }),
  });

  const box = await screen.findByRole('searchbox');
  await user.type(box, 'ab');

  // The commit, 300 ms later, comes back as the list's term: the same box, still focused, still holding what was typed.
  await waitFor(() => expect(screen.getByRole('searchbox')).toBe(box), { timeout: 1000 });
  await new Promise((resolve) => setTimeout(resolve, 400));
  expect(screen.getByRole('searchbox')).toBe(box);
  expect(box).toHaveFocus();
  expect(box).toHaveValue('ab');

  // A term from outside — a back button — replaces it.
  await user.click(screen.getByRole('button', { name: 'back' }));
  expect(screen.getByRole('searchbox')).toHaveValue('back');
});
