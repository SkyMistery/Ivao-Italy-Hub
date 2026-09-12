import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { BACKGROUNDS } from '../../blocks';
import { renderWithProviders } from '../../test/harness';

import { SectionFrame } from './SectionFrame';

/**
 * The two things about a section that are chosen while looking at the page rather than written.
 *
 * ⚠️ What matters here is that they apply **at once**. HQ's builder puts the same two on the
 * section's own bar, and the reason is not decoration: a background you have to pick in a select and
 * then confirm with a button is a background you compare by memory
 * (`decisions/2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`).
 */

const words = englishCommon.content.section;

test('a background is one click, and the strip says which one is on', async () => {
  const user = userEvent.setup();
  const onBackground = vi.fn();

  renderWithProviders(
    <SectionFrame background="muted" layout="stacked" onBackground={onBackground} onLayout={vi.fn()} />,
  );

  // Pressed, so a screen reader is told which of the four the section carries — the swatches are
  // colours, and a colour says nothing to somebody who cannot see it.
  expect(screen.getByRole('button', { name: words.options.background.muted })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await user.click(screen.getByRole('button', { name: words.options.background.accent }));

  // No form and no "apply": the click is the change.
  expect(onBackground).toHaveBeenCalledWith('accent');
});

test('a layout is a picture, and choosing one applies it', async () => {
  const user = userEvent.setup();
  const onLayout = vi.fn();

  renderWithProviders(
    <SectionFrame background="none" layout="stacked" onLayout={onLayout} onBackground={vi.fn()} />,
  );

  // All five, each with a name a reader hears rather than a diagram they cannot see.
  for (const label of Object.values(words.options.layout)) {
    expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
  }

  await user.click(screen.getByRole('button', { name: words.options.layout['1/3+2/3'] }));

  expect(onLayout).toHaveBeenCalledWith('1/3+2/3');
});

test('every ground the envelope allows is in the strip, with a name', () => {
  renderWithProviders(
    <SectionFrame background="none" layout="stacked" onBackground={vi.fn()} onLayout={vi.fn()} />,
  );

  // Eight since 12 September 2026, and the test is the list itself rather than the number: adding a
  // value to `BACKGROUNDS` without a label leaves a swatch whose only name is the key, which a
  // sighted editor never notices and a screen reader reads out loud. `SWATCH` is a `Record`, so the
  // colour is the compiler's problem; the name is this one's.
  for (const value of BACKGROUNDS) {
    const label: string | undefined = words.options.background[value];
    expect(label, `no name for the "${value}" ground`).toBeTruthy();
    expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
  }
});
