import { expect, test } from 'vitest';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { MessageThread, type MessageThreadValue } from './MessageThread';

/**
 * A thread names an erased person as every page does (issue #332, item 11): «Deleted person», never «VID -17»; and the
 * word «VID» comes from the language files, not from the component.
 */
test('a thread names an erased sender, participant and staff member as a deleted person', () => {
  const thread: MessageThreadValue = {
    id: 1,
    department: 'ED',
    kind: 'General',
    subject: 'A question',
    body: 'Hello',
    status: 'Answered',
    senderVid: -17,
    createdAt: '2026-01-15T18:00:00Z',
    readerIsSender: false,
    participants: [-18, 790002],
    references: [],
    replies: [
      {
        id: 2,
        side: 'Department',
        authorVid: -19,
        authorName: null,
        body: 'One',
        createdAt: '2026-01-15T19:00:00Z',
      },
      {
        id: 3,
        side: 'Department',
        authorVid: 790003,
        authorName: null,
        body: 'Two',
        createdAt: '2026-01-15T20:00:00Z',
      },
      {
        id: 4,
        side: 'Department',
        authorVid: 790004,
        authorName: 'Test Staff',
        body: 'Three',
        createdAt: '2026-01-15T21:00:00Z',
      },
    ],
  };

  renderWithProviders(<MessageThread thread={thread} timezone="UTC" />, { i18n: createTestI18n() });

  const text = document.body.textContent ?? '';
  expect(text).not.toMatch(/-1[789]/);
  expect(text).toContain('Deleted person');
  expect(text).toContain('790002');
  expect(text).toContain('VID 790003');
  expect(text).toContain('Test Staff');
});
