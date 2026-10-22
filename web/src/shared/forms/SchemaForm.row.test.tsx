import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';

/**
 * A form whose row moves under it (#332, item 1). The staff contact detail is the shape copied here:
 * a reply, outside the form, moves the status and the row version; the form below must send the new
 * version, or the next save is a 409 about the person themselves.
 */

const schema = z.object({
  status: z.string(),
  note: z.string(),
  rowVersion: z.string().meta({ hidden: true }),
});

type Values = z.infer<typeof schema>;

const labels = { test: { fields: { status: 'Status', note: 'Note' } } };

function Screen({ onSubmit }: { onSubmit: (values: Values) => Promise<unknown> }) {
  const [row, setRow] = useState({ status: 'New', note: '', rowVersion: 'v1' });

  return (
    <>
      {/* Another action on the same row: what a reply does to a contact message. */}
      <button type="button" onClick={() => setRow({ status: 'Answered', note: '', rowVersion: 'v2' })}>
        Reply
      </button>
      <SchemaForm
        schema={schema}
        defaults={row}
        locales={['en']}
        labels="test"
        submitLabel="Save"
        onSubmit={onSubmit}
      />
    </>
  );
}

test('a newer row version reaches the form, and what nobody touched shows the row as it is now', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  const user = userEvent.setup();
  renderWithProviders(<Screen onSubmit={onSubmit} />, { i18n: createTestI18n(labels) });

  await user.click(screen.getByRole('button', { name: 'Reply' }));
  await waitFor(() => expect(screen.getByLabelText('Status')).toHaveValue('Answered'));

  await user.click(screen.getByRole('button', { name: 'Save' }));

  await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  expect(onSubmit).toHaveBeenCalledWith({ status: 'Answered', note: '', rowVersion: 'v2' });
});

test('what the person has written survives the newer row', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  const user = userEvent.setup();
  renderWithProviders(<Screen onSubmit={onSubmit} />, { i18n: createTestI18n(labels) });

  await user.type(screen.getByLabelText('Note'), 'half a sentence');
  await user.clear(screen.getByLabelText('Status'));
  await user.type(screen.getByLabelText('Status'), 'Closed');

  await user.click(screen.getByRole('button', { name: 'Reply' }));

  await user.click(screen.getByRole('button', { name: 'Save' }));

  await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  // The person's words and choice, with the row's new version.
  expect(onSubmit).toHaveBeenCalledWith({
    status: 'Closed',
    note: 'half a sentence',
    rowVersion: 'v2',
  });
});
