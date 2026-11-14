import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';

/**
 * A switch takes the sentence under it like any other field (PR 3.13: "Several open at once" of the questions and
 * answers says when it counts). Before, `<labels>.hints.<path>` was read for every field and drawn for every kind but
 * this one.
 */

const words = {
  test: {
    fields: { several: 'Several open at once', numbered: 'Numbered' },
    hints: { several: 'Only when the answers start closed.' },
  },
};

test('a switch with a hint shows it under itself, and one without shows nothing more', async () => {
  const i18n = createTestI18n(words);
  await i18n.changeLanguage('en');

  renderWithProviders(
    <SchemaForm
      schema={z.object({ several: z.boolean(), numbered: z.boolean() })}
      defaults={{ several: false, numbered: false }}
      locales={['en', 'it']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
      division={{ defaultLocale: 'en', timezone: 'UTC' }}
    />,
    { i18n },
  );

  expect(screen.getByRole('switch', { name: 'Several open at once' })).toBeInTheDocument();
  expect(screen.getByText('Only when the answers start closed.')).toBeInTheDocument();
  expect(screen.getByRole('switch', { name: 'Numbered' })).toBeInTheDocument();
  expect(screen.getAllByText(/Only when/)).toHaveLength(1);
});
