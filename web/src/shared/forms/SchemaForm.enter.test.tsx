import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';
import { z } from 'zod';

import englishCommon from '../../../../locales/en/common.json';
import italianCommon from '../../../../locales/it/common.json';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { localized } from './schema';

/**
 * What a screen reader is told about Enter under a form (#224). "Press Enter to save" was read under every
 * form, and in a box of many lines Enter starts a new line: under the slot sheet of an event — one such box and
 * a select — it promised a save that never happens.
 */

const words = {
  test: {
    fields: {
      title: 'Title',
      weight: 'Weight',
      day: 'Day',
      text: 'Sheet',
      mode: 'Mode',
      message: 'Message',
      summary: 'Summary',
      published: 'Published',
      contact: 'Contact',
      'contact.note': 'Note',
      rowVersion: 'Version',
    },
    options: { mode: { Add: 'Add', ReplaceFree: 'Replace the free ones' } },
  },
};

async function draw<T extends Record<string, unknown>>(
  schema: z.ZodType<T, T>,
  defaults: T,
  { language = 'en', actionsElsewhere = false }: { language?: 'en' | 'it'; actionsElsewhere?: boolean } = {},
) {
  const i18n = createTestI18n(words);
  await i18n.changeLanguage(language);

  renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={defaults}
      locales={['en', 'it']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
      actionsElsewhere={actionsElsewhere}
      division={{ defaultLocale: 'en', timezone: 'UTC' }}
    />,
    { i18n },
  );
}

/** Both sentences, in the language the form is drawn in: which one is there, if any. */
function hints(language: 'en' | 'it' = 'en') {
  const form = (language === 'en' ? englishCommon : italianCommon).form;

  return {
    all: screen.queryByText(form.submitHint),
    oneLine: screen.queryByText(form.submitHintOneLine),
  };
}

test('a form of boxes of one line says that Enter saves, as it always did', async () => {
  await draw(
    z.object({ title: z.string(), weight: z.number(), day: z.string().meta({ date: true }) }),
    { title: '', weight: 0, day: '' },
  );

  expect(await screen.findByLabelText('Title')).toBeInTheDocument();
  expect(hints().all).toBeInTheDocument();
  expect(hints().oneLine).not.toBeInTheDocument();
});

test('a form whose only box is many lines, beside a select, says nothing about Enter', async () => {
  // The slot sheet of an event: a table pasted over many lines, and what to do with the slots already there.
  await draw(z.object({ text: z.string().meta({ multiline: true }), mode: z.enum(['Add', 'ReplaceFree']) }), {
    text: '',
    mode: 'Add' as const,
  });

  expect(await screen.findByLabelText('Sheet')).toBeInTheDocument();
  expect(hints().all).not.toBeInTheDocument();
  expect(hints().oneLine).not.toBeInTheDocument();
});

test('a form with both kinds of box says that Enter saves from a box of one line', async () => {
  await draw(z.object({ title: z.string(), message: z.string().meta({ multiline: true }) }), {
    title: '',
    message: '',
  });

  expect(await screen.findByLabelText('Message')).toBeInTheDocument();
  expect(hints().oneLine).toBeInTheDocument();
  expect(hints().all).not.toBeInTheDocument();
});

test('a translated box of many lines, a nested one and a switch save nothing on Enter either', async () => {
  // Nor does the row version, a box of one line that is never drawn.
  await draw(
    z.object({
      summary: localized().meta({ localized: true, multiline: true }),
      published: z.boolean(),
      contact: z.object({ note: z.string().meta({ multiline: true }) }),
      rowVersion: z.string().meta({ hidden: true }),
    }),
    { summary: { en: '', it: '' }, published: false, contact: { note: '' }, rowVersion: 'v1' },
  );

  expect(await screen.findByLabelText('Note')).toBeInTheDocument();
  expect(hints().all).not.toBeInTheDocument();
  expect(hints().oneLine).not.toBeInTheDocument();
});

test('the rule holds when the button is drawn somewhere else, and in Italian', async () => {
  await draw(
    z.object({ title: z.string(), message: z.string().meta({ multiline: true }) }),
    { title: '', message: '' },
    { language: 'it', actionsElsewhere: true },
  );

  expect(await screen.findByLabelText('Message')).toBeInTheDocument();
  expect(hints('it').oneLine).toBeInTheDocument();
  expect(hints('it').all).not.toBeInTheDocument();
  expect(screen.queryByText(englishCommon.form.submitHintOneLine)).not.toBeInTheDocument();
});
