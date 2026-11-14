import { Button, Checkbox, DialogClose, Input, Label } from '@ivao/atmosphere-react';
import { Plus, Trash2 } from 'lucide-react';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { LocaleTabs } from '../../shared/forms';
import { SHEET_FILE_TYPES, readWorkbook, rowsOfPastedText } from '../../shared/files/sheet';
import { useLocaleOnScreen } from '../../shared/i18n/previewLocale';
import { Dialog } from '../../shared/ui';

import {
  TABLE_LIMITS,
  emptyTable,
  tableFromSheet,
  tableOf,
  withCell,
  withColumnAdded,
  withLabel,
  withRowAdded,
  withTable,
  withoutColumn,
  withoutRow,
  writtenIn,
  type SheetTable,
  type TableShape,
} from './tableGrid';

/**
 * What the tools last said: a refusal, or what a table they wrote now holds — said only while the block holds that table
 * (`table`, its mark), so that an undo takes the line away with the table it spoke of.
 */
type Said = { tone: 'refused'; text: string } | { tone: 'done'; text: string; table: string } | null;

/** The mark of the table a block holds: its columns and rows, which are what the tools write. */
function tableMark(props: Record<string, unknown>): string {
  return JSON.stringify([props.columns, props.rows]) ?? '';
}

/**
 * The table's own tools in the properties panel (design of the editor's phase 3, §5.1, PR 3.12): paste from a
 * spreadsheet, import a file, make an empty table of N × M, and write it in a grid. The declared exception of
 * `BlockRegistration.editedAsGrid`: each of them writes the block's `props` — columns and rows, the rest kept — through
 * `onApply`, which is the panel's `onApplyProps`, and the generated form under them takes what they wrote.
 *
 * What is pasted or imported is written in the language on screen, the others left to write (design §5.1): the tab the
 * translated fields open on. Each replaces the table; undo takes it back, and the line that says what happened says so.
 */
export function TableTools({
  props,
  locales,
  onApply,
}: {
  props: Record<string, unknown>;
  locales: readonly string[];
  onApply: (props: Record<string, unknown>) => void;
}) {
  const { t, i18n } = useTranslation();
  const id = useId();
  const locale = useLocaleOnScreen(locales) ?? i18n.language;
  const language = new Intl.DisplayNames([i18n.language], { type: 'language' }).of(locale) ?? locale;

  const [header, setHeader] = useState(true);
  const [size, setSize] = useState({ rows: '3', columns: '3' });
  const [said, setSaid] = useState<Said>(null);

  const replaceWith = (table: TableShape) => {
    const written = withTable(props, table);
    onApply(written);
    setSaid({
      tone: 'done',
      text: t('blocks.table.tools.replaced', {
        columns: t('blocks.table.tools.columnCount', { count: table.columns.length }),
        rows: t('blocks.table.tools.rowCount', { count: table.rows.length }),
      }),
      table: tableMark(written),
    });
  };

  const take = (made: SheetTable) => {
    if (made.kind === 'table') {
      replaceWith(made.table);
    } else if (made.kind === 'empty') {
      setSaid({ tone: 'refused', text: t('blocks.table.tools.empty') });
    } else {
      setSaid({
        tone: 'refused',
        text: t('blocks.table.tools.tooLarge', {
          rows: made.rows,
          columns: made.columns,
          maxRows: TABLE_LIMITS.rows,
          maxColumns: TABLE_LIMITS.columns,
        }),
      });
    }
  };

  const paste = async () => {
    let text: string;
    try {
      text = await navigator.clipboard.readText();
    } catch {
      // Refused by the person, by the browser, or not offered at all (a page that is not served over HTTPS).
      setSaid({ tone: 'refused', text: t('blocks.table.tools.clipboard') });
      return;
    }

    take(tableFromSheet(rowsOfPastedText(text), { header, locale, locales }));
  };

  const importFile = async (file: File) => {
    let made: SheetTable = { kind: 'empty' };
    try {
      // A short date of Excel is written in the language the table is written in.
      const workbook = await readWorkbook(file, { locale });
      if (workbook === null) {
        setSaid({ tone: 'refused', text: t('blocks.table.tools.unreadable') });
        return;
      }

      // The first sheet that holds something: a workbook may open on an empty one. The others are not read.
      for (const name of workbook.sheets) {
        made = tableFromSheet(workbook.cells(name, { formatted: true }), { header, locale, locales });
        if (made.kind !== 'empty') {
          break;
        }
      }
    } catch {
      // A sheet read but not made rows: a file that cannot be read, said, not a promise rejected with nothing on screen.
      setSaid({ tone: 'refused', text: t('blocks.table.tools.unreadable') });
      return;
    }

    take(made);
  };

  const rows = Number(size.rows);
  const columns = Number(size.columns);
  const sizeIsRight =
    Number.isInteger(rows) &&
    Number.isInteger(columns) &&
    rows >= 1 &&
    columns >= 1 &&
    rows <= TABLE_LIMITS.rows &&
    columns <= TABLE_LIMITS.columns;

  return (
    <div className="flex flex-col gap-3">
      <TableGrid props={props} locales={locales} onApply={onApply} />

      <div className="flex items-center gap-2">
        <Checkbox
          id={`${id}-header`}
          checked={header}
          onCheckedChange={(ticked) => setHeader(ticked === true)}
        />
        <Label htmlFor={`${id}-header`}>{t('blocks.table.tools.header')}</Label>
      </div>

      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <Button type="button" variant="outline" onClick={() => void paste()}>
          {t('blocks.table.tools.paste')}
        </Button>
        {/* The browser's own file control, as the interactive block's source has it: the one a keyboard and a screen
            reader already know. */}
        <label className="text-primary w-fit cursor-pointer text-sm underline underline-offset-2">
          {t('blocks.table.tools.fromFile')}
          <input
            type="file"
            accept={SHEET_FILE_TYPES}
            className="sr-only"
            onChange={(event) => {
              const file = event.target.files?.[0];
              // The same file chosen twice in a row fires no change unless the value is cleared.
              event.target.value = '';
              if (file !== undefined) {
                void importFile(file);
              }
            }}
          />
        </label>
      </div>
      <p className="text-muted-foreground text-sm">{t('blocks.table.tools.hint', { language })}</p>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium">{t('blocks.table.tools.create')}</legend>
        <div className="flex items-end gap-2">
          <div className="flex w-20 flex-col gap-1">
            <Label htmlFor={`${id}-rows`}>{t('blocks.table.tools.rows')}</Label>
            <Input
              id={`${id}-rows`}
              type="number"
              min={1}
              max={TABLE_LIMITS.rows}
              value={size.rows}
              onChange={(event) => setSize({ ...size, rows: event.target.value })}
            />
          </div>
          <span aria-hidden className="pb-2">
            ×
          </span>
          <div className="flex w-20 flex-col gap-1">
            <Label htmlFor={`${id}-columns`}>{t('blocks.table.tools.columns')}</Label>
            <Input
              id={`${id}-columns`}
              type="number"
              min={1}
              max={TABLE_LIMITS.columns}
              value={size.columns}
              onChange={(event) => setSize({ ...size, columns: event.target.value })}
            />
          </div>
          <Button
            type="button"
            variant="outline"
            disabled={!sizeIsRight}
            onClick={() => replaceWith(emptyTable(rows, columns, locales))}
          >
            {t('blocks.table.tools.make')}
          </Button>
        </div>
      </fieldset>

      {/* Mounted before it says anything, and never hidden: a live region is announced when its text changes, not when it
          appears with it. Empty, it takes back the gap above it. A refusal is an alert, announced as it appears. */}
      <p role="status" className="text-muted-foreground text-sm empty:-mt-3">
        {said?.tone === 'done' && said.table === tableMark(props) ? said.text : null}
      </p>
      {said?.tone === 'refused' ? (
        <p role="alert" className="text-destructive text-sm">
          {said.text}
        </p>
      ) : null}
    </div>
  );
}

/**
 * «Edit in the grid»: the table in a wide dialog, a box per cell and per heading, a tab per language (Carmine's answer 1
 * of 31 October 2026). It works on a copy, and writes it once with Apply: one step to undo, and a dialog closed with its
 * «X» or Escape changes nothing.
 */
function TableGrid({
  props,
  locales,
  onApply,
}: {
  props: Record<string, unknown>;
  locales: readonly string[];
  onApply: (props: Record<string, unknown>) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<TableShape>(() => tableOf(props, locales));

  return (
    <Dialog
      open={open}
      onOpenChange={(opening) => {
        // Every opening starts from what the block holds now: an undo, a paste, the form may have changed it.
        if (opening) {
          setDraft(tableOf(props, locales));
        }
        setOpen(opening);
      }}
      size="wide"
      trigger={t('blocks.table.grid.open')}
      title={t('blocks.table.grid.title')}
      description={t('blocks.table.grid.description')}
      footer={
        <>
          <DialogClose asChild>
            <Button type="button" variant="outline">
              {t('common.cancel')}
            </Button>
          </DialogClose>
          {/* A table without columns has rows of nothing, which the check before publishing would let through. */}
          <Button
            type="button"
            disabled={draft.columns.length === 0}
            onClick={() => {
              onApply(withTable(props, draft));
              setOpen(false);
            }}
          >
            {t('blocks.table.grid.apply')}
          </Button>
        </>
      }
    >
      <LocaleTabs
        label={t('blocks.table.grid.cells')}
        locales={locales}
        isWritten={(locale) => writtenIn(draft, locale)}
        error={undefined}
        renderContent={(locale) => (
          <GridOf
            table={draft}
            locale={locale}
            onLabel={(column, text) => setDraft(withLabel(draft, column, locale, text))}
            onCell={(row, column, text) => setDraft(withCell(draft, row, column, locale, text))}
            onRemoveRow={(row) => setDraft(withoutRow(draft, row))}
            onRemoveColumn={(column) => setDraft(withoutColumn(draft, column))}
          />
        )}
      />
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="outline"
          disabled={draft.rows.length >= TABLE_LIMITS.rows || draft.columns.length === 0}
          onClick={() => setDraft(withRowAdded(draft, locales))}
        >
          <Plus aria-hidden className="mr-2 size-4" />
          {t('blocks.table.grid.addRow')}
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={draft.columns.length >= TABLE_LIMITS.columns}
          onClick={() => setDraft(withColumnAdded(draft, locales))}
        >
          <Plus aria-hidden className="mr-2 size-4" />
          {t('blocks.table.grid.addColumn')}
        </Button>
      </div>
    </Dialog>
  );
}

/** The boxes of one language: the headings on top, a row of boxes per row, and the ways to take a row or a column away. */
function GridOf({
  table,
  locale,
  onLabel,
  onCell,
  onRemoveRow,
  onRemoveColumn,
}: {
  table: TableShape;
  locale: string;
  onLabel: (column: number, text: string) => void;
  onCell: (row: number, column: number, text: string) => void;
  onRemoveRow: (row: number) => void;
  onRemoveColumn: (column: number) => void;
}) {
  const { t } = useTranslation();

  if (table.columns.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('blocks.table.grid.none')}</p>;
  }

  return (
    // The grid scrolls inside the dialog, both ways, so that the buttons under it stay on screen.
    <div className="scroll-thin max-h-[60vh] overflow-auto">
      <table className="border-separate border-spacing-1">
        <thead>
          {/* The way to take a column away, over it, so that a heading is as wide as the cells under it. */}
          <tr>
            {table.columns.map((_, index) => (
              <td key={index} className="text-center">
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t('blocks.table.grid.removeColumn', { column: index + 1 })}
                  onClick={() => onRemoveColumn(index)}
                >
                  <Trash2 aria-hidden className="size-4" />
                </Button>
              </td>
            ))}
            <td />
          </tr>
          <tr>
            {table.columns.map((column, index) => (
              <th key={index} scope="col" className="min-w-36 font-normal">
                <Input
                  aria-label={t('blocks.table.grid.heading', { column: index + 1 })}
                  className="font-semibold"
                  value={column.label[locale] ?? ''}
                  onChange={(event) => onLabel(index, event.target.value)}
                />
              </th>
            ))}
            <td />
          </tr>
        </thead>
        <tbody>
          {table.rows.map((cells, row) => (
            <tr key={row}>
              {cells.map((cell, column) => (
                <td key={column}>
                  <Input
                    aria-label={t('blocks.table.grid.cell', { row: row + 1, column: column + 1 })}
                    value={cell[locale] ?? ''}
                    onChange={(event) => onCell(row, column, event.target.value)}
                  />
                </td>
              ))}
              <td>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t('blocks.table.grid.removeRow', { row: row + 1 })}
                  onClick={() => onRemoveRow(row)}
                >
                  <Trash2 aria-hidden className="size-4" />
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
