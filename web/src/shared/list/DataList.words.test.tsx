import { queryOptions } from '@tanstack/react-query';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import italianCommon from '../../../../locales/it/common.json';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { col, type ColumnSpec } from './columns';
import { DataList, type Page } from './DataList';
import { listSearchSchema, type ListSearch } from './search';

/**
 * The words of a generic list (#224). Its pages said "Previous" and "Next" under every interface, Italian
 * included, because they were Atmosphere's `Pagination`, which takes no words; and an empty list said that
 * everything "this department" creates would show up there, under the rows of a module too.
 */

interface Row {
  readonly id: number;
  readonly name: string;
}

const columns: readonly ColumnSpec<Row>[] = [col.text('name')];

let drawn = 0;

async function draw({
  total,
  search = {},
  language = 'en',
  emptyDescription,
  onSearchChange = () => undefined,
}: {
  total: number;
  search?: Partial<ListSearch>;
  language?: 'en' | 'it';
  emptyDescription?: string;
  onSearchChange?: (patch: Partial<ListSearch>) => void;
}) {
  const i18n = createTestI18n({ test: { fields: { name: 'Name' } } });
  await i18n.changeLanguage(language);

  const page: Page<Row> = {
    items: total === 0 ? [] : [{ id: 1, name: 'A row' }],
    page: search.page ?? 1,
    pageSize: 25,
    total,
  };

  // A key of its own for every list drawn, so that no answer of the cache stands in for another one.
  drawn += 1;

  renderWithProviders(
    <DataList
      columns={columns}
      query={queryOptions({ queryKey: ['test', 'words', drawn] as const, queryFn: () => Promise.resolve(page) })}
      search={listSearchSchema.parse(search)}
      onSearchChange={onSearchChange}
      labels="test"
      locale={language}
      defaultLocale="en"
      timezone="UTC"
      {...(emptyDescription === undefined ? {} : { emptyDescription })}
    />,
    { i18n },
  );
}

test('the pages of a list are named in the words of the language files, and none is Atmosphere’s', async () => {
  await draw({ total: 75 });

  const pages = await screen.findByRole('navigation', { name: englishCommon.list.pages.label });

  expect(within(pages).getByRole('button', { name: englishCommon.list.pages.previous })).toBeDisabled();
  expect(within(pages).getByRole('button', { name: '1' })).toHaveAttribute('aria-current', 'page');
  expect(within(pages).getByRole('button', { name: '2' })).toBeEnabled();
  expect(within(pages).getByRole('button', { name: '3' })).toBeEnabled();
  expect(within(pages).getByRole('button', { name: englishCommon.list.pages.next })).toBeEnabled();

  // What Atmosphere's own writes into the page, none of which a language file can reach.
  expect(screen.queryByRole('navigation', { name: 'pagination' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Go to next page' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Go to previous page' })).not.toBeInTheDocument();
  expect(screen.queryByText('More pages')).not.toBeInTheDocument();

  // Three pages fit on the line, so no «…» leads to one of them: Atmosphere drew one here, beside the 3 it led to.
  expect(within(pages).queryByRole('button', { name: englishCommon.list.pages.last })).not.toBeInTheDocument();
  expect(within(pages).queryByRole('button', { name: englishCommon.list.pages.first })).not.toBeInTheDocument();
});

test('in Italian the pages say Italian words, and no English one is left', async () => {
  await draw({ total: 75, language: 'it' });

  const pages = await screen.findByRole('navigation', { name: italianCommon.list.pages.label });

  expect(within(pages).getByRole('button', { name: italianCommon.list.pages.previous })).toBeDisabled();
  expect(within(pages).getByRole('button', { name: italianCommon.list.pages.next })).toBeEnabled();

  expect(screen.queryByRole('button', { name: /previous|next|go to/i })).not.toBeInTheDocument();
  expect(screen.queryByText(/previous|next|more pages/i)).not.toBeInTheDocument();
});

test('a page out of the window is a «…» away, and every button asks for its own page', async () => {
  const onSearchChange = vi.fn();
  await draw({ total: 250, search: { page: 5 }, onSearchChange });

  const pages = await screen.findByRole('navigation', { name: englishCommon.list.pages.label });

  // Ten pages, the fifth on screen: the three around it, and the two ends a «…» away.
  expect(
    within(pages)
      .getAllByRole('button')
      .map((button) => button.getAttribute('aria-label') ?? button.textContent),
  ).toEqual([
    englishCommon.list.pages.previous,
    englishCommon.list.pages.first,
    '4',
    '5',
    '6',
    englishCommon.list.pages.last,
    englishCommon.list.pages.next,
  ]);

  const user = userEvent.setup();

  await user.click(within(pages).getByRole('button', { name: englishCommon.list.pages.next }));
  expect(onSearchChange).toHaveBeenLastCalledWith({ page: 6 });

  await user.click(within(pages).getByRole('button', { name: englishCommon.list.pages.previous }));
  expect(onSearchChange).toHaveBeenLastCalledWith({ page: 4 });

  await user.click(within(pages).getByRole('button', { name: englishCommon.list.pages.last }));
  expect(onSearchChange).toHaveBeenLastCalledWith({ page: 10 });

  await user.click(within(pages).getByRole('button', { name: englishCommon.list.pages.first }));
  expect(onSearchChange).toHaveBeenLastCalledWith({ page: 1 });

  await user.click(within(pages).getByRole('button', { name: '6' }));
  expect(onSearchChange).toHaveBeenLastCalledWith({ page: 6 });
});

test('on the last page «Next» is off, and the first page is a «…» away', async () => {
  await draw({ total: 250, search: { page: 10 } });

  const pages = await screen.findByRole('navigation', { name: englishCommon.list.pages.label });

  expect(within(pages).getByRole('button', { name: englishCommon.list.pages.next })).toBeDisabled();
  expect(within(pages).getByRole('button', { name: '10' })).toHaveAttribute('aria-current', 'page');
  expect(within(pages).getByRole('button', { name: '8' })).toBeInTheDocument();
  expect(within(pages).getByRole('button', { name: englishCommon.list.pages.first })).toBeInTheDocument();
  expect(within(pages).queryByRole('button', { name: englishCommon.list.pages.last })).not.toBeInTheDocument();
});

test('an empty list says the core’s sentence, unless its screen gives one of its own', async () => {
  await draw({ total: 0 });

  expect(await screen.findByText(englishCommon.list.empty.description)).toBeInTheDocument();
  // And nothing to page through.
  expect(screen.queryByRole('navigation', { name: englishCommon.list.pages.label })).not.toBeInTheDocument();
});

test('the sentence a screen gives an empty list is said in place of the core’s', async () => {
  await draw({ total: 0, emptyDescription: 'No slot yet: paste a sheet, or create one.' });

  expect(await screen.findByText('No slot yet: paste a sheet, or create one.')).toBeInTheDocument();
  expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();
});

test('a search that finds nothing says so, and neither sentence about an empty list', async () => {
  await draw({ total: 0, search: { q: 'nothing like this' }, emptyDescription: 'No slot yet.' });

  expect(await screen.findByText(englishCommon.list.empty.noMatch)).toBeInTheDocument();
  expect(screen.queryByText('No slot yet.')).not.toBeInTheDocument();
  expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();
});
