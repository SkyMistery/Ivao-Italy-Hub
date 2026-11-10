import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm, FormProvider, useWatch } from 'react-hook-form';
import { expect, test } from 'vitest';
import type { z } from 'zod';

import {
  accordionSchema,
  calloutSchema,
  heroSchema,
  headingSchema,
  tabsSchema,
  textSchema,
  timelineSchema,
  welcomeSchema,
} from '../../blocks/schemas';
import { renderWithProviders } from '../../test/harness';

import { LocaleFields } from './LocaleFields';
import { readFields, type FieldNode } from './schema';

/**
 * The formatting bar, in the box of a text the renderer draws as Markdown (design of the editor, phase 3, §4.6). What it
 * writes is `markdownEdits.test.ts`; here is where it is and that what it writes reaches the form.
 */

function Form({ markdown }: { markdown: boolean }) {
  const form = useForm({ defaultValues: { body: { en: 'one word here' } } });
  const value = useWatch({ control: form.control, name: 'body.en' });

  return (
    <FormProvider {...form}>
      <LocaleFields
        path="body"
        label="Body"
        locales={['en']}
        multiline
        markdown={markdown}
        error={undefined}
      />
      <output>{value}</output>
    </FormProvider>
  );
}

function box() {
  return screen.getByRole<HTMLTextAreaElement>('textbox');
}

function select(from: number, to: number) {
  box().focus();
  box().setSelectionRange(from, to);
}

const held = () => screen.getByRole('status').textContent;

test('the bar is there only on a text the renderer draws as Markdown', () => {
  const { unmount } = renderWithProviders(<Form markdown={false} />);
  expect(screen.queryByRole('toolbar')).toBeNull();
  unmount();

  renderWithProviders(<Form markdown />);
  expect(screen.getByRole('toolbar')).toBeTruthy();
});

test('a button writes around the selection, into the form, and leaves the selection on the words', async () => {
  renderWithProviders(<Form markdown />);
  select(4, 8);

  await userEvent.click(within(screen.getByRole('toolbar')).getByRole('button', { name: 'Bold (Ctrl+B)' }));

  expect(held()).toBe('one **word** here');
  expect(box().value).toBe('one **word** here');
  expect(document.activeElement).toBe(box());
  expect(box().value.slice(box().selectionStart, box().selectionEnd)).toBe('word');
});

test.each([
  [{ ctrlKey: true, key: 'b' }, 'one **word** here'],
  [{ metaKey: true, key: 'b' }, 'one **word** here'],
  [{ ctrlKey: true, key: 'i' }, 'one *word* here'],
  // Not the shortcut of the bar: it stays the browser's.
  [{ ctrlKey: true, shiftKey: true, key: 'b' }, 'one word here'],
  [{ key: 'b' }, 'one word here'],
])('%o in the box', (keys, expected) => {
  renderWithProviders(<Form markdown />);
  select(4, 8);

  fireEvent.keyDown(box(), keys);

  expect(held()).toBe(expected);
});

test('a colour comes from the closed set, and no colour is one of the choices', async () => {
  renderWithProviders(<Form markdown />);
  select(4, 8);

  await userEvent.click(screen.getByRole('button', { name: 'Colour of the text' }));

  // The names of the colours are the set, and the brand's orange is not among them.
  for (const colour of ['Blue', 'Light blue', 'Teal', 'Red', 'No colour']) {
    expect(screen.getByRole('button', { name: colour })).toBeTruthy();
  }
  expect(screen.queryByRole('button', { name: /orange/i })).toBeNull();

  await userEvent.click(screen.getByRole('button', { name: 'Light blue' }));
  expect(held()).toBe('one :ocean[word] here');

  await userEvent.click(screen.getByRole('button', { name: 'Colour of the text' }));
  await userEvent.click(screen.getByRole('button', { name: 'No colour' }));
  expect(held()).toBe('one word here');
});

test('a link is written with the address given', async () => {
  renderWithProviders(<Form markdown />);
  select(4, 8);

  await userEvent.click(screen.getByRole('button', { name: 'Link' }));
  await userEvent.type(screen.getByRole('combobox'), 'https://example.org');
  await userEvent.keyboard('{Enter}');

  expect(held()).toBe('one [word](https://example.org) here');
});

/** Whether the field at `path` has the bar. */
function annotated(schema: z.ZodType, path: string): boolean {
  const leaves = (fields: readonly FieldNode[]): FieldNode[] =>
    fields.flatMap((field) =>
      field.kind === 'list' || field.kind === 'object' ? leaves(field.children) : [field],
    );

  const field = leaves(readFields(schema)).find((candidate) => candidate.path === path);
  if (field === undefined) {
    throw new Error(`No field at ${path}`);
  }

  return field.meta.markdown === true;
}

test.each([
  ['text', textSchema, 'markdown'],
  ['callout', calloutSchema, 'text'],
  ['tabs', tabsSchema, 'tabs.body'],
  ['accordion', accordionSchema, 'items.answer'],
  ['welcome', welcomeSchema, 'message'],
] as const)('%s is drawn as Markdown, and has the bar', (_name, schema, path) => {
  expect(annotated(schema, path)).toBe(true);
});

test.each([
  ['hero', heroSchema, 'text'],
  ['timeline', timelineSchema, 'items.text'],
  ['heading', headingSchema, 'text'],
] as const)('%s is drawn as plain text, and an asterisk there stays one', (_name, schema, path) => {
  expect(annotated(schema, path)).toBe(false);
});
