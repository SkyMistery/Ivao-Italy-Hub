import { render, screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, test } from 'vitest';

import { ListFilter } from './ListFilter';

/**
 * A filter put back to "all" draws "all". Radix, without a `value`, goes uncontrolled and draws the last choice
 * it held: the list was the whole list, and the filter above it still named one row's worth of it.
 */

const items = [
  { value: 'alpha', label: 'Alpha' },
  { value: 'bravo', label: 'Bravo' },
];

/** The screen around the filter: it holds the value, as the address of a list does, and can clear it itself. */
function Host({ initial }: { initial?: string }) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <ListFilter id="kind" label="Kind" none="All kinds" value={value} onChange={setValue} items={items} />
      <output aria-label="held">{value ?? 'NOTHING'}</output>
      <button type="button" onClick={() => setValue(undefined)}>
        Clear the filters
      </button>
    </>
  );
}

const filter = () => screen.getByRole('combobox', { name: 'Kind' });
const held = () => screen.getByRole('status', { name: 'held' });

async function choose(user: UserEvent, name: string) {
  await user.click(filter());
  await user.click(screen.getByRole('option', { name }));
}

describe('ListFilter', () => {
  test('a new filter shows what "no filter" is called', () => {
    render(<Host />);

    expect(filter()).toHaveTextContent('All kinds');
    expect(held()).toHaveTextContent('NOTHING');
  });

  test('a filter born with a value draws it', () => {
    render(<Host initial="bravo" />);

    expect(filter()).toHaveTextContent('Bravo');
  });

  test('a choice is drawn and held', async () => {
    const user = userEvent.setup();
    render(<Host />);

    await choose(user, 'Alpha');

    expect(filter()).toHaveTextContent('Alpha');
    expect(held()).toHaveTextContent('alpha');
  });

  test('going back to "all" draws "all", as a new filter does', async () => {
    const user = userEvent.setup();
    render(<Host />);

    await choose(user, 'Alpha');
    await choose(user, 'All kinds');

    expect(held()).toHaveTextContent('NOTHING');
    expect(filter()).toHaveTextContent('All kinds');
    expect(filter()).not.toHaveTextContent('Alpha');
  });

  test('a filter born with a value also goes back to "all"', async () => {
    const user = userEvent.setup();
    render(<Host initial="bravo" />);

    await choose(user, 'All kinds');

    expect(held()).toHaveTextContent('NOTHING');
    expect(filter()).toHaveTextContent('All kinds');
  });

  test('a filter cleared from outside draws "all" too', async () => {
    const user = userEvent.setup();
    render(<Host />);

    await choose(user, 'Alpha');
    await user.click(screen.getByRole('button', { name: 'Clear the filters' }));

    expect(filter()).toHaveTextContent('All kinds');
  });

  test('after going back to "all" the same choice can be made again', async () => {
    const user = userEvent.setup();
    render(<Host />);

    await choose(user, 'Alpha');
    await choose(user, 'All kinds');
    await choose(user, 'Alpha');

    expect(held()).toHaveTextContent('alpha');
    expect(filter()).toHaveTextContent('Alpha');
  });
});
