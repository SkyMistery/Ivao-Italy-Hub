import { queryOptions } from '@tanstack/react-query';
import { screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import { SchemaForm } from '../forms';
import { col } from '../list/columns';
import { DataList, type Page } from '../list/DataList';
import { listSearchSchema } from '../list/search';
import { MessageThread, type MessageThreadValue } from '../ui/MessageThread';

import { formatMoment } from './useMoment';

/**
 * Times are twenty four hours, in every language, and the rule is written once (note 2026-10-13-le-ore-in-24, issue
 * #260): in English three formatters of the core wrote "7:00 PM" where `useMoment` wrote 19:00. English is the
 * language that shows it; Italian is twenty four hours by default and would pass either way.
 *
 * 18:00 UTC in January is 19:00 in Rome, so each reading below is told apart from the other.
 */

const EVENING = '2026-01-15T18:00:00Z';
const ZONE = 'Europe/Rome';

const noTwelveHourClock = () => {
  expect(document.body.textContent).not.toMatch(/\b[AP]M\b/);
};

describe('formatMoment', () => {
  test('writes an evening as 19:00 in English, never 7:00 PM', () => {
    expect(formatMoment('en', '2026-01-15T19:00:00Z')).toBe('Jan 15, 2026, 19:00');
    expect(formatMoment('en', EVENING, { timeZone: ZONE, dateStyle: 'short' })).toBe('1/15/26, 19:00');
  });

  test('writes the time alone, and midnight as 00:00', () => {
    expect(formatMoment('en', '2026-01-15T00:05:00Z', { date: false })).toBe('00:05');
  });

  test('says nothing for what is not an instant', () => {
    expect(formatMoment('en', '')).toBe('');
    expect(formatMoment('en', 'not a date')).toBe('');
    expect(formatMoment('en', undefined)).toBe('');
  });
});

test('the line under an instant field is twenty four hours in English', () => {
  renderWithProviders(
    <SchemaForm
      schema={z.object({ startsAt: z.string().optional().meta({ datetime: true }) })}
      defaults={{ startsAt: EVENING }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
      division={{ defaultLocale: 'en', timezone: ZONE }}
    />,
    { i18n: createTestI18n({ test: { fields: { startsAt: 'Starts' } } }) },
  );

  expect(screen.getByText(/Europe\/Rome/)).toHaveTextContent(/^1\/15\/26, 19:00 Europe\/Rome$/);
  noTwelveHourClock();
});

interface Row {
  readonly id: number;
  readonly startsAt: string;
}

test('a date column is twenty four hours in English, in UTC and where the division lives', async () => {
  const page: Page<Row> = { items: [{ id: 1, startsAt: EVENING }], page: 1, pageSize: 25, total: 1 };

  renderWithProviders(
    <DataList
      columns={[col.date<Row>('startsAt')]}
      query={queryOptions({
        queryKey: ['test', 'twenty-four-hours'] as const,
        queryFn: () => Promise.resolve(page),
      })}
      search={listSearchSchema.parse({})}
      onSearchChange={() => undefined}
      labels="test"
      locale="en"
      defaultLocale="en"
      timezone={ZONE}
    />,
    { i18n: createTestI18n({ test: { fields: { startsAt: 'Starts' } } }) },
  );

  expect(await screen.findByText('1/15/26, 18:00 UTC')).toBeInTheDocument();
  expect(screen.getByText('1/15/26, 19:00')).toBeInTheDocument();
  noTwelveHourClock();
});

test('the times of a conversation are twenty four hours in English', () => {
  const thread: MessageThreadValue = {
    id: 1,
    department: 'ED',
    kind: 'General',
    subject: 'A question',
    body: 'Hello',
    status: 'Answered',
    senderVid: 790001,
    createdAt: EVENING,
    readerIsSender: true,
    participants: [],
    references: [],
    replies: [
      {
        id: 2,
        side: 'Department',
        authorVid: null,
        authorName: null,
        body: 'Hello back',
        createdAt: '2026-01-15T20:30:00Z',
      },
    ],
  };

  renderWithProviders(<MessageThread thread={thread} timezone={ZONE} />, { i18n: createTestI18n() });

  expect(document.body.textContent).toContain('Jan 15, 2026, 19:00');
  expect(document.body.textContent).toContain('Jan 15, 2026, 21:30');
  noTwelveHourClock();
});
