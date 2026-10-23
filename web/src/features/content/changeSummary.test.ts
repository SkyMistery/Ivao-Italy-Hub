import { describe, expect, it } from 'vitest';

import { createTestI18n } from '../../test/harness';

import { CHANGELOG_MAX, changeSummary } from './changeSummary';
import type { ContentReviewDto } from './queries';

const i18n = createTestI18n();
const t = i18n.getFixedT('en');
const read = (value: Record<string, string> | null | undefined) => value?.['en'] ?? '';

function changes(over: Partial<ContentReviewDto>): ContentReviewDto {
  return {
    firstPublication: false,
    titleChanged: false,
    path: '/about',
    sections: [],
    menu: null,
    note: null,
    readyAt: null,
    readyByName: null,
    blocks: { added: 0, removed: 0, changed: 0 },
    fields: [],
    ...over,
  };
}

describe('changeSummary', () => {
  it('says a first publication is one', () => {
    expect(changeSummary(changes({ firstPublication: true }), t, read)).toBe('First publication.');
  });

  it('names the sections, counts the blocks and names the fields', () => {
    const line = changeSummary(
      changes({
        sections: [
          { key: 'hero', title: { en: 'Welcome', it: 'Benvenuti' }, change: 'Changed' },
          { key: 'faq', title: null, change: 'Added' },
          { key: 'closing', title: null, change: 'Unchanged' },
          { key: 'old', title: { en: 'Old news', it: 'Vecchie notizie' }, change: 'Removed' },
        ],
        blocks: { added: 2, removed: 1, changed: 1 },
        fields: ['title', 'showFooter'],
      }),
      t,
      read,
    );

    expect(line).toBe(
      'Sections added: faq. Sections changed: Welcome. Sections removed: Old news. ' +
        'Blocks: 2 added, 1 changed, 1 removed. Changed: Title, footer.',
    );
  });

  it('says nothing changed when nothing did', () => {
    expect(
      changeSummary(changes({ sections: [{ key: 'hero', title: null, change: 'Unchanged' }] }), t, read),
    ).toBe('Published again, nothing changed.');
  });

  it('never writes more than a version keeps', () => {
    const many = Array.from({ length: 80 }, (_, index) => ({
      key: `section-number-${String(index)}`,
      title: null,
      change: 'Added' as const,
    }));

    expect(changeSummary(changes({ sections: many }), t, read)).toHaveLength(CHANGELOG_MAX);
  });

  it('writes in the language on screen', () => {
    const italian = i18n.getFixedT('it');
    expect(changeSummary(changes({ blocks: { added: 1, removed: 0, changed: 3 } }), italian, read)).toBe(
      'Blocchi: 1 aggiunto, 3 modificati.',
    );
  });
});
