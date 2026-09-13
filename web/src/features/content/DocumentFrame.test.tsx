import { screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { DocumentFooter, DocumentNotice, DocumentStrip } from './DocumentFrame';
import { isOperational } from './operational';
import type { PublicContentDto } from './queries';

/**
 * What the public screen says around an operational document (G14): the facts under the title,
 * the notice when it is not the one to follow, and the edition on the footer.
 */

// The way on is the router's link; an anchor with the same address says the same thing.
vi.mock('@tanstack/react-router', () => ({
  Link: ({
    to,
    params,
    children,
    ...rest
  }: {
    to: string;
    params?: Record<string, string>;
    children: ReactNode;
  }) => (
    <a href={to.replace('$slug', params?.slug ?? '')} {...rest}>
      {children}
    </a>
  ),
}));

function document(overrides: Partial<PublicContentDto> = {}): PublicContentDto {
  return {
    kind: 'Document',
    slug: 'lirf-twr-sop',
    ownerDepartment: 'AOD',
    title: { en: 'Fiumicino Tower SOP', it: 'SOP Torre Fiumicino' },
    summary: null,
    seo: null,
    body: { schemaVersion: 1, sections: [] },
    schemaVersion: 1,
    category: null,
    coverMediaId: null,
    fileMediaId: null,
    version: 3,
    publishedAt: '2026-09-12T10:00:00Z',
    documentType: 'Sop',
    primaryPosition: 'LIRF_TWR',
    secondaryPosition: null,
    icao: 'LIRF',
    fir: 'LIRR',
    effectiveOn: '2026-10-01T00:00:00',
    reviewOn: null,
    retiredAt: null,
    supersededBySlug: null,
    supersededByTitle: null,
    showFooter: true,
    publishedByName: 'Test User',
    airac: '2609',
    ...overrides,
  };
}

test('a guide filed among the documents is not operational, a SOP is', () => {
  expect(
    isOperational(
      document({ documentType: null, primaryPosition: null, icao: null, fir: null, effectiveOn: null }),
    ),
  ).toBe(false);
  expect(isOperational(document())).toBe(true);
});

test('the strip says what the document is about, as words and not stored names', () => {
  renderWithProviders(<DocumentStrip content={document()} />);

  expect(screen.getByText('SOP')).toBeInTheDocument();
  expect(screen.getByText('LIRF_TWR')).toBeInTheDocument();
  expect(screen.getByText('LIRR')).toBeInTheDocument();
  // The date, in the language on screen, and no time beside it: a document comes into force on a day.
  expect(screen.getByText('Oct 1, 2026')).toBeInTheDocument();
  expect(screen.queryByText('Counterpart')).not.toBeInTheDocument();
});

test('a document not yet in force says so, and one retired says what replaced it', () => {
  const { unmount } = renderWithProviders(
    <DocumentNotice content={document()} now={new Date('2026-09-12T00:00:00Z')} />,
  );
  expect(screen.getByText('Not in force yet: it applies from Oct 1, 2026.')).toBeInTheDocument();
  unmount();

  renderWithProviders(
    <DocumentNotice
      content={document({
        retiredAt: '2026-11-01T00:00:00',
        supersededBySlug: 'lirf-twr-sop-v2',
        supersededByTitle: { en: 'Fiumicino Tower SOP, second edition' },
      })}
    />,
  );

  expect(screen.getByText('No longer in force since Nov 1, 2026.')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Fiumicino Tower SOP, second edition' })).toHaveAttribute(
    'href',
    '/documents/lirf-twr-sop-v2',
  );
});

test('the footer is the edition: version, date, a name and the cycle, and a way to paper', () => {
  renderWithProviders(<DocumentFooter content={document()} />);

  expect(screen.getByText('3')).toBeInTheDocument();
  expect(screen.getByText('Sep 12, 2026')).toBeInTheDocument();
  expect(screen.getByText('Test User')).toBeInTheDocument();
  expect(screen.getByText('2609')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Print' })).toBeInTheDocument();
});
