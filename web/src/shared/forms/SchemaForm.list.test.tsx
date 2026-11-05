import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import { ApiError } from '../api/problem';

import { SchemaForm } from './SchemaForm';
import { entryTitleField, localized, readFields } from './schema';

/**
 * The entries of a repeatable list are closed rows, and an optional date can be taken off (design of the
 * editor, phase 3, §4.3). Every form of every module draws its lists through here.
 */

const labels = {
  test: {
    fields: {
      steps: 'Steps',
      'steps.name': 'Name',
      'steps.hours': 'Hours',
      when: 'When',
      until: 'Until',
    },
    options: { 'steps.kind': { Tower: 'Tower control' } },
  },
};

const schema = z.object({
  steps: z.array(z.object({ name: z.string().min(1), hours: z.number().optional() })),
});
type Values = z.infer<typeof schema>;

const three: Values = { steps: [{ name: 'First' }, { name: 'Second' }, { name: 'Third' }] };

function renderList(
  defaults: Values = three,
  onSubmit: (values: Values) => Promise<unknown> = () => Promise.resolve(),
) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={defaults}
      locales={['en', 'it']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );
}

const row = (name: string | RegExp) => screen.getByRole('button', { name });
const box = (index: number) => document.getElementById(`steps.${index}.name`) as HTMLInputElement;

test('an entry is a closed row with its number and its title', () => {
  renderList();

  expect(row('1 First')).toHaveAttribute('aria-expanded', 'false');
  expect(row('3 Third')).toHaveAttribute('aria-expanded', 'false');
  expect(box(0)).not.toBeVisible();
});

test('a list that opens with one entry shows it, and it can still be closed', async () => {
  // Carmine, 5 November 2026: a pilot opening a report would otherwise find a row to click first.
  const user = userEvent.setup();
  renderList({ steps: [{ name: 'Only' }] });

  expect(box(0)).toBeVisible();

  await user.click(row('1 Only'));
  expect(box(0)).not.toBeVisible();
});

test('the entry shown at the opening closes like any other when another is added', async () => {
  const user = userEvent.setup();
  renderList({ steps: [{ name: 'Only' }] });

  await user.click(row('Add'));

  expect(box(1)).toBeVisible();
  expect(box(0)).not.toBeVisible();
});

test('a list that reaches one entry by removing the others does not open it', async () => {
  // The rule is about how the form opens: nobody asked for this entry to be opened.
  const user = userEvent.setup();
  renderList({ steps: [{ name: 'First' }, { name: 'Second' }] });

  await user.click(screen.getAllByRole('button', { name: 'Remove' })[1] as HTMLElement);

  expect(row('1 First')).toHaveAttribute('aria-expanded', 'false');
});

test('one click opens an entry and closes the one open before', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('1 First'));
  expect(box(0)).toBeVisible();

  await user.click(row('2 Second'));
  expect(box(1)).toBeVisible();
  expect(box(0)).not.toBeVisible();

  await user.click(row('2 Second'));
  expect(box(1)).not.toBeVisible();
});

test('the title follows what is typed, and an entry with nothing written says which one it is', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('2 Second'));
  await user.clear(box(1));
  expect(row('Item 2')).toBeInTheDocument();

  await user.type(box(1), 'Renamed');
  expect(row('2 Renamed')).toBeInTheDocument();
});

test('a new entry is born open, and the one open before closes', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('1 First'));
  await user.click(row('Add'));

  expect(box(3)).toBeVisible();
  expect(box(0)).not.toBeVisible();
  expect(row('Item 4')).toHaveAttribute('aria-expanded', 'true');
});

test('an entry the schema refuses opens by itself, and stays open while the error is being fixed', async () => {
  const user = userEvent.setup();
  renderList({ steps: [{ name: 'First' }, { name: '' }, { name: 'Third' }] });

  expect(box(1)).not.toBeVisible();
  await user.click(row('Save'));

  await waitFor(() => expect(box(1)).toBeVisible());
  expect(box(0)).not.toBeVisible();
  await waitFor(() => expect(box(1)).toHaveFocus());

  // Closing it does not hide the error: an error in a closed entry is an error nobody sees.
  await user.click(row('Item 2'));
  expect(box(1)).toBeVisible();

  // The letter that ends the error does not close the entry under the hands of whoever typed it.
  await user.type(box(1), 'x');
  expect(box(1)).toBeVisible();
  expect(box(1)).toHaveValue('x');
});

test('an entry the server refuses opens by itself too', async () => {
  const user = userEvent.setup();
  const refusal = new ApiError(400, {
    title: 'Bad',
    status: 400,
    errors: { 'steps[2].name': ['errors.required'] },
  });
  renderList(three, () => Promise.reject(refusal));

  await user.click(row('Save'));

  await waitFor(() => expect(box(2)).toBeVisible());
  expect(box(1)).not.toBeVisible();
});

test('an error shows every entry that holds one: opening another does not hide them', async () => {
  const user = userEvent.setup();
  renderList({ steps: [{ name: '' }, { name: '' }, { name: 'Third' }] });

  await user.click(row('Save'));
  await waitFor(() => expect(box(1)).toBeVisible());
  expect(box(0)).toBeVisible();

  await user.click(row('3 Third'));
  expect(box(2)).toBeVisible();
  expect(box(0)).toBeVisible();
  expect(box(1)).toBeVisible();
});

test('the open entry goes where the arrows take it, and what is written goes with it', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('1 First'));
  await user.type(box(0), '!');

  const moveDown = screen.getAllByRole('button', { name: 'Move down' })[0];
  await user.click(moveDown as HTMLElement);

  expect(row('2 First!')).toHaveAttribute('aria-expanded', 'true');
  expect(box(1)).toHaveValue('First!');
  expect(row('1 Second')).toHaveAttribute('aria-expanded', 'false');
});

test('moving the closed entry beside the open one leaves the open one open', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('2 Second'));
  await user.click(screen.getAllByRole('button', { name: 'Move up' })[2] as HTMLElement);

  expect(row('3 Second')).toHaveAttribute('aria-expanded', 'true');
  expect(row('2 Third')).toHaveAttribute('aria-expanded', 'false');
});

test('removing an entry leaves the open one open, whichever side it was on', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('2 Second'));

  // One after it: the open one has not moved.
  await user.click(screen.getAllByRole('button', { name: 'Remove' })[2] as HTMLElement);
  expect(row('2 Second')).toHaveAttribute('aria-expanded', 'true');

  // One before it: the open one has moved up, and stays open.
  await user.click(screen.getAllByRole('button', { name: 'Remove' })[0] as HTMLElement);
  expect(row('1 Second')).toHaveAttribute('aria-expanded', 'true');
});

test('removing the open entry leaves the next one closed', async () => {
  const user = userEvent.setup();
  renderList();

  await user.click(row('1 First'));
  await user.click(screen.getAllByRole('button', { name: 'Remove' })[0] as HTMLElement);

  expect(row('1 Second')).toHaveAttribute('aria-expanded', 'false');
});

test('a form sends what is in its closed entries, as it sends what is in the open ones', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderList(three, onSubmit);

  await user.click(row('2 Second'));
  await user.clear(box(1));
  await user.type(box(1), 'Changed');
  await user.click(row('1 First'));
  await user.click(row('Save'));

  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({
    steps: [
      { name: 'First', hours: undefined },
      { name: 'Changed', hours: undefined },
      { name: 'Third', hours: undefined },
    ],
  });
});

test('a closed entry is validated as a drawn one is: a box turns what the server sent into what the schema wants', async () => {
  // The reason a closed entry stays mounted. A null is what the server sends for a number nobody wrote,
  // and the box for a number has always handed the schema "no number" for it: an entry that was never
  // drawn would hand it the null, and the form could not be saved.
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderList(
    {
      steps: [
        { name: 'First', hours: null as unknown as number },
        { name: 'Second', hours: null as unknown as number },
      ],
    },
    onSubmit,
  );

  expect(box(0)).not.toBeVisible();
  await user.click(row('Save'));

  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({
    steps: [
      { name: 'First', hours: undefined },
      { name: 'Second', hours: undefined },
    ],
  });
});

/** A list in a list, as the table block's rows and their cells. */
const nested = z.object({
  rows: z.array(z.object({ cells: z.array(z.object({ text: z.string().min(1) })) })),
});
const nestedLabels = {
  test: { fields: { rows: 'Rows', 'rows.cells': 'Cells', 'rows.cells.text': 'Text' } },
};

function renderNested(
  defaults: z.infer<typeof nested>,
  onSubmit: (values: z.infer<typeof nested>) => Promise<unknown> = () => Promise.resolve(),
) {
  return renderWithProviders(
    <SchemaForm
      schema={nested}
      defaults={defaults}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(nestedLabels) },
  );
}

const cell = (at: number, of: number) =>
  document.getElementById(`rows.${at}.cells.${of}.text`) as HTMLInputElement;

test('a list inside an entry closes on its own, and the two do not touch each other', async () => {
  const user = userEvent.setup();
  renderNested({
    rows: [{ cells: [{ text: 'A' }, { text: 'B' }] }, { cells: [{ text: 'C' }, { text: 'D' }] }],
  });

  // The title of a row is not looked for inside its cells, so it says which row it is.
  await user.click(row('Item 1'));
  expect(cell(0, 0)).not.toBeVisible();

  await user.click(row('1 A'));
  expect(cell(0, 0)).toBeVisible();
  expect(row('Item 1')).toHaveAttribute('aria-expanded', 'true');

  // Opening a cell does not close its row; opening another row does, and its cells keep what was open.
  await user.click(row('2 B'));
  expect(cell(0, 0)).not.toBeVisible();
  expect(cell(0, 1)).toBeVisible();

  await user.click(row('Item 2'));
  expect(cell(0, 1)).not.toBeVisible();
  await user.click(row('Item 1'));
  expect(cell(0, 1)).toBeVisible();
});

test('a cell the schema refuses inside a closed row opens both, and the rest is sent as it was', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderNested(
    {
      rows: [{ cells: [{ text: 'A' }, { text: 'B' }] }, { cells: [{ text: 'C' }, { text: '' }] }],
    },
    onSubmit,
  );

  await user.click(row('Save'));

  await waitFor(() => expect(cell(1, 1)).toBeVisible());
  expect(cell(1, 0)).not.toBeVisible();
  expect(cell(0, 0)).not.toBeVisible();
  expect(onSubmit).not.toHaveBeenCalled();

  await user.type(cell(1, 1), 'D');
  await user.click(row('Save'));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({
    rows: [{ cells: [{ text: 'A' }, { text: 'B' }] }, { cells: [{ text: 'C' }, { text: 'D' }] }],
  });
});

test('an entry takes its title from a translated field in the language on screen, or the first one written', async () => {
  const translated = z.object({ steps: z.array(z.object({ name: localized() })) });
  const { i18n } = renderWithProviders(
    <SchemaForm
      schema={translated}
      defaults={{ steps: [{ name: { en: 'Hello', it: 'Ciao' } }, { name: { en: '', it: 'Solo italiano' } }] }}
      locales={['en', 'it']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
    { i18n: createTestI18n(labels) },
  );

  expect(row('1 Hello')).toBeInTheDocument();
  expect(row('2 Solo italiano')).toBeInTheDocument();

  await act(() => i18n.changeLanguage('it'));
  expect(row('1 Ciao')).toBeInTheDocument();
});

test('an entry of a closed choice is called what the choice calls it', () => {
  const choosing = z.object({
    steps: z.array(z.object({ kind: z.enum(['Tower']) })),
    picked: z.array(z.object({ code: z.string().meta({ choices: [{ value: 'a1', label: 'Alpha' }] }) })),
    found: z.array(
      z.object({
        code: z.string().meta({ suggestions: [{ value: 's1', label: 'Sierra' }], suggestionsOnly: true }),
      }),
    ),
  });
  renderWithProviders(
    <SchemaForm
      schema={choosing}
      defaults={{
        steps: [{ kind: 'Tower' as const }],
        picked: [{ code: 'a1' }],
        found: [{ code: 's1' }],
      }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
    {
      i18n: createTestI18n({
        test: { ...labels.test, options: { 'steps.kind': { Tower: 'Tower control' } } },
      }),
    },
  );

  expect(row('1 Tower control')).toBeInTheDocument();
  expect(row('1 Alpha')).toBeInTheDocument();
  expect(row('1 Sierra')).toBeInTheDocument();
});

test.each([
  [
    'the first thing written',
    z.object({ icon: z.string().meta({ icon: true }), title: z.string() }),
    'list.title',
  ],
  [
    'not a hidden field',
    z.object({ key: z.string().meta({ hidden: true }), label: localized() }),
    'list.label',
  ],
  ['nor a choice, while there is text', z.object({ kind: z.enum(['A']), name: z.string() }), 'list.name'],
  ['inside a group', z.object({ link: z.object({ label: z.string() }) }), 'list.link.label'],
  [
    'what the list says it is',
    z.object({ name: z.string(), to: z.string().meta({ entryTitle: true }) }),
    'list.to',
  ],
  ['a choice, when there is nothing else', z.object({ kind: z.enum(['A']), on: z.boolean() }), 'list.kind'],
  [
    'a date, when there is nothing else',
    z.object({ from: z.string().optional().meta({ datetime: true }), on: z.boolean() }),
    'list.from',
  ],
  ['nothing, for a list of pictures', z.object({ mediaId: z.number().meta({ media: true }) }), null],
  ['nothing inside a list of its own', z.object({ cells: z.array(z.object({ text: z.string() })) }), null],
])('the title of an entry is %s', (_, entry, path) => {
  const [list] = readFields(z.object({ list: z.array(entry) }));
  if (list?.kind !== 'list') {
    throw new Error('A list was expected.');
  }

  expect(entryTitleField(list.children)?.path ?? null).toBe(path);
});

const dated = z.object({
  when: z.string().optional().meta({ date: true }),
  until: z.string().optional().meta({ datetime: true }),
});

function renderDates(onSubmit: (values: z.infer<typeof dated>) => Promise<unknown>) {
  return renderWithProviders(
    <SchemaForm
      schema={dated}
      defaults={{ when: '2026-01-02T00:00:00Z', until: '2026-01-02T10:30:00Z' }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
      division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('an optional date is taken off with the button beside it: it leaves the box and the form', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderDates(onSubmit);

  const when = screen.getByLabelText('When');
  expect(when).toHaveValue('2026-01-02');

  await user.click(screen.getByRole('button', { name: 'Remove the date (When)' }));

  expect(when).toHaveValue('');
  // Nothing to take off any more; the other date keeps its own.
  expect(screen.queryByRole('button', { name: 'Remove the date (When)' })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Remove the date (Until)' })).toBeInTheDocument();
  expect(screen.getByLabelText('Until')).toHaveValue('2026-01-02T10:30');

  await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({ when: undefined, until: '2026-01-02T10:30:00Z' });
});

/** A form whose row moves under it, on a button: another action on the same row, as `useNewerRow` says. */
function MovingRow<TValues extends { rowVersion: string }>({
  schema: drawn,
  row,
  onSubmit,
}: {
  schema: z.ZodType<TValues, TValues>;
  row: TValues;
  onSubmit: (values: TValues) => Promise<unknown>;
}) {
  const [rowVersion, setRowVersion] = useState('one');

  return (
    <>
      <button type="button" onClick={() => setRowVersion('two')}>
        Another action
      </button>
      <SchemaForm
        schema={drawn}
        defaults={{ ...row, rowVersion }}
        locales={['en']}
        labels="test"
        submitLabel="Save"
        onSubmit={onSubmit}
        division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
      />
    </>
  );
}

test('what is open stays open when the row moves under the form', async () => {
  const versioned = schema.extend({ rowVersion: z.string() });
  const user = userEvent.setup();
  renderWithProviders(
    <MovingRow schema={versioned} row={{ ...three, rowVersion: 'one' }} onSubmit={() => Promise.resolve()} />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(row('2 Second'));
  await user.click(row('Another action'));

  await waitFor(() => expect(box(1)).toBeVisible());
  expect(row('2 Second')).toHaveAttribute('aria-expanded', 'true');
  expect(row('1 First')).toHaveAttribute('aria-expanded', 'false');
});

test('a date taken off stays off when the row moves under the form', async () => {
  const versioned = dated.extend({ rowVersion: z.string() });
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderWithProviders(
    <MovingRow
      schema={versioned}
      row={{ when: '2026-01-02T00:00:00Z', until: '2026-01-02T10:30:00Z', rowVersion: 'one' }}
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(screen.getByRole('button', { name: 'Remove the date (When)' }));
  await user.click(screen.getByRole('button', { name: 'Another action' }));

  await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled());
  expect(screen.getByLabelText('When')).toHaveValue('');
  await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({
    when: undefined,
    until: '2026-01-02T10:30:00Z',
    rowVersion: 'two',
  });
});

test('a date taken off inside an entry stays off when the row moves under the form', async () => {
  const entries = z.object({
    rowVersion: z.string(),
    slots: z.array(z.object({ name: z.string(), when: z.string().optional().meta({ date: true }) })),
  });
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());
  renderWithProviders(
    <MovingRow
      schema={entries}
      row={{ rowVersion: 'one', slots: [{ name: 'Only', when: '2026-01-02T00:00:00Z' }] }}
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(screen.getByRole('button', { name: /^Remove the date/ }));
  await user.click(screen.getByRole('button', { name: 'Another action' }));

  await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled());
  expect(document.getElementById('slots.0.when')).toHaveValue('');
  await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith({
    rowVersion: 'two',
    slots: [{ name: 'Only', when: undefined }],
  });
});

test('a date emptied from the keyboard stays empty too, and can be written again', async () => {
  // It used to draw itself again: the box showed the date while the form held nothing and saved nothing.
  const onSubmit = vi.fn(() => Promise.resolve());
  const user = userEvent.setup();
  renderDates(onSubmit);

  const when = screen.getByLabelText('When');
  fireEvent.change(when, { target: { value: '' } });
  expect(when).toHaveValue('');

  fireEvent.change(when, { target: { value: '2027-05-06' } });
  expect(when).toHaveValue('2027-05-06');
  expect(screen.getByRole('button', { name: 'Remove the date (When)' })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(onSubmit).toHaveBeenCalled());
  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ when: '2027-05-06T00:00:00Z' }));
});

test('a required date, and an empty one, have nothing to take off', () => {
  renderWithProviders(
    <SchemaForm
      schema={z.object({
        when: z.string().meta({ date: true }),
        until: z.string().optional().meta({ date: true }),
      })}
      defaults={{ when: '2026-01-02T00:00:00Z' }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
    { i18n: createTestI18n(labels) },
  );

  expect(screen.queryByRole('button', { name: /Remove the date/ })).not.toBeInTheDocument();
});

test('a date taken off in a form that applies as it is written is applied', async () => {
  const user = userEvent.setup();
  const applied = vi.fn();

  function Live() {
    const [values] = useState({ when: '2026-01-02T00:00:00Z' });
    return (
      <SchemaForm
        schema={z.object({ when: z.string().optional().meta({ date: true }) })}
        defaults={values}
        locales={['en']}
        labels="test"
        onChange={applied}
      />
    );
  }

  renderWithProviders(<Live />, { i18n: createTestI18n(labels) });
  await user.click(screen.getByRole('button', { name: 'Remove the date (When)' }));

  await waitFor(() => expect(applied).toHaveBeenCalledWith({ when: undefined }));
  expect(within(document.body).getByLabelText('When')).toHaveValue('');
});

test('a date inside an entry follows the entry when it moves', async () => {
  const user = userEvent.setup();
  renderWithProviders(
    <SchemaForm
      schema={z.object({
        steps: z.array(z.object({ name: z.string(), when: z.string().optional().meta({ date: true }) })),
      })}
      defaults={{
        steps: [
          { name: 'First', when: '2026-01-01T00:00:00Z' },
          { name: 'Second', when: '2026-02-02T00:00:00Z' },
        ],
      }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
    { i18n: createTestI18n(labels) },
  );

  await user.click(screen.getAllByRole('button', { name: 'Move down' })[0] as HTMLElement);

  expect(document.getElementById('steps.0.when')).toHaveValue('2026-02-02');
  expect(document.getElementById('steps.1.when')).toHaveValue('2026-01-01');
});
