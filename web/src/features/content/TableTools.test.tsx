import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import * as XLSX from 'xlsx';
import { afterEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { BlockEnvelope } from '../../blocks';
import { TableBlock } from '../../blocks/blocks';
import { tableSchema } from '../../blocks/schemas';
import { renderWithProviders } from '../../test/harness';

import { BlockProperties } from './BlockProperties';

/**
 * The table's tools in the properties panel (design of the editor's phase 3, §5.1, PR 3.12): paste from a spreadsheet,
 * import a file, make N × M, and the grid — the second declared exception to the generated form, which writes the same
 * `props` through the same `onApplyProps`. The harness's language is English, so the language on screen is `en`.
 */

const words = englishCommon.blocks.table;
const say = (text: string, values: Record<string, string | number>) =>
  Object.entries(values).reduce((said, [key, value]) => said.replace(`{{${key}}}`, String(value)), text);
const both = (it: string, en: string) => ({ it, en });

const props = {
  caption: both('Orari', 'Hours'),
  columns: [
    { label: both('Giorno', 'Day'), align: 'left' },
    { label: both('Dalle', 'From'), align: 'right' },
  ],
  rows: [{ cells: [{ text: both('Lunedì', 'Monday') }, { text: both('18:00z', '18:00z') }] }],
};

const section = {
  id: 's_1',
  layout: 'stacked' as const,
  background: 'none' as const,
  padding: 'md' as const,
  width: 'default' as const,
  blocks: [],
  sections: [],
};

/**
 * The panel as the editor holds it: what it applies becomes the block it is drawn with next, and an undo — the editor's,
 * from outside the panel — puts back the props before the last write.
 */
function Editor({
  onApply,
  start,
  locales,
}: {
  onApply: (props: Record<string, unknown>) => void;
  start: object;
  locales: readonly string[];
}) {
  const [history, setHistory] = useState<Record<string, unknown>[]>([{ ...start }]);
  const current = history.at(-1) ?? {};
  const block: BlockEnvelope = {
    id: 'b_1',
    type: 'table',
    version: 1,
    props: current,
    renderMode: null,
    frozen: null,
    column: 0,
    source: null,
    span: null,
    align: null,
  };

  return (
    <>
      <button
        type="button"
        onClick={() => setHistory((written) => written.slice(0, Math.max(1, written.length - 1)))}
      >
        Undo
      </button>
      <BlockProperties
        block={block}
        section={section}
        sections={[{ value: 's_1', label: 'Intro' }]}
        locales={locales}
        division={{ defaultLocale: 'it', timezone: 'Europe/Rome' }}
        mediaLibrary={{
          queryKey: ['media'],
          queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 20, total: 0 }),
        }}
        onApplyProps={(next) => {
          onApply(next);
          setHistory((written) => [...written, next]);
        }}
        onEnvelope={vi.fn()}
        onMoveTo={vi.fn()}
      />
    </>
  );
}

function draw(start: object = props, locales: readonly string[] = ['it', 'en']) {
  const onApply = vi.fn<(props: Record<string, unknown>) => void>();
  renderWithProviders(<Editor onApply={onApply} start={start} locales={locales} />);
  return { onApply, last: () => onApply.mock.lastCall?.[0] };
}

/** «The table now has …», as the language files put it together. */
function replacedLine(columns: number, rows: number) {
  const count = (one: string, other: string, n: number) => (n === 1 ? one : say(other, { count: n }));
  return say(words.tools.replaced, {
    columns: count(words.tools.columnCount_one, words.tools.columnCount_other, columns),
    rows: count(words.tools.rowCount_one, words.tools.rowCount_other, rows),
  });
}

function clipboardHolds(read: () => Promise<string>) {
  Object.defineProperty(navigator, 'clipboard', { value: { readText: read }, configurable: true });
}

afterEach(() => {
  Reflect.deleteProperty(navigator, 'clipboard');
});

test('a paste from Excel replaces the table, the first row its heading, in the language on screen', async () => {
  const user = userEvent.setup();
  const { last } = draw();
  clipboardHolds(() => Promise.resolve('Station\tFrequency\r\nLIMM_CTR\t125.500\r\nLIRR_CTR\t\r\n'));

  await user.click(screen.getByRole('button', { name: words.tools.paste }));

  expect(last()).toEqual({
    ...props,
    columns: [
      { label: both('', 'Station'), align: 'left' },
      { label: both('', 'Frequency'), align: 'left' },
    ],
    rows: [
      { cells: [{ text: both('', 'LIMM_CTR') }, { text: both('', '125.500') }] },
      { cells: [{ text: both('', 'LIRR_CTR') }, { text: both('', '') }] },
    ],
  });
  expect(screen.getByRole('status')).toHaveTextContent(replacedLine(2, 2));
});

test('the line that says what happened counts in words, is there before it says anything, and goes with an undo', async () => {
  const user = userEvent.setup();
  draw();
  // A live region is announced when its text changes, not when it is mounted with it: it waits, empty.
  expect(screen.getByRole('status')).toBeEmptyDOMElement();
  clipboardHolds(() => Promise.resolve('Only\nrow\n'));

  await user.click(screen.getByRole('button', { name: words.tools.paste }));
  expect(screen.getByRole('status')).toHaveTextContent(replacedLine(1, 1));
  expect(replacedLine(1, 1)).toBe('The table now has one column and one row.');

  // Undone, the table is no longer the one the line speaks of.
  await user.click(screen.getByRole('button', { name: 'Undo' }));
  expect(screen.getByRole('status')).toBeEmptyDOMElement();
});

test('without the heading ticked, the first row is a row', async () => {
  const user = userEvent.setup();
  const { last } = draw();
  clipboardHolds(() => Promise.resolve('a\tb\n'));

  await user.click(screen.getByLabelText(words.tools.header));
  await user.click(screen.getByRole('button', { name: words.tools.paste }));

  expect(last()?.rows).toEqual([{ cells: [{ text: both('', 'a') }, { text: both('', 'b') }] }]);
});

test('a clipboard the browser keeps, or that holds nothing, is said and changes nothing', async () => {
  const user = userEvent.setup();
  const { onApply } = draw();

  clipboardHolds(() => Promise.reject(new DOMException('denied', 'NotAllowedError')));
  await user.click(screen.getByRole('button', { name: words.tools.paste }));
  expect(screen.getByRole('alert')).toHaveTextContent(words.tools.clipboard);

  clipboardHolds(() => Promise.resolve('\t\n'));
  await user.click(screen.getByRole('button', { name: words.tools.paste }));
  expect(screen.getByRole('alert')).toHaveTextContent(words.tools.empty);

  expect(onApply).not.toHaveBeenCalled();
});

test('a file is read as a sheet, and one that is not is refused', async () => {
  const user = userEvent.setup();
  const { onApply, last } = draw();

  await user.upload(
    screen.getByLabelText(words.tools.fromFile),
    new File(['Day;From\r\nFriday;20:00z\r\n'], 'hours.csv', { type: 'text/csv' }),
  );
  await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(replacedLine(2, 1)));
  expect(last()?.rows).toEqual([{ cells: [{ text: both('', 'Friday') }, { text: both('', '20:00z') }] }]);

  onApply.mockClear();
  await user.upload(
    screen.getByLabelText(words.tools.fromFile),
    new File([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])], 'broken.xlsx'),
  );
  expect(await screen.findByRole('alert')).toHaveTextContent(words.tools.unreadable);
  expect(onApply).not.toHaveBeenCalled();
});

test('a CSV in UTF-8 without its mark keeps its accents, and a short date of Excel is written in the language on screen', async () => {
  const user = userEvent.setup();
  const { last } = draw(props, ['it']);

  // Google Sheets and LibreOffice write UTF-8 without the mark.
  await user.upload(
    screen.getByLabelText(words.tools.fromFile),
    new File([new TextEncoder().encode('Giorno;Ora\r\nLunedì;18:00\r\n')], 'orari.csv', { type: 'text/csv' }),
  );
  await waitFor(() =>
    expect(last()?.rows).toEqual([{ cells: [{ text: { it: 'Lunedì' } }, { text: { it: '18:00' } }] }]),
  );

  // 1 October 2026 in Excel's format 14, what an Italian Excel gives a date typed as 01/10/2026.
  const sheet = XLSX.utils.aoa_to_sheet([['Giorno'], [null]]);
  sheet.A2 = { t: 'n', v: 46296, z: 'm/d/yy' };
  sheet['!ref'] = 'A1:A2';
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'Orari');
  await user.upload(
    screen.getByLabelText(words.tools.fromFile),
    new File([XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer], 'orari.xlsx'),
  );
  await waitFor(() => expect(last()?.rows).toEqual([{ cells: [{ text: { it: '01/10/2026' } }] }]));
});

test('N × M makes an empty table, within the limits', async () => {
  const user = userEvent.setup();
  const { last } = draw();

  const rows = screen.getByLabelText(words.tools.rows);
  await user.clear(rows);
  await user.type(rows, '2');
  const columns = screen.getByLabelText(words.tools.columns);
  await user.clear(columns);
  await user.type(columns, '4');
  await user.click(screen.getByRole('button', { name: words.tools.make }));

  expect(last()?.columns).toHaveLength(4);
  expect(last()?.rows).toEqual(
    Array.from({ length: 2 }, () => ({ cells: Array(4).fill({ text: both('', '') }) })),
  );

  await user.clear(columns);
  await user.type(columns, '21');
  expect(screen.getByRole('button', { name: words.tools.make })).toBeDisabled();
});

test('the grid writes a cell, a heading, a row and a column, all at once when applied', async () => {
  const user = userEvent.setup();
  const { onApply, last } = draw();

  await user.click(screen.getByRole('button', { name: words.grid.open }));
  const dialog = screen.getByRole('dialog', { name: words.grid.title });

  const cell = within(dialog).getByLabelText(say(words.grid.cell, { row: 1, column: 2 }));
  expect(cell).toHaveValue('18:00z');
  await user.clear(cell);
  await user.type(cell, '19:00z');
  await user.clear(within(dialog).getByLabelText(say(words.grid.heading, { column: 1 })));
  await user.type(within(dialog).getByLabelText(say(words.grid.heading, { column: 1 })), 'Weekday');
  await user.click(within(dialog).getByRole('button', { name: words.grid.addRow }));
  await user.click(within(dialog).getByRole('button', { name: words.grid.addColumn }));
  await user.click(within(dialog).getByRole('button', { name: say(words.grid.removeColumn, { column: 2 }) }));
  expect(onApply).not.toHaveBeenCalled();

  await user.click(within(dialog).getByRole('button', { name: words.grid.apply }));

  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(onApply).toHaveBeenCalledTimes(1);
  expect(last()).toEqual({
    ...props,
    columns: [
      { label: both('Giorno', 'Weekday'), align: 'left' },
      { label: both('', ''), align: 'left' },
    ],
    rows: [
      { cells: [{ text: both('Lunedì', 'Monday') }, { text: both('', '') }] },
      { cells: [{ text: both('', '') }, { text: both('', '') }] },
    ],
  });
});

test('the grid has a tab per language, and a dialog closed without applying changes nothing', async () => {
  const user = userEvent.setup();
  const { onApply } = draw();

  await user.click(screen.getByRole('button', { name: words.grid.open }));
  const dialog = screen.getByRole('dialog', { name: words.grid.title });
  await user.click(within(dialog).getByRole('tab', { name: /Italian/ }));
  expect(within(dialog).getByLabelText(say(words.grid.cell, { row: 1, column: 1 }))).toHaveValue('Lunedì');
  await user.type(within(dialog).getByLabelText(say(words.grid.cell, { row: 1, column: 1 })), '!');

  await user.click(within(dialog).getByRole('button', { name: englishCommon.common.cancel }));
  expect(onApply).not.toHaveBeenCalled();

  // Opened again, it starts from the block, not from what was left in it.
  await user.click(screen.getByRole('button', { name: words.grid.open }));
  await user.click(within(screen.getByRole('dialog')).getByRole('tab', { name: /Italian/ }));
  expect(screen.getByLabelText(say(words.grid.cell, { row: 1, column: 1 }))).toHaveValue('Lunedì');
});

test('a grid without columns cannot be applied: its rows would be blank rows', async () => {
  const user = userEvent.setup();
  draw();

  await user.click(screen.getByRole('button', { name: words.grid.open }));
  const dialog = screen.getByRole('dialog', { name: words.grid.title });
  await user.click(within(dialog).getByRole('button', { name: say(words.grid.removeColumn, { column: 2 }) }));
  await user.click(within(dialog).getByRole('button', { name: say(words.grid.removeColumn, { column: 1 }) }));

  expect(within(dialog).getByRole('button', { name: words.grid.apply })).toBeDisabled();
});

test('an undo of headings written in the grid draws the form again, and the form does not write them back', async () => {
  const user = userEvent.setup();
  const { last } = draw();

  // Only the headings change: the rows stay what they were.
  await user.click(screen.getByRole('button', { name: words.grid.open }));
  const dialog = screen.getByRole('dialog', { name: words.grid.title });
  const heading = within(dialog).getByLabelText(say(words.grid.heading, { column: 1 }));
  await user.clear(heading);
  await user.type(heading, 'Weekday');
  await user.click(within(dialog).getByRole('button', { name: words.grid.apply }));
  expect(last()?.columns).toEqual([{ label: both('Giorno', 'Weekday'), align: 'left' }, props.columns[1]]);

  await user.click(screen.getByRole('button', { name: 'Undo' }));
  await user.type(screen.getByDisplayValue('Hours'), '!');

  await waitFor(() => expect(last()?.caption).toEqual(both('Orari', 'Hours!')));
  expect(last()?.columns).toEqual(props.columns);
  expect(last()?.rows).toEqual(props.rows);
});

// The columns half of the form's mark: the headings the grid applies draw the form again, or its next keystroke would
// write the old ones back over them (the reviewer's recheck of the fixes).
test('headings applied from the grid survive a keystroke in the form', async () => {
  const user = userEvent.setup();
  const { last } = draw();

  await user.click(screen.getByRole('button', { name: words.grid.open }));
  const dialog = screen.getByRole('dialog', { name: words.grid.title });
  const heading = within(dialog).getByLabelText(say(words.grid.heading, { column: 1 }));
  await user.clear(heading);
  await user.type(heading, 'Weekday');
  await user.click(within(dialog).getByRole('button', { name: words.grid.apply }));
  await user.type(screen.getByDisplayValue('Hours'), '!');

  await waitFor(() => expect(last()?.caption).toEqual(both('Orari', 'Hours!')));
  expect(last()?.columns).toEqual([{ label: both('Giorno', 'Weekday'), align: 'left' }, props.columns[1]]);
});

test('after a tool writes, the form writes what the tool wrote and not the table it held before', async () => {
  const user = userEvent.setup();
  const { last } = draw();
  clipboardHolds(() => Promise.resolve('New\nrow\n'));

  await user.click(screen.getByRole('button', { name: words.tools.paste }));
  // The caption, in the form, open on the language on screen.
  const caption = screen.getByDisplayValue('Hours');
  await user.type(caption, '!');

  // The form applies a moment after the last keystroke.
  await waitFor(() => expect(last()?.caption).toEqual(both('Orari', 'Hours!')));
  expect(last()?.columns).toEqual([{ label: both('', 'New'), align: 'left' }]);
  expect(last()?.rows).toEqual([{ cells: [{ text: both('', 'row') }] }]);
});

test('the form draws no rows, which are the grid’s, and offers the style of the rows', () => {
  draw();

  expect(screen.queryByText(words.fields['rows.cells'])).not.toBeInTheDocument();
  expect(screen.getByText(words.fields.rowStyle)).toBeInTheDocument();
});

test('the rows are told apart by their style, and a table written before PR 3.12 keeps its lines', () => {
  expect(tableSchema.parse(props)).toEqual({ ...props, rowStyle: 'lines' });

  const { unmount } = renderWithProviders(<TableBlock props={props} />);
  expect(screen.getAllByRole('row')[1]).toHaveClass('border-b');
  expect(screen.getAllByRole('row')[1]).not.toHaveClass('border-b-0');
  unmount();

  renderWithProviders(<TableBlock props={{ ...props, rowStyle: 'striped' }} />);
  expect(screen.getAllByRole('row')[1]).toHaveClass('border-b-0', 'even:bg-current/5');
});
