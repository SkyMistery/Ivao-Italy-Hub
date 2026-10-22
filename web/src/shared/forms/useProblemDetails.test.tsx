import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test } from 'vitest';
import { z } from 'zod';

import englishErrors from '../../../../locales/en/errors.json';
import { ApiError, type HubProblem } from '../api/problem';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { describeProblem } from './useProblemDetails';
import { localized, readFields, whereDrawn } from './schema';

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

/**
 * The same refusal, for the one control of the back office that is not a form. An upload has a
 * button and no fields, so the keys the server sent have nowhere to land except above it — and the
 * two keys the upload can answer with are resolved here rather than shown as keys.
 */
test('a refusal about a control that is not a form becomes one sentence', () => {
  const i18n = createTestI18n();
  const t = i18n.getFixedT(null, null);

  expect(describeProblem(undefined, t, 'en')).toBeNull();

  expect(describeProblem(new ApiError(400, { errors: { file: ['errors.media.tooLarge'] } }), t, 'en')).toBe(
    englishErrors.errors.media.tooLarge,
  );

  expect(
    describeProblem(new ApiError(400, { errors: { file: ['errors.media.typeNotAllowed'] } }), t, 'en'),
  ).toBe(englishErrors.errors.media.typeNotAllowed);

  // And a refusal with nothing to say about a field still says what happened.
  expect(describeProblem(new ApiError(403, undefined), t, 'en')).toBe(englishErrors.errors.forbidden.title);

  // A limit of the hub too: the core's 429 is a whole request refused, not a field.
  expect(describeProblem(new ApiError(429, undefined), t, 'en')).toBe(
    englishErrors.errors.tooManyRequests.title,
  );
});

/**
 * A refusal the form has no field for (#331). `id` is how the server refuses the whole row — an event
 * already started, a report already decided — and it used to be handed to a field nobody draws: the
 * button stopped spinning and nothing was said. Every such key is the sentence above the form now.
 */
const rowSchema = z.object({
  url: z.string(),
  items: z.array(z.object({ name: z.string() })),
  rowVersion: z.string().meta({ hidden: true }),
});
const rowLabels = { test: { fields: { url: 'Address', items: 'Items', 'items.name': 'Name' } } };

function renderRow(problem: HubProblem, items: { name: string }[] = []) {
  return renderWithProviders(
    <SchemaForm
      schema={rowSchema}
      defaults={{ url: '', items, rowVersion: 'AAA=' }}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.reject(new ApiError(400, problem))}
    />,
    { i18n: createTestI18n(rowLabels) },
  );
}

/** The sentence above the form: the one alert that carries the core's title. */
async function summary(): Promise<HTMLElement> {
  const alerts = await screen.findAllByRole('alert');
  const above = alerts.find((alert) => alert.textContent.includes(englishErrors.errors.title));
  if (above === undefined) {
    throw new Error('no sentence above the form');
  }
  return above;
}

test('a refusal of the whole row, under a name the form does not draw, is said above the form', async () => {
  const user = userEvent.setup();
  renderRow({ errors: { id: ['errors.token.tooMany'] } });

  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(await summary()).toHaveTextContent(englishErrors.errors.token.tooMany);
});

test('an empty name and a hidden field are said above the form too, next to a field that is drawn', async () => {
  const user = userEvent.setup();
  renderRow({
    errors: {
      url: ['errors.url.absolute'],
      '': ['errors.token.tooMany'],
      rowVersion: ['errors.conflict.title'],
    },
  });

  await user.click(screen.getByRole('button', { name: 'Save' }));

  const above = await summary();
  expect(above).toHaveTextContent(englishErrors.errors.token.tooMany);
  expect(above).toHaveTextContent(englishErrors.errors.conflict.title);
  // The one that has a field stays on it, and is not repeated above.
  expect(above).not.toHaveTextContent(englishErrors.errors.url.absolute);
  expect(
    screen.getAllByRole('alert').some((alert) => alert.textContent === englishErrors.errors.url.absolute),
  ).toBe(true);
});

test('an error of a list is drawn under the list, and one of an entry that is not there is said above', async () => {
  const user = userEvent.setup();
  renderRow(
    {
      errors: {
        items: ['errors.token.tooMany'],
        'items[0].Name': ['errors.url.absolute'],
        'items.3.name': ['errors.conflict.title'],
      },
    },
    [{ name: 'first' }],
  );

  await user.click(screen.getByRole('button', { name: 'Save' }));

  const list = screen.getByRole('group', { name: 'Items' });
  expect(await within(list).findByText(englishErrors.errors.token.tooMany)).toBeInTheDocument();
  // The server lowers only the first letter of a nested path, and the entry's field is found anyway.
  expect(within(list).getByText(englishErrors.errors.url.absolute)).toBeInTheDocument();
  // There is no fourth entry to point at.
  expect(await summary()).toHaveTextContent(englishErrors.errors.conflict.title);
});

test('where a server path is drawn, as the form names it', () => {
  const fields = readFields(
    z.object({
      title: localized(),
      seo: z.object({ title: z.string() }),
      items: z.array(z.object({ name: z.string() })),
      rowVersion: z.string().meta({ hidden: true }),
    }),
  );
  const values = { title: { en: '' }, seo: { title: '' }, items: [{ name: 'a' }], rowVersion: 'x' };
  const at = (path: string) => whereDrawn(fields, path, values);

  expect(at('title')).toBe('title');
  expect(at('Title')).toBe('title');
  expect(at('seo')).toBe('seo');
  expect(at('seo.title')).toBe('seo.title');
  expect(at('items')).toBe('items');
  expect(at('items[0].name')).toBe('items.0.name');
  expect(at('items.0.Name')).toBe('items.0.name');

  expect(at('id')).toBeNull();
  expect(at('')).toBeNull();
  expect(at('rowVersion')).toBeNull();
  // Inside a field that holds no fields, an entry by itself, an entry that is not there.
  expect(at('title.en')).toBeNull();
  expect(at('items.0')).toBeNull();
  expect(at('items.1.name')).toBeNull();
  expect(at('items.x.name')).toBeNull();
});
