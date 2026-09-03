import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { localized } from './schema';

/**
 * A translated field is one value with one entry per language, and the tabs are how a coordinator
 * fills it in. The badge on a tab and the copy button are the two things that make the difference
 * between "the Italian is missing" being obvious and being discovered by the server.
 */

const LOCALES = ['en', 'it'] as const;

const schema = z.object({ title: localized() });
const labels = { test: { fields: { title: 'Title' } } };

function renderField(defaults: Record<string, string>) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{ title: defaults }}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('marks the languages that have nothing in them yet', () => {
  renderField({ en: 'Flight plan', it: '' });

  const english = screen.getByRole('tab', { name: /English/ });
  const italian = screen.getByRole('tab', { name: /Italian/ });

  expect(within(english).queryByText('Empty')).not.toBeInTheDocument();
  expect(within(italian).getByText('Empty')).toBeInTheDocument();
});

test('copies from a language that has been written', async () => {
  const user = userEvent.setup();
  renderField({ en: 'Flight plan', it: '' });

  await user.click(screen.getByRole('tab', { name: /Italian/ }));
  await user.click(screen.getByRole('button', { name: 'Copy from English' }));

  expect(screen.getByRole('textbox')).toHaveValue('Flight plan');
});

test('offers nothing to copy when no other language has been written', async () => {
  const user = userEvent.setup();
  renderField({ en: '', it: '' });

  await user.click(screen.getByRole('tab', { name: /Italian/ }));

  expect(screen.queryByRole('button', { name: /Copy from/ })).not.toBeInTheDocument();
});
