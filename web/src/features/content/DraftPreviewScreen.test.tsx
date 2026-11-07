import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { DraftPreviewScreen } from './DraftPreviewScreen';
import type { ContentDetailDto } from './queries';

/**
 * The saved draft in a tab of its own (design of phase 3, §4.7): the page a visitor gets, with a strip that says it is a draft,
 * the links stopped, and «refresh».
 */

vi.mock('@tanstack/react-router', () => ({
  Link: ({ children, ...rest }: { children: React.ReactNode }) => <a {...rest}>{children}</a>,
}));

const words = englishCommon.content.draftPreview;

const row = {
  id: 7,
  kind: 'Page',
  slug: 'welcome',
  parentId: null,
  path: 'welcome',
  ownerDepartment: 'ED',
  visibility: 'Public',
  status: 'Draft',
  reviewNote: null,
  templateId: null,
  isTemplate: false,
  title: { en: 'Welcome', it: 'Benvenuti' },
  summary: { en: 'The first page', it: 'La prima pagina' },
  seo: null,
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 's1',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [
          {
            id: 'b1',
            type: 'cta',
            version: 1,
            props: {
              title: { en: 'Join us', it: 'Unisciti' },
              text: { en: 'Come and fly', it: 'Vieni a volare' },
              href: 'https://example.org/join',
              label: { en: 'Join', it: 'Iscriviti' },
            },
          },
        ],
        sections: [],
      },
    ],
  },
  schemaVersion: 1,
  collections: [],
  coverMediaId: null,
  pinned: false,
  sort: 0,
  fileMediaId: null,
  effectiveOn: null,
  reviewOn: null,
  retiredAt: null,
  supersededById: null,
  showFooter: true,
  draftFromFile: false,
  publishedVersionId: null,
  publishedAt: null,
  createdAt: '2026-11-07T10:00:00Z',
  createdBy: 1,
  updatedAt: '2026-11-07T11:00:00Z',
  updatedBy: 1,
  rowVersion: '2026-11-07T11:00:00',
} as unknown as ContentDetailDto;

function draw(overrides: Partial<ContentDetailDto> = {}, locale = 'en') {
  const onRefresh = vi.fn();
  const view = renderWithProviders(
    <DraftPreviewScreen
      row={{ ...row, ...overrides }}
      locale={locale}
      refreshing={false}
      refreshFailed={false}
      onRefresh={onRefresh}
    />,
  );
  return { ...view, onRefresh };
}

test('the draft is drawn under a strip that says what it is, and «refresh» reads it again', async () => {
  const user = userEvent.setup();
  const { onRefresh } = draw();

  expect(screen.getByText(words.notice)).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Join' })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: words.refresh }));

  expect(onRefresh).toHaveBeenCalledTimes(1);
});

test('a link of the page is not followed, and a button still works', async () => {
  const user = userEvent.setup();
  const { onRefresh } = draw();

  const link = screen.getByRole('link', { name: 'Join' });
  expect(link).toHaveAttribute('href', 'https://example.org/join');

  const clicked = new MouseEvent('click', { bubbles: true, cancelable: true });
  link.dispatchEvent(clicked);
  expect(clicked.defaultPrevented).toBe(true);

  const middle = new MouseEvent('auxclick', { bubbles: true, cancelable: true, button: 1 });
  link.dispatchEvent(middle);
  expect(middle.defaultPrevented).toBe(true);

  // The strip's own button is not a link, and is pressed as any other.
  await user.click(screen.getByRole('button', { name: words.refresh }));
  expect(onRefresh).toHaveBeenCalledTimes(1);
});

test('the links are given back when the preview goes away', () => {
  const { unmount } = draw();
  unmount();

  const outside = document.createElement('a');
  outside.href = 'https://example.org/elsewhere';
  document.body.append(outside);

  const clicked = new MouseEvent('click', { bubbles: true, cancelable: true });
  outside.dispatchEvent(clicked);
  expect(clicked.defaultPrevented).toBe(false);
  outside.remove();
});

test('the page is drawn in the language chosen in the editor', () => {
  draw({}, 'it');

  expect(screen.getByRole('link', { name: 'Iscriviti' })).toBeInTheDocument();
  // The strip is the site's, in the site's language.
  expect(screen.getByText(words.notice)).toBeInTheDocument();
});

test('a news item is drawn the way its public screen draws it, title and summary on top', () => {
  draw({ kind: 'News' });

  expect(screen.getByRole('heading', { level: 1, name: 'Welcome' })).toBeInTheDocument();
  expect(screen.getByText('The first page')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Join' })).toBeInTheDocument();
});

test('an entry already online is dated as its next publication will be, by the last save', () => {
  draw({ kind: 'News', publishedAt: '2025-03-02T10:00:00Z', updatedAt: '2026-11-07T11:00:00Z' });

  expect(document.querySelector('time')).toHaveAttribute('dateTime', '2026-11-07T11:00:00Z');
});

test('a read again that failed is said over the draft read before it', () => {
  renderWithProviders(
    <DraftPreviewScreen row={row} locale="en" refreshing={false} refreshFailed onRefresh={() => {}} />,
  );

  expect(screen.getByRole('alert')).toHaveTextContent(words.refreshFailed);
  expect(screen.getByRole('link', { name: 'Join' })).toBeInTheDocument();
});
