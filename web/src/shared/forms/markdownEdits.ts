import type { TextColour } from '../ui/textColours';

/**
 * What the formatting bar does to a text (design of the editor, phase 3, §4.6): it writes Markdown around the selection, and
 * whoever writes keeps seeing the marks. Every function takes the text and its selection and answers with the same, so all
 * of it is read without a browser.
 */
export interface TextState {
  readonly value: string;
  readonly start: number;
  readonly end: number;
}

export type InlineMark = '**' | '*' | '~~';

interface Span {
  readonly start: number;
  readonly end: number;
}

/** What opens a line and is not its text: `MarkdownContent` would not read a mark put in front of it. */
const LINE_OPENING = /^[ \t]*(?:[-+*][ \t]+|\d+\.[ \t]+|#{1,6}[ \t]+|>[ \t]*)*/;

/**
 * The selection, line by line, without the spaces around it and without what opens a line. Emphasis does not cross a
 * paragraph, and `**word **` is not bold, so a mark goes on each of these and not around the whole.
 */
function linesOfTheSelection({ value, start, end }: TextState): Span[] {
  const spans: Span[] = [];
  let offset = start;

  for (const line of value.slice(start, end).split('\n')) {
    const atLineStart = offset === 0 || value[offset - 1] === '\n';
    const opening = atLineStart ? (LINE_OPENING.exec(line)?.[0].length ?? 0) : 0;
    const text = line.slice(opening);
    const from = offset + opening + (text.length - text.trimStart().length);
    const to = offset + opening + text.trimEnd().length;

    if (to > from) {
      spans.push({ start: from, end: to });
    }

    offset += line.length + 1;
  }

  return spans;
}

const runBefore = (value: string, at: number, mark: string) => {
  let length = 0;
  while (value[at - length - 1] === mark) {
    length += 1;
  }
  return length;
};

const runAfter = (value: string, at: number, mark: string) => {
  let length = 0;
  while (value[at + length] === mark) {
    length += 1;
  }
  return length;
};

/** Three asterisks are bold and italic together: one more is not italic, and two more is not bold. */
const holds = (mark: InlineMark, around: number) =>
  mark === '*' ? around === 1 || around >= 3 : around >= mark.length;

/** A selection that took its marks with it reads as one that left them out. */
function withoutItsMarks(value: string, span: Span, char: string): Span {
  const lead = runAfter(value, span.start, char);
  const trail = runBefore(value, span.end, char);
  const both = Math.min(lead, trail);
  const inner = value.slice(span.start + both, span.end - both);

  return both > 0 && inner !== '' && !inner.includes(char)
    ? { start: span.start + both, end: span.end - both }
    : span;
}

/** Bold, italic or struck: on when any line of the selection lacks it, and off when all of them have it. */
export function toggleMark(state: TextState, mark: InlineMark): TextState {
  const { value, start, end } = state;
  const char = mark.charAt(0);

  if (start === end) {
    const around = Math.min(runBefore(value, start, char), runAfter(value, start, char));
    return holds(mark, around)
      ? {
          value: value.slice(0, start - mark.length) + value.slice(start + mark.length),
          start: start - mark.length,
          end: start - mark.length,
        }
      : {
          value: value.slice(0, start) + mark + mark + value.slice(start),
          start: start + mark.length,
          end: start + mark.length,
        };
  }

  const spans = linesOfTheSelection(state).map((span) => withoutItsMarks(value, span, char));
  if (spans.length === 0) {
    return state;
  }

  const held = (span: Span) =>
    holds(mark, Math.min(runBefore(value, span.start, char), runAfter(value, span.end, char)));
  const removing = spans.every(held);

  return rewriting(value, spans, (span) => {
    if (removing) {
      return { from: span.start - mark.length, to: span.end + mark.length, open: '', close: '' };
    }
    return held(span)
      ? { from: span.start, to: span.end, open: '', close: '' }
      : { from: span.start, to: span.end, open: mark, close: mark };
  });
}

/**
 * The text with each span rewritten: `from`…`to` replaced by the span's own words between `open` and `close`. The selection
 * answered goes from the words of the first to the words of the last, so a second click reads what the first wrote.
 */
function rewriting(
  value: string,
  spans: readonly Span[],
  rewrite: (span: Span) => { from: number; to: number; open: string; close: string; words?: string },
): TextState {
  let written = '';
  let cursor = 0;
  let start = -1;
  let end = -1;

  for (const span of spans) {
    const { from, to, open, close, words } = rewrite(span);
    written += value.slice(cursor, from) + open;
    if (start === -1) {
      start = written.length;
    }
    written += words ?? value.slice(span.start, span.end);
    end = written.length;
    written += close;
    cursor = to;
  }

  return { value: written + value.slice(cursor), start, end };
}

const LIST_LINE = /^([ \t]*)([-+*]|\d+\.)([ \t]+)/;
const ORDERED = /^\d+\.$/;

interface WholeLines {
  readonly lines: string[];
  /** The first line the selection touches, and the last. */
  readonly first: number;
  readonly last: number;
}

function wholeLines({ value, start, end }: TextState): WholeLines {
  const lines = value.split('\n');
  const lineAt = (offset: number) => value.slice(0, offset).split('\n').length - 1;
  const first = lineAt(start);
  // A selection that ends where a line starts does not touch that line: it is what a triple click gives.
  const last = Math.max(first, end > start && value[end - 1] === '\n' ? lineAt(end) - 1 : lineAt(end));

  return { lines, first, last };
}

/** The list around the lines touched, numbered from one at each depth: an item moved keeps its list in order. */
function renumber(lines: string[], first: number, last: number) {
  let top = first;
  while (top > 0 && (lines[top - 1] ?? '').trim() !== '') {
    top -= 1;
  }

  let bottom = last;
  while (bottom < lines.length - 1 && (lines[bottom + 1] ?? '').trim() !== '') {
    bottom += 1;
  }

  const counters = new Map<number, number>();

  for (let at = top; at <= bottom; at++) {
    const match = LIST_LINE.exec(lines[at] ?? '');
    if (match === null) {
      continue;
    }

    const [whole, indent = '', marker = '', gap = ''] = match;
    for (const depth of [...counters.keys()]) {
      if (depth > indent.length) {
        counters.delete(depth);
      }
    }

    if (ORDERED.test(marker)) {
      const number = (counters.get(indent.length) ?? 0) + 1;
      counters.set(indent.length, number);
      lines[at] = `${indent}${number}.${gap}${(lines[at] ?? '').slice(whole.length)}`;
    } else {
      counters.delete(indent.length);
    }
  }
}

/** The lines back into a text, with the lines touched selected; a caret stays a caret, at the end of its line. */
function fromLines({ lines, first, last }: WholeLines, caret: boolean): TextState {
  const value = lines.join('\n');
  const offsetOf = (line: number) => lines.slice(0, line).reduce((sum, text) => sum + text.length + 1, 0);
  const end = offsetOf(last) + (lines[last] ?? '').length;

  return { value, start: caret ? end : offsetOf(first), end };
}

/** A bulleted or numbered list out of the lines touched, or the plain lines back when they already are that list. */
export function toggleList(state: TextState, kind: 'bullets' | 'numbers'): TextState {
  const whole = wholeLines(state);
  const { lines, first, last } = whole;

  const touched = lines.slice(first, last + 1);
  const written = touched.filter((line) => line.trim() !== '');
  const isThatList = (line: string) => {
    const marker = LIST_LINE.exec(line)?.[2];
    return marker !== undefined && ORDERED.test(marker) === (kind === 'numbers');
  };
  const removing = written.length > 0 && written.every(isThatList);

  touched.forEach((line, index) => {
    if (line.trim() === '' && written.length > 0) {
      return;
    }

    const match = LIST_LINE.exec(line);
    const indent = match?.[1] ?? '';
    const text = match === null ? line.trimStart() : line.slice(match[0].length);
    lines[first + index] = removing
      ? `${indent}${text}`
      : `${indent}${kind === 'numbers' ? '1.' : '-'} ${text}`;
  });

  renumber(lines, first, last);
  return fromLines(whole, state.start === state.end);
}

/** How far a line of a list is from the left, and how wide its marker is: what a line nested under it has to clear. */
function shapeOf(line: string | undefined) {
  const match = LIST_LINE.exec(line ?? '');
  return match === null
    ? null
    : { indent: (match[1] ?? '').length, width: (match[2] ?? '').length + (match[3] ?? '').length };
}

/**
 * One step in or out of the list above, which is how a list gets nested. The first line touched decides how far, because
 * the distance is not a constant: under `- ` a nested item starts two spaces in, under `1. ` three. The first item of a list
 * has nothing to go under, and stays.
 */
export function moveListLines(state: TextState, direction: 'in' | 'out'): TextState {
  const whole = wholeLines(state);
  const { lines, first, last } = whole;

  const own = shapeOf(lines[first]);
  if (own === null) {
    return state;
  }

  let distance = 0;

  for (let at = first - 1; at >= 0 && (lines[at] ?? '').trim() !== ''; at--) {
    const above = shapeOf(lines[at]);
    if (above === null) {
      continue;
    }

    if (direction === 'in' && above.indent <= own.indent) {
      distance = above.indent + above.width - own.indent;
      break;
    }

    if (direction === 'out' && above.indent < own.indent) {
      distance = own.indent - above.indent;
      break;
    }
  }

  // Nothing above it is shallower: it goes all the way out.
  if (direction === 'out' && distance === 0) {
    distance = own.indent;
  }

  if (distance === 0) {
    return state;
  }

  for (let at = first; at <= last; at++) {
    const line = lines[at] ?? '';
    if (LIST_LINE.test(line)) {
      const removable = line.length - line.trimStart().length;
      lines[at] =
        direction === 'in' ? `${' '.repeat(distance)}${line}` : line.slice(Math.min(distance, removable));
    }
  }

  renumber(lines, first, last);
  return fromLines(whole, state.start === state.end);
}

/** The piece of the line the selection stands in that matches `pattern`, when the selection is inside it. */
function aroundTheSelection({ value, start, end }: TextState, pattern: RegExp) {
  const lineStart = value.lastIndexOf('\n', start - 1) + 1;
  const newline = value.indexOf('\n', end);
  const line = value.slice(lineStart, newline === -1 ? value.length : newline);

  for (const match of line.matchAll(pattern)) {
    const from = lineStart + match.index;
    const to = from + match[0].length;
    if (from <= start && end <= to) {
      return { from, to, match };
    }
  }

  return null;
}

const LINK = /!?\[([^\]\n]*)\]\(([^)\n]*)\)/g;

/** The address of the link the selection stands in, for the bar to start from; empty when there is none. */
export function linkAddressAt(state: TextState): string {
  return aroundTheSelection(state, LINK)?.match[2] ?? '';
}

/**
 * An address as Markdown can hold it. A space or a closing bracket would end a link and leave its marks on the page, so
 * they go in the only form a link can carry them.
 */
const asDestination = (address: string) =>
  address.trim().replaceAll(' ', '%20').replaceAll('(', '%28').replaceAll(')', '%29');

/**
 * The selection as a link to `address`. A link the selection stands in takes the new address, and with no address it goes
 * back to its words. With nothing selected the words are `label`, the name of the page chosen, or else the address itself.
 */
export function writeLink(state: TextState, address: string, label?: string): TextState {
  const { value, start, end } = state;
  const existing = aroundTheSelection(state, LINK);
  const from = existing?.from ?? start;
  const to = existing?.to ?? end;
  const selected = existing?.match[1] ?? value.slice(start, end).trim();
  const destination = asDestination(address);

  if (destination === '') {
    return existing === null
      ? state
      : {
          value: value.slice(0, from) + selected + value.slice(to),
          start: from,
          end: from + selected.length,
        };
  }

  const words = selected === '' ? (label ?? destination) : selected;
  const written = `[${words}](${destination})`;

  return { value: value.slice(0, from) + written + value.slice(to), start: from, end: from + written.length };
}

const COLOURED = /:([a-z]+)\[((?:[^[\]\n]|\[[^\]\n]*\])*)\]/g;

const withoutColours = (text: string) => text.replaceAll(COLOURED, '$2');

/**
 * The selection in a colour of the set, or out of any with `null`. A colour the selection stands in changes or goes as a
 * whole: there is no colour inside a colour, and a colour already inside what is selected gives way to the new one.
 */
export function writeColour(state: TextState, colour: TextColour | null): TextState {
  const { value, start, end } = state;

  const around = aroundTheSelection(state, COLOURED);
  if (around !== null) {
    const words = around.match[2] ?? '';
    const open = colour === null ? '' : `:${colour}[`;
    const close = colour === null ? '' : ']';
    return {
      value: value.slice(0, around.from) + open + words + close + value.slice(around.to),
      start: around.from + open.length,
      end: around.from + open.length + words.length,
    };
  }

  if (start === end) {
    return colour === null
      ? state
      : {
          value: `${value.slice(0, start)}:${colour}[]${value.slice(start)}`,
          start: start + colour.length + 2,
          end: start + colour.length + 2,
        };
  }

  const spans = linesOfTheSelection(state);
  if (spans.length === 0) {
    return state;
  }

  return rewriting(value, spans, (span) => ({
    from: span.start,
    to: span.end,
    open: colour === null ? '' : `:${colour}[`,
    close: colour === null ? '' : ']',
    words: withoutColours(value.slice(span.start, span.end)),
  }));
}
