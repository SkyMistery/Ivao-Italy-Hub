import {
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  type ReactNode,
} from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

/**
 * The two thin borders between the three columns of the editor, and the columns' widths (design of phase 3, §4.2).
 *
 * A piece of the editor and not a component of the closed list (§2 of the design), as `SectionFrame` and `DropZone` are.
 * The side columns move only when somebody drags them: changing how the page is looked at still moves neither
 * (`docs/UI-GUIDELINES.md`, "The editor of a page").
 *
 * ⚠️ `PanelColumns` holds the widths, and not `BodyEditor`, for a reason a drag makes plain: the columns come in already
 * drawn, so a move of the pointer draws this grid and leaves the whole page and its panels alone.
 */

type Panel = 'palette' | 'properties';
type Widths = Record<Panel, number>;

/** In rem. `start` is where a double click puts a panel back. */
const LIMITS: Record<Panel, { min: number; max: number; start: number }> = {
  palette: { min: 10, max: 20, start: 13 },
  properties: { min: 16, max: 36, start: 19 },
};

/** ⚠️ Said once more in the class of the grid below, where the browser keeps it: the two have to agree. */
const PAGE_MIN = 24;

const KEY_STEP = 1;

const STORAGE_KEY = 'hub.content.editor.panels';

const within = (panel: Panel, width: number) =>
  Math.min(LIMITS[panel].max, Math.max(LIMITS[panel].min, width));

/** A tenth of a rem is under two pixels, and what a screen reader reads and the browser keeps stays a short number. */
const tidy = (width: number) => Math.round(width * 10) / 10;

const storedWidths = z.object({ palette: z.number(), properties: z.number() });

/**
 * ⚠️ `localStorage`, and the first use of it in the hub for a comfort of whoever is looking (design §4.2): not a thing to
 * keep on the server, and it may be missing, refused or full, so it is always asked inside a `try`. Without it the panels
 * simply last as long as the editor does. The only other reader is what an import left to do (`claudeFile.ts`).
 */
function readWidths(): Widths {
  try {
    const stored = storedWidths.safeParse(JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null'));
    if (stored.success) {
      return {
        palette: within('palette', stored.data.palette),
        properties: within('properties', stored.data.properties),
      };
    }
  } catch {
    // Nothing to read: the widths everybody starts from.
  }

  return { palette: LIMITS.palette.start, properties: LIMITS.properties.start };
}

function rememberWidths(widths: Widths) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(widths));
  } catch {
    // No storage: see `readWidths`.
  }
}

function remInPixels(): number {
  const size = parseFloat(getComputedStyle(document.documentElement).fontSize);
  return Number.isFinite(size) && size > 0 ? size : 16;
}

/**
 * What the grid really draws, in rem, which is not always what was asked for: a window narrowed since, or the sidebar
 * opened again, leaves the side columns squeezed so that the page keeps its minimum. `null` where the columns are not
 * side by side, or where nothing is laid out at all (a test).
 */
function drawnColumns(grid: HTMLElement | null): (Widths & { page: number }) | null {
  if (grid === null) {
    return null;
  }

  const rem = remInPixels();
  const [palette, , page, , properties] = getComputedStyle(grid)
    .gridTemplateColumns.split(' ')
    .map((track) => parseFloat(track) / rem);

  return palette === undefined ||
    page === undefined ||
    properties === undefined ||
    Number.isNaN(palette + page + properties)
    ? null
    : { palette, page, properties };
}

/**
 * The three columns, side by side from `xl` up and one under the other below it, where there is nothing to drag.
 *
 * ⚠️ The minimum of the page is the **grid's** to keep, not a sum done here. A side column is `minmax(its minimum, its
 * width)` and the page `minmax(24rem, 1fr)`, so a track gives way before the page does whatever happens to the room:
 * no listener on the window, and no frame in which the page is under its minimum. The arithmetic below is only so that a
 * drag stops where the page would start pushing back, instead of the pointer running on ahead of a line that has stopped.
 */
export function PanelColumns({
  palette,
  page,
  properties,
}: {
  palette: ReactNode;
  page: ReactNode;
  properties: ReactNode;
}) {
  const { t } = useTranslation();
  const grid = useRef<HTMLDivElement>(null);
  const [widths, setWidths] = useState(readWidths);

  /** Moved as far as the page leaves room for. Starts from what is drawn, so a squeezed column does not jump. */
  const move = (panel: Panel, target: number): Widths => {
    const drawn = drawnColumns(grid.current);
    const room = drawn === null ? LIMITS[panel].max : drawn[panel] + drawn.page - PAGE_MIN;
    const moved = tidy(within(panel, Math.min(room, target)));

    const next =
      drawn === null
        ? { ...widths, [panel]: moved }
        : { palette: tidy(drawn.palette), properties: tidy(drawn.properties), [panel]: moved };

    setWidths(next);
    return next;
  };

  const track = (panel: Panel) => `minmax(${LIMITS[panel].min}rem,${widths[panel]}rem)`;

  const splitter = (panel: Panel, grows: 1 | -1) => (
    <PanelSplitter
      label={t(`content.editor.panelWidth.${panel}`)}
      hint={t('content.editor.panelWidth.hint')}
      value={widths[panel]}
      min={LIMITS[panel].min}
      max={LIMITS[panel].max}
      grows={grows}
      drawn={() => drawnColumns(grid.current)?.[panel] ?? widths[panel]}
      // Asked with the value it already has: a squeezed column says what it really is once somebody lands on the line.
      onFocus={() => move(panel, widths[panel])}
      onDrag={(target) => move(panel, target)}
      onDrop={() => rememberWidths(widths)}
      onStep={(target) => rememberWidths(move(panel, target))}
      onReset={() => {
        const next = { ...widths, [panel]: LIMITS[panel].start };
        setWidths(next);
        rememberWidths(next);
      }}
    />
  );

  return (
    <div
      ref={grid}
      style={
        { '--editor-palette': track('palette'), '--editor-properties': track('properties') } as CSSProperties
      }
      className="grid grid-cols-1 max-xl:gap-6 xl:grid-cols-[var(--editor-palette)_1.5rem_minmax(24rem,1fr)_1.5rem_var(--editor-properties)]"
    >
      {palette}
      {splitter('palette', 1)}
      {page}
      {splitter('properties', -1)}
      {properties}
    </div>
  );
}

/**
 * One of the two lines: a real separator, for a mouse, a finger and a keyboard. The value is the width of the panel it
 * belongs to, which grows to the right of the first line and to the left of the second, and that is `grows`.
 */
function PanelSplitter({
  label,
  hint,
  value,
  min,
  max,
  grows,
  drawn,
  onFocus,
  onDrag,
  onDrop,
  onStep,
  onReset,
}: {
  label: string;
  hint: string;
  value: number;
  min: number;
  max: number;
  grows: 1 | -1;
  /** How wide the panel is on the screen right now, which is where a drag starts from. */
  drawn: () => number;
  onFocus: () => void;
  onDrag: (width: number) => void;
  onDrop: () => void;
  onStep: (width: number) => void;
  onReset: () => void;
}) {
  const dragging = useRef<{ x: number; width: number; rem: number } | null>(null);

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) {
      return;
    }

    // Captured, so a pointer that runs ahead of the line, over a frame or out of the window, still belongs to the drag.
    event.currentTarget.setPointerCapture(event.pointerId);
    dragging.current = { x: event.clientX, width: drawn(), rem: remInPixels() };
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (dragging.current !== null) {
      const { x, width, rem } = dragging.current;
      onDrag(width + (grows * (event.clientX - x)) / rem);
    }
  };

  const endDrag = () => {
    if (dragging.current !== null) {
      dragging.current = null;
      onDrop();
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    // The arrow says where the line goes, as under a pointer; the value follows from which panel it belongs to.
    const direction = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
    if (direction !== 0) {
      event.preventDefault();
      onStep(drawn() + grows * direction * KEY_STEP);
    }
  };

  return (
    <div
      role="separator"
      tabIndex={0}
      aria-orientation="vertical"
      aria-label={label}
      aria-valuenow={value}
      aria-valuemin={min}
      aria-valuemax={max}
      title={hint}
      onFocus={onFocus}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      onKeyDown={onKeyDown}
      onDoubleClick={onReset}
      // The width of the gap it stands in, so a thin line is not a thin target. Gone below `xl`, where the columns are one
      // under the other and there is nothing between them to move, and off the tab order with it.
      // ⚠️ `max-xl:hidden` and never `hidden xl:flex`, and `max-xl:gap-6` on the grid for the same reason: Atmosphere's
      // stylesheet comes after Tailwind's utilities and says `.hidden` and the gaps again, so the plain class wins over the
      // one in the media query. Both were tried, and only a browser could tell.
      className="group flex cursor-col-resize touch-none justify-center outline-none select-none max-xl:hidden"
    >
      <span
        aria-hidden
        className="bg-border group-hover:bg-fuselage-400 group-focus-visible:bg-atmos-700 dark:group-focus-visible:bg-ocean-300 w-px transition-colors group-focus-visible:w-0.5"
      />
    </div>
  );
}
