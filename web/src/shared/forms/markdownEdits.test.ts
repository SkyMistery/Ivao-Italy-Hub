import { expect, test } from 'vitest';

import {
  linkAddressAt,
  moveListLines,
  toggleList,
  toggleMark,
  writeColour,
  writeLink,
  type TextState,
} from './markdownEdits';

/**
 * What the formatting bar writes (design of the editor, phase 3, §4.6). A text is written with its selection between two
 * pipes, `una |parola| qui`, or with one pipe for a caret: what comes back is read the same way.
 */
function state(written: string): TextState {
  const start = written.indexOf('|');
  const second = written.indexOf('|', start + 1);
  const end = second === -1 ? start : second - 1;
  return { value: written.replaceAll('|', ''), start, end };
}

function written({ value, start, end }: TextState): string {
  return start === end
    ? `${value.slice(0, start)}|${value.slice(start)}`
    : `${value.slice(0, start)}|${value.slice(start, end)}|${value.slice(end)}`;
}

test.each([
  ['una |parola| qui', '**', 'una **|parola|** qui'],
  ['una |parola| qui', '*', 'una *|parola|* qui'],
  ['una |parola| qui', '~~', 'una ~~|parola|~~ qui'],
  // Off again, whether the selection left its marks out or took them with it.
  ['una **|parola|** qui', '**', 'una |parola| qui'],
  ['una |**parola**| qui', '**', 'una |parola| qui'],
  ['una ~~|parola|~~ qui', '~~', 'una |parola| qui'],
  // Bold and italic are told apart by how many asterisks there are.
  ['una **|parola|** qui', '*', 'una ***|parola|*** qui'],
  ['una ***|parola|*** qui', '*', 'una **|parola|** qui'],
  ['una ***|parola|*** qui', '**', 'una *|parola|* qui'],
  ['una *|parola|* qui', '**', 'una ***|parola|*** qui'],
  // `**parola **` is not bold: the spaces stay out.
  ['una| parola |qui', '**', 'una **|parola|** qui'],
  // With nothing selected the marks wait for the words, and a second click takes them back.
  ['una | qui', '**', 'una **|** qui'],
  ['una **|** qui', '**', 'una | qui'],
] as const)('%s with %s', (before, mark, after) => {
  expect(written(toggleMark(state(before), mark))).toBe(after);
});

test('a mark goes on each line, because emphasis does not cross a paragraph, and after what opens a line', () => {
  expect(written(toggleMark(state('|uno\n\n- due\n## tre|'), '**'))).toBe(
    '**|uno**\n\n- **due**\n## **tre|**',
  );
  expect(toggleMark(state('|**uno**\n**due**|'), '**').value).toBe('uno\ndue');
  // One of them without it: the click gives it to that one, and the other is not marked twice.
  expect(toggleMark(state('|**uno**\ndue|'), '**').value).toBe('**uno**\n**due**');
});

test('a selection of nothing but spaces writes nothing', () => {
  expect(toggleMark(state('una| |qui'), '**').value).toBe('una qui');
});

test.each([
  ['uno|\ndue', 'bullets', '- uno|\ndue'],
  ['|uno\ndue|', 'bullets', '|- uno\n- due|'],
  ['|uno\ndue|', 'numbers', '|1. uno\n2. due|'],
  // The same list again goes back to plain lines; the other kind takes its place.
  ['|- uno\n- due|', 'bullets', '|uno\ndue|'],
  ['|1. uno\n2. due|', 'bullets', '|- uno\n- due|'],
  ['|- uno\n- due|', 'numbers', '|1. uno\n2. due|'],
  // An empty line between two is left empty.
  ['|uno\n\ndue|', 'bullets', '|- uno\n\n- due|'],
  // An empty line of its own is where the first item starts.
  ['|', 'bullets', '- |'],
  // A triple click selects the line's end too, and the next line is not touched.
  ['|uno\n|due', 'bullets', '|- uno|\ndue'],
] as const)('%s as %s', (before, kind, after) => {
  expect(written(toggleList(state(before), kind))).toBe(after);
});

test('a line added to a numbered list keeps the list in order', () => {
  expect(toggleList(state('1. uno\n|due'), 'numbers').value).toBe('1. uno\n2. due');
});

test.each([
  // As far in as the marker above is wide: two under a bullet, three under a number.
  ['- uno\n- |due', 'in', '- uno\n  - due|'],
  ['1. uno\n2. |due', 'in', '1. uno\n   1. due|'],
  // Out to the level above, and the numbers follow.
  ['- uno\n  - |due', 'out', '- uno\n- due|'],
  ['1. uno\n   1. |due', 'out', '1. uno\n2. due|'],
  // Several lines move together.
  ['- uno\n|- due\n- tre|', 'in', '- uno\n|  - due\n  - tre|'],
  // The first item has nothing to go under, and a line that is not of a list has nothing to move.
  ['- |uno', 'in', '- |uno'],
  ['te|sto', 'in', 'te|sto'],
  ['- |uno', 'out', '- |uno'],
] as const)('%s moved %s', (before, direction, after) => {
  expect(written(moveListLines(state(before), direction))).toBe(after);
});

test('a link around the selection, whose words are the selection', () => {
  expect(written(writeLink(state('una |parola| qui'), '/about'))).toBe('una |[parola](/about)| qui');
  expect(written(writeLink(state('una| parola |qui'), '/about'))).toBe('una|[parola](/about)|qui');
});

test('with nothing selected the words are the name of the page, or the address', () => {
  expect(written(writeLink(state('vedi |'), '/about', 'Chi siamo'))).toBe('vedi |[Chi siamo](/about)|');
  expect(writeLink(state('vedi |'), 'https://example.org').value).toBe(
    'vedi [https://example.org](https://example.org)',
  );
});

test('a link the selection stands in gives its address back, changes it, and goes with no address', () => {
  expect(linkAddressAt(state('una [pa|rola](/about) qui'))).toBe('/about');
  expect(linkAddressAt(state('una pa|rola qui'))).toBe('');
  expect(writeLink(state('una [pa|rola](/about) qui'), '/news').value).toBe('una [parola](/news) qui');
  expect(written(writeLink(state('una [pa|rola](/about) qui'), ''))).toBe('una |parola| qui');
  expect(writeLink(state('una |parola| qui'), '  ').value).toBe('una parola qui');
});

test('an address cannot break the link it is written in', () => {
  expect(writeLink(state('|parola|'), ' /una pagina (due) ').value).toBe(
    '[parola](/una%20pagina%20%28due%29)',
  );
});

test.each([
  ['una |parola| qui', 'ocean', 'una :ocean[|parola|] qui'],
  // A colour again on the same words changes, and never nests.
  ['una :ocean[|parola|] qui', 'danger', 'una :danger[|parola|] qui'],
  ['una |:ocean[parola]| qui', 'danger', 'una :danger[|parola|] qui'],
  ['una :ocean[una |parola| qui] qui', 'aurora', 'una :aurora[|una parola qui|] qui'],
  ['una :oc|ean[parola] qui', 'brand', 'una :brand[|parola|] qui'],
  // A colour inside what is selected gives way.
  ['|una :ocean[parola] qui|', 'brand', ':brand[|una parola qui|]'],
  // With nothing selected it waits for the words.
  ['una | qui', 'ocean', 'una :ocean[|] qui'],
  // Line by line, like a mark.
  ['|- uno\n- due|', 'brand', '- :brand[|uno]\n- :brand[due|]'],
  // No colour: the words stay.
  ['una :ocean[|parola|] qui', null, 'una |parola| qui'],
  ['|una :ocean[parola] qui|', null, '|una parola qui|'],
  ['una |parola| qui', null, 'una |parola| qui'],
  ['una | qui', null, 'una | qui'],
] as const)('%s in %s', (before, colour, after) => {
  expect(written(writeColour(state(before), colour))).toBe(after);
});
