import { render } from '@testing-library/react';
import { expect, test } from 'vitest';

import { MarkdownContent } from './MarkdownContent';
import { TEXT_COLOURS, textColourClass } from './textColours';

/**
 * Prose drawn again is the same prose, not a new copy of it (issue #351).
 *
 * The elements the library draws used to be handed over as new functions at every render, which
 * React reads as new components: each paragraph was taken out of the document and put back whenever
 * anything above it drew. In the editor's preview that is every keystroke and every drag, and
 * Chromium, asked by the drag and drop to measure the page in between, left the whole preview
 * without a box (`e2e/full/drop.spec.ts`).
 */
test('drawing the same prose again keeps the elements it drew', () => {
  const { container, rerender } = render(
    <MarkdownContent source={'A paragraph with a [link](https://example.com).\n\n- and a list'} />,
  );
  const drawn = [...container.querySelectorAll('p, a, ul, li')];
  expect(drawn).toHaveLength(4);

  rerender(<MarkdownContent source={'A paragraph with a [link](https://example.com).\n\n- and a list'} />);

  expect([...container.querySelectorAll('p, a, ul, li')]).toEqual(drawn);
  for (const element of drawn) {
    expect(element.isConnected).toBe(true);
  }
});

const drawn = (source: string) => render(<MarkdownContent source={source} />).container;

/** The design of the editor, phase 3, §4.6: two pieces of syntax more, and a body already saved draws as it did (§6). */
test('two tildes strike, and one is a tilde', () => {
  expect(drawn('~~via~~').querySelector('del')?.textContent).toBe('via');

  const one = drawn('circa ~30~ minuti');
  expect(one.querySelector('del')).toBeNull();
  expect(one.textContent).toContain('~30~');
});

test.each(TEXT_COLOURS)('%s colours a word, and the rest of the sentence stays as it was', (colour) => {
  const container = drawn(`una :${colour}[parola] qui`);

  const coloured = container.querySelector(`span.${textColourClass(colour)}`);
  expect(coloured?.textContent).toBe('parola');
  expect(container.textContent).toBe('una parola qui');
});

test('a colour holds other marks, and a link holds a colour', () => {
  expect(drawn(':ocean[una **parola**]').querySelector('span strong')?.textContent).toBe('parola');
  expect(drawn(':ocean[[pagina](/about)]').querySelector('span a')?.getAttribute('href')).toBe('/about');
  expect(drawn('[:ocean[pagina]](/about)').querySelector('a span')?.textContent).toBe('pagina');
});

test('a name outside the set is plain text, with no colour', () => {
  const container = drawn('una :orange[parola] qui');

  expect(container.querySelector('span')).toBeNull();
  expect(container.textContent).toBe('una parola qui');
});

test('nothing but the class comes from what is written around a colour', () => {
  const coloured = drawn(':ocean[parola]{.x style="color:red" onclick="y"}').querySelector('span');

  expect(coloured?.getAttributeNames()).toEqual(['class']);
  expect(coloured?.className).toBe(textColourClass('ocean'));
});

test.each([
  // A colon in a sentence is not a directive, and the words after it stay.
  'alle 10:30 in punto',
  'Frequenza:LIRF_APP',
  'nota:importante',
  ':ocean senza parole',
  ':ocean[] senza parole',
  'ore 10:30[1]',
  ':smile: e basta',
  'un nome:strano{a=1} qui',
  // Written alone on a line these are the other two kinds of directive, which are not read at all.
  '::leaf',
  ':::note',
])('%s is drawn as it was written', (source) => {
  const container = drawn(source);

  expect(container.textContent).toBe(source);
  expect(container.querySelector('span')).toBeNull();
});

test('the words of a picture keep their colons', () => {
  expect(drawn('![foto:bella 10:30](/x.png)').querySelector('img')?.getAttribute('alt')).toBe(
    'foto:bella 10:30',
  );
  expect(drawn('![il **logo**](/x.png)').querySelector('img')?.getAttribute('alt')).toBe('il logo');
});

test('a block directive does not swallow the lines under it', () => {
  const container = drawn(':::note\ncontenuto\n:::\n\ndopo');

  expect(container.textContent).toContain(':::note');
  expect(container.textContent).toContain('dopo');
});

test.each([
  // A table is the table block: in the text it stays the words it always was, not an empty hole.
  ['| a | b |\n|---|---|\n| 1 | 2 |', '| a | b |'],
  ['nota[^1]\n\n[^1]: testo della nota', 'testo della nota'],
  ['- [ ] da fare', '[ ] da fare'],
])('what the rest of GFM would read stays text: %s', (source, kept) => {
  const container = drawn(source);

  expect(container.textContent).toContain(kept);
  expect(container.querySelector('table, sup, input')).toBeNull();
});

test('an address alone in a sentence stays words', () => {
  expect(drawn('vedi www.ivao.aero').querySelector('a')).toBeNull();
});
