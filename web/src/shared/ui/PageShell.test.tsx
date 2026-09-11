import { screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { CompactPageShells, PageActions, PageShell } from './PageShell';

/**
 * The frame of a page, in its two densities (Carmine, 11 September 2026).
 *
 * The back office draws it on one line — the title is the end of the trail, the actions beside it —
 * and the public site keeps the large title and the summary a reader expects. These are the
 * properties of each that a screen relies on without knowing which one it is in.
 */

// The trail's links are the router's, and a router is not what is under test here: an anchor with
// the same address says the same thing.
vi.mock('@tanstack/react-router', () => ({
  Link: ({ to, children, ...rest }: { to: string; children: ReactNode }) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

const listScreen = (
  <PageShell
    title="Links"
    description="The addresses this department publishes on the division site."
    breadcrumb={[{ label: 'ED' }, { label: 'Links' }]}
    actions={<button type="button">New link</button>}
  >
    <p>The table</p>
  </PageShell>
);

test('in the back office the title is the end of the trail, and is written once', () => {
  renderWithProviders(<CompactPageShells>{listScreen}</CompactPageShells>);

  // One heading, the page's own, and not a second "Links" as the last crumb above it.
  expect(screen.getByRole('heading', { level: 1, name: 'Links' })).toBeInTheDocument();
  expect(screen.getAllByText('Links')).toHaveLength(1);
  expect(screen.getByText('ED')).toBeInTheDocument();

  // And the actions on the same line, which is the whole of the request.
  expect(screen.getByRole('button', { name: 'New link' })).toBeInTheDocument();
});

test('the sentence the sidebar already says is not said again, but stays as the tooltip', () => {
  renderWithProviders(<CompactPageShells>{listScreen}</CompactPageShells>);

  expect(
    screen.queryByText('The addresses this department publishes on the division site.'),
  ).not.toBeInTheDocument();
  expect(screen.getByRole('heading', { level: 1 })).toHaveAttribute(
    'title',
    'The addresses this department publishes on the division site.',
  );
});

test('a note is information and stays on screen in either density', () => {
  // ⚠️ The difference between `description` and `note` is the one this component asks callers to
  // get right: how many pages were made from a template is not a caption the sidebar repeats.
  const withNote = (
    <PageShell title="Edit template" note="9 rows were made from this template." breadcrumb={[]}>
      <p>The editor</p>
    </PageShell>
  );

  const compact = renderWithProviders(<CompactPageShells>{withNote}</CompactPageShells>);
  expect(screen.getByText('9 rows were made from this template.')).toBeInTheDocument();
  compact.unmount();

  renderWithProviders(withNote);
  expect(screen.getByText('9 rows were made from this template.')).toBeInTheDocument();
});

test('a trail that ends at a list keeps it, and the title says which row', () => {
  // An editor: the trail's last crumb is a link back to the list, not the page itself, so nothing
  // is dropped and the title comes after it.
  renderWithProviders(
    <CompactPageShells>
      <PageShell title="Edit link" breadcrumb={[{ label: 'ED' }, { label: 'Links', to: '/staff/ed/links' }]}>
        <p>The form</p>
      </PageShell>
    </CompactPageShells>,
  );

  expect(screen.getByRole('link', { name: 'Links' })).toHaveAttribute('href', '/staff/ed/links');
  expect(screen.getByRole('heading', { level: 1, name: 'Edit link' })).toBeInTheDocument();
});

test('on the public site the page keeps its large title and its summary', () => {
  renderWithProviders(listScreen);

  expect(
    screen.getByText('The addresses this department publishes on the division site.'),
  ).toBeInTheDocument();
});

test('a screen can send its own controls up beside the title from deep inside it', async () => {
  // The content editor's toolbar is built from the editor's own state, so it cannot be handed to the
  // frame as `actions`; it is drawn there from where it is instead.
  renderWithProviders(
    <CompactPageShells>
      <PageShell title="New news item" breadcrumb={[{ label: 'ED' }]}>
        <section aria-label="editor">
          <PageActions>
            <button type="button">Save draft</button>
          </PageActions>
        </section>
      </PageShell>
    </CompactPageShells>,
  );

  const save = await screen.findByRole('button', { name: 'Save draft' });

  // On the frame's line, next to the heading, and no longer inside the editor that made it.
  expect(screen.getByRole('region', { name: 'editor' })).not.toContainElement(save);
  expect(save.closest('.sticky')).toContainElement(screen.getByRole('heading', { level: 1 }));
});

test('outside a one-line frame the controls stay where they stand', () => {
  renderWithProviders(
    <section aria-label="editor">
      <PageActions>
        <button type="button">Save draft</button>
      </PageActions>
    </section>,
  );

  expect(screen.getByRole('region', { name: 'editor' })).toContainElement(
    screen.getByRole('button', { name: 'Save draft' }),
  );
});
