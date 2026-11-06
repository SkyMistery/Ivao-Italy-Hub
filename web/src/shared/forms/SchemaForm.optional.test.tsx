import { queryOptions } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import type { MediaLibraryQuery } from '../ui';

import { SchemaForm } from './SchemaForm';

/**
 * An optional field put back to "nothing" draws nothing. A controller reads `undefined` as the value it was
 * born with, so the choice stayed on the screen while the form held nothing and saved nothing: the date had it
 * (design of the editor, phase 3, §4.3), and so had these three.
 */

const library: MediaLibraryQuery = queryOptions({
  queryKey: ['optional-test', 'library'] as readonly unknown[],
  queryFn: () =>
    Promise.resolve({
      items: [
        { id: 42, fileName: 'hangar.png', contentType: 'image/png', alt: {}, url: '/media/42/hangar.png' },
      ],
      total: 1,
    }),
});

const labels = {
  test: {
    fields: { field: 'The field', slots: 'Slots', 'slots.field': 'The field' },
    options: {
      field: { none: 'Nothing here', ED: 'Events', FOD: 'Flights' },
      'slots.field': { none: 'Nothing here', ED: 'Events', FOD: 'Flights' },
    },
  },
};

interface Case {
  kind: string;
  field: z.ZodType;
  chosen: unknown;
  /** What `choose` puts back. */
  again: unknown;
  takeOff: (user: UserEvent) => Promise<void>;
  choose: (user: UserEvent) => Promise<void>;
  expectChosen: () => Promise<void>;
  expectNothing: () => Promise<void>;
}

const combobox = () => screen.getByRole('combobox');
const picture = () => screen.findByRole('button', { name: /hangar\.png/ });

const enumCase: Case = {
  kind: 'an enum',
  field: z.enum(['ED', 'FOD']).optional(),
  chosen: 'ED',
  again: 'FOD',
  takeOff: async (user) => {
    await user.click(combobox());
    await user.click(screen.getByRole('option', { name: 'Nothing here' }));
  },
  choose: async (user) => {
    await user.click(combobox());
    await user.click(screen.getByRole('option', { name: 'Flights' }));
  },
  expectChosen: () => {
    expect(combobox()).toHaveTextContent('Flights');
    return Promise.resolve();
  },
  expectNothing: () => {
    expect(combobox()).not.toHaveTextContent('Events');
    return Promise.resolve();
  },
};

const iconCase: Case = {
  kind: 'an icon',
  field: z.string().optional().meta({ icon: true }),
  chosen: 'plane',
  again: 'plane',
  takeOff: (user) => user.click(screen.getByRole('radio', { name: 'Nothing here' })),
  choose: (user) => user.click(screen.getByRole('radio', { name: 'plane' })),
  expectChosen: () => {
    expect(screen.getByRole('radio', { name: 'plane' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('radio', { name: 'Nothing here' })).toHaveAttribute('aria-checked', 'false');
    return Promise.resolve();
  },
  expectNothing: () => {
    expect(screen.getByRole('radio', { name: 'plane' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('radio', { name: 'Nothing here' })).toHaveAttribute('aria-checked', 'true');
    return Promise.resolve();
  },
};

const mediaCase: Case = {
  kind: 'a media, pressed again',
  field: z.number().optional().meta({ media: true }),
  chosen: 42,
  again: 42,
  takeOff: async (user) => user.click(await picture()),
  choose: async (user) => user.click(await picture()),
  expectChosen: async () => {
    expect(await picture()).toHaveAttribute('aria-pressed', 'true');
  },
  expectNothing: async () => {
    expect(await picture()).toHaveAttribute('aria-pressed', 'false');
    expect(screen.queryByRole('button', { name: 'Choose none' })).toBeNull();
  },
};

const mediaByItsButton: Case = {
  ...mediaCase,
  kind: 'a media, by its button',
  takeOff: async (user) => {
    await picture();
    await user.click(screen.getByRole('button', { name: 'Choose none' }));
  },
};

const cases = [enumCase, iconCase, mediaCase, mediaByItsButton];

function MovingRow({
  schema,
  row,
  onSubmit,
}: {
  schema: z.ZodType<Record<string, unknown>, Record<string, unknown>>;
  row: Record<string, unknown>;
  onSubmit: (values: Record<string, unknown>) => Promise<unknown>;
}) {
  const [rowVersion, setRowVersion] = useState('one');

  return (
    <>
      <button type="button" onClick={() => setRowVersion('two')}>
        Another action
      </button>
      <SchemaForm
        schema={schema}
        defaults={{ ...row, rowVersion }}
        locales={['en']}
        labels="test"
        submitLabel="Save"
        onSubmit={onSubmit}
        mediaLibrary={library}
        division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
      />
    </>
  );
}

function renderRow(schema: z.ZodType, row: Record<string, unknown>) {
  const onSubmit = vi.fn<(values: Record<string, unknown>) => Promise<unknown>>(() => Promise.resolve());
  renderWithProviders(
    <MovingRow
      schema={schema as z.ZodType<Record<string, unknown>, Record<string, unknown>>}
      row={row}
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );

  return { onSubmit, user: userEvent.setup() };
}

/** Saves, and answers what the form sent. */
async function saved(user: UserEvent, onSubmit: ReturnType<typeof renderRow>['onSubmit']) {
  await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());

  return onSubmit.mock.calls[0]?.[0];
}

async function moveTheRow(user: UserEvent) {
  await user.click(screen.getByRole('button', { name: 'Another action' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled());
}

describe.each(cases)('$kind', ({ field, chosen, again, takeOff, choose, expectChosen, expectNothing }) => {
  const single = z.object({ rowVersion: z.string(), field });

  test('put back to nothing, it draws nothing and saves nothing', async () => {
    const { onSubmit, user } = renderRow(single, { field: chosen });

    await takeOff(user);

    await expectNothing();
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'one', field: undefined });
  });

  test('it stays off when the row moves under the form', async () => {
    const { onSubmit, user } = renderRow(single, { field: chosen });

    await takeOff(user);
    await moveTheRow(user);

    await expectNothing();
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'two', field: undefined });
  });

  test('it can be chosen again', async () => {
    const { onSubmit, user } = renderRow(single, { field: chosen });

    await takeOff(user);
    await choose(user);

    await expectChosen();
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'one', field: again });
  });

  test('inside an entry of a list it behaves the same', async () => {
    const { onSubmit, user } = renderRow(
      z.object({ rowVersion: z.string(), slots: z.array(z.object({ field })) }),
      { slots: [{ field: chosen }] },
    );

    await takeOff(user);
    await expectNothing();
    await moveTheRow(user);

    await expectNothing();
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'two', slots: [{ field: undefined }] });
  });
});

// ---- a select keeps a choice of its own --------------------------------------------------------
//
// A select handed no value holds the last one it was given while it had none, and goes back to it. So the way
// back to nothing failed for a select born empty, and for one taken off twice, whichever value the form held.

const words = z.object({
  rowVersion: z.string(),
  field: z
    .string()
    .optional()
    .meta({
      choices: [
        { value: 'ED', label: 'Events' },
        { value: 'FOD', label: 'Flights' },
      ],
    }),
});

describe.each([
  ['an enum', z.object({ rowVersion: z.string(), field: enumCase.field }), undefined],
  ['a set of words', words, ''],
])('%s, a select', (_kind, schema, nothing) => {
  const pick = async (user: UserEvent, name: string) => {
    await user.click(combobox());
    await user.click(screen.getByRole('option', { name }));
  };

  test('born with nothing, chosen and put back, it draws nothing', async () => {
    const { onSubmit, user } = renderRow(schema, { field: nothing });

    await pick(user, 'Flights');
    expect(combobox()).toHaveTextContent('Flights');
    await pick(user, 'Nothing here');

    expect(combobox()).not.toHaveTextContent('Flights');
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'one', field: nothing });
  });

  test('put back twice, it draws nothing both times', async () => {
    const { onSubmit, user } = renderRow(schema, { field: 'ED' });

    await pick(user, 'Nothing here');
    expect(combobox()).not.toHaveTextContent('Events');
    await pick(user, 'Flights');
    expect(combobox()).toHaveTextContent('Flights');
    await pick(user, 'Nothing here');

    expect(combobox()).not.toHaveTextContent('Flights');
    expect(await saved(user, onSubmit)).toEqual({ rowVersion: 'one', field: nothing });
  });
});
