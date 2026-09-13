import { screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { DocumentFooter, DocumentNotice, DocumentStrip } from './DocumentFrame';
import type { PublicContentDto } from './queries';

/**
 * What the public screen says around a document (G14): the facts under the title,
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
    id: 1,
    kind: 'Document',
    slug: 'code-of-conduct',
    path: 'code-of-conduct',
    ownerDepartment: 'AOD',
    title: { en: 'Code of conduct', it: 'Codice di condotta' },
    summary: null,
    seo: null,
    body: { schemaVersion: 1, sections: [] },
    schemaVersion: 1,
    category: null,
    coverMediaId: null,
    fileMediaId: null,
    version: 3,
    publishedAt: '2026-09-12T10:00:00Z',
    effectiveOn: '2026-10-01T00:00:00',
    reviewOn: null,
    retiredAt: null,
    supersededBySlug: null,
    supersededByTitle: null,
    showFooter: true,
    publishedByName: 'Test User',
    ...overrides,
  };
}

test('the strip says when the document comes into force, and nothing when it does not say', () => {
  const { unmount } = renderWithProviders(<DocumentStrip content={document()} />);

  // The date, in the language on screen, and no time beside it: a document comes into force on a day.
  expect(screen.getByText('Oct 1, 2026')).toBeInTheDocument();
  unmount();

  const { container } = renderWithProviders(<DocumentStrip content={document({ effectiveOn: null })} />);
  expect(container).toBeEmptyDOMElement();
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
        supersededBySlug: 'code-of-conduct-2027',
        supersededByTitle: { en: 'Code of conduct, 2027 edition' },
      })}
    />,
  );

  expect(screen.getByText('No longer in force since Nov 1, 2026.')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Code of conduct, 2027 edition' })).toHaveAttribute(
    'href',
    '/documents/code-of-conduct-2027',
  );
});

test('the footer is the edition: version, date and a name, and a way to paper', () => {
  renderWithProviders(<DocumentFooter content={document()} />);

  expect(screen.getByText('3')).toBeInTheDocument();
  expect(screen.getByText('Sep 12, 2026')).toBeInTheDocument();
  expect(screen.getByText('Test User')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Print' })).toBeInTheDocument();
});
