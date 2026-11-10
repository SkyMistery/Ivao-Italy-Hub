import { expect, test } from 'vitest';

import { readBody } from '../../blocks';

import { NO_STYLE_PAGE, styleText, toSettingsForm } from './contentSettings';

/** The page of the style as text (decisions/2026-11-04-la-pagina-dello-stile.md §2): the words, in one language. */

const locales = ['en', 'xx'];
const read = (text: Record<string, string>) => text.en ?? '';

const style = readBody({
  schemaVersion: 1,
  sections: [
    {
      id: 's1',
      blocks: [
        { id: 'b1', type: 'heading', props: { level: 2, text: { en: 'Tone', xx: 'Ton' } } },
        { id: 'b2', type: 'text', props: { markdown: { en: 'Write **short** sentences.', xx: 'Kurz.' } } },
        {
          id: 'b3',
          type: 'callout',
          props: {
            tone: 'info',
            title: { en: 'News', xx: 'N' },
            text: { en: 'One fact per item.', xx: 'F' },
          },
        },
        { id: 'b4', type: 'image', props: { mediaId: 9, alt: { en: '', xx: '' } } },
      ],
      sections: [
        {
          id: 's2',
          blocks: [
            { id: 'b5', type: 'text', props: { markdown: { en: 'A row, after its section.', xx: 'R' } } },
          ],
          sections: [],
        },
      ],
    },
  ],
});

test('headings stay headings one level below, texts keep their Markdown, other prose becomes paragraphs', () => {
  expect(styleText(style, locales, read)).toBe(
    [
      '### Tone',
      'Write **short** sentences.',
      'News',
      'One fact per item.',
      'A row, after its section.',
    ].join('\n\n'),
  );
});

test('a value that is not prose — an enum, a number, an empty text — says nothing', () => {
  const text = styleText(style, locales, read);

  expect(text).not.toContain('info');
  expect(text).not.toContain('9');
});

test('an empty page is no style', () => {
  expect(styleText(readBody({ schemaVersion: 1, sections: [] }), locales, read)).toBe('');
});

test('the form says "none" when no page is chosen, and the page otherwise', () => {
  expect(toSettingsForm({ stylePageId: null, stylePagePath: null })).toEqual({ stylePageId: NO_STYLE_PAGE });
  expect(toSettingsForm({ stylePageId: 12, stylePagePath: '/style' })).toEqual({ stylePageId: '12' });
});
