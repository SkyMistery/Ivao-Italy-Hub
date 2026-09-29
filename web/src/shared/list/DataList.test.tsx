import { queryOptions } from '@tanstack/react-query';
import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { col, type ColumnSpec } from './columns';
import { DataList, type Page } from './DataList';
import { listSearchSchema } from './search';

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
