import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, test } from 'vitest';
import { z } from 'zod';

import italianErrors from '../../../../locales/it/errors.json';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { speakTheServersWords, zodIssueKey } from './zodWords';

// `z.config` is zod's own, for the whole file: put back what zod says by itself after every test.
afterEach(() => {
  z.config({ customError: undefined });
});

/** The key each problem of a schema becomes, read through a validation that is handed the map on its own. */
function keysOf(schema: z.ZodType, value: unknown): string[] {
  const result = schema.safeParse(value, { error: (issue) => zodIssueKey(issue) });
  return result.success ? [] : result.error.issues.map((issue) => issue.message);
}

test('a text too long is the key the server answers for it', () => {
  expect(keysOf(z.string().max(16), 'RAVA5A RAVA5A RAVA5A')).toEqual(['errors.text.tooLong']);
});

test('a value missing, or a text or a list left empty under a minimum of one, is a required field', () => {
  expect(keysOf(z.object({ sid: z.string() }), {})).toEqual(['errors.required']);
  expect(keysOf(z.number(), null)).toEqual(['errors.required']);
  expect(keysOf(z.string().trim().min(1), '   ')).toEqual(['errors.required']);
  expect(keysOf(z.array(z.string()).min(1), [])).toEqual(['errors.required']);
});

test('anything else is a value that is not valid, and never zod’s English', () => {
  expect(keysOf(z.string().min(4), 'ab')).toEqual(['errors.invalid']);
  expect(keysOf(z.number().int().min(2), 1)).toEqual(['errors.invalid']);
  expect(keysOf(z.string().regex(/^[A-Z]{4}$/), 'limc')).toEqual(['errors.invalid']);
  expect(keysOf(z.enum(['Public', 'Staff']), 'Nobody')).toEqual(['errors.invalid']);
  expect(keysOf(z.string(), 42)).toEqual(['errors.invalid']);
});

test('a message a schema writes itself still wins', () => {
  speakTheServersWords(createTestI18n());

  const result = z.string().max(1, 'its own').safeParse('ab');
  expect(result.error?.issues[0]?.message).toBe('its own');
});

test('the generated form says the server’s words in the language on screen, under a field checked in the browser', async () => {
  const user = userEvent.setup();
  const i18n = createTestI18n({ test: { fields: { sid: 'SID' } } });
  i18n.addResourceBundle('it', 'errors', italianErrors);
  await i18n.changeLanguage('it');
  speakTheServersWords(i18n);

  renderWithProviders(
    <SchemaForm
      // The pilot's report: `sid` is `z.string().trim().max(MAX_PROCEDURE)`, and the procedure is sixteen characters.
      schema={z.object({ sid: z.string().trim().max(16) })}
      defaults={{ sid: '' }}
      locales={['it', 'en']}
      labels="test"
      submitLabel="Invia"
      onSubmit={() => Promise.resolve()}
    />,
    { i18n },
  );

  await user.type(screen.getByLabelText('SID'), 'RAVA5A RAVA5A RAVA5A');
  await user.click(screen.getByRole('button', { name: 'Invia' }));

  // ⚠️ It read «Too big: expected string to have <=16 characters», zod's own English, on a screen in Italian.
  expect(await screen.findByRole('alert')).toHaveTextContent('Questo testo è troppo lungo.');
});
