import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test } from 'vitest';
import { z } from 'zod';

import englishErrors from '../../../../locales/en/errors.json';
import { ApiError, type HubProblem } from '../api/problem';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { localized } from './schema';

/**
 * The server sends i18n keys and never sentences, so this is where a refusal becomes something a
 * coordinator can read. What is tested here is exactly the contract of `CrudProblems`: a key per
 * field, and — when languages are missing — the `localized` extension that says which ones.
 */

const LOCALES = ['en', 'it'] as const;

const schema = z.object({ title: localized(), url: z.string() });
const labels = { test: { fields: { title: 'Title', url: 'Address' } } };

function renderRefusedForm(status: number, problem: HubProblem | undefined) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{ title: { en: '', it: '' }, url: '' }}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.reject(new ApiError(status, problem))}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('puts a field error on its field, resolved from the key', async () => {
  const user = userEvent.setup();
  renderRefusedForm(400, { errors: { url: ['errors.url.absolute'] } });

  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(await screen.findByRole('alert')).toHaveTextContent(englishErrors.errors.url.absolute);
});

test('names the languages that are missing, from the extension', async () => {
  const user = userEvent.setup();
  renderRefusedForm(400, {
    errors: { title: ['errors.localized.missing'] },
    localized: { title: ['it'] },
  });

  await user.click(screen.getByRole('button', { name: 'Save' }));

  // Not "invalid", and not "errors.localized.missing" either: the language, by name.
  expect(await screen.findByRole('alert')).toHaveTextContent('Still missing in: Italian.');
});

test('a refusal that is about no field at all becomes the alert above the form', async () => {
  const user = userEvent.setup();
  renderRefusedForm(409, { title: 'ignored, the client knows the sentence for a 409' });

  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(await screen.findByRole('alert')).toHaveTextContent(englishErrors.errors.conflict.title);
});

test('something that is not a refusal at all still says something', async () => {
  const user = userEvent.setup();
  renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{ title: { en: '', it: '' }, url: '' }}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.reject(new TypeError('the network went away'))}
    />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(await screen.findByRole('alert')).toHaveTextContent(englishErrors.errors.unknown);
});
