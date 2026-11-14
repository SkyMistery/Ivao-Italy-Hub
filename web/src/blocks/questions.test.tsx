import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { AccordionBlock, NetworkStatsBlock, StatsBlock } from './blocks';
import { PrintContext } from './print';

/**
 * PR 3.13 of the editor's phase 3: questions and answers with a title, numbers and a way to open (§5.4), and a word of
 * the editor's own under a figure (§5.7).
 */

const en = (value: string) => ({ en: value, it: value });

const items = [
  { question: en('When?'), answer: en('At the top of the hour.') },
  { question: en('Where?'), answer: en('On the ground frequency.') },
];

describe('questions and answers', () => {
  test('a block written before PR 3.13 draws as it did: no title, no numbers, every answer closed', () => {
    renderWithProviders(<AccordionBlock props={{ allowMultiple: false, items }} />);

    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
    expect(screen.getByRole('button', { name: 'When?' })).toBeInTheDocument();
    expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();
    expect(screen.queryByText('On the ground frequency.')).not.toBeInTheDocument();
  });

  test('the title stands above the questions, and the numbers are part of each question', () => {
    renderWithProviders(
      <AccordionBlock props={{ title: en('Before you connect'), numbered: true, items }} />,
    );

    expect(screen.getByRole('heading', { level: 2, name: 'Before you connect' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '1. When?' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '2. Where?' })).toBeInTheDocument();
  });

  test('a title left empty in the language on screen is no title', () => {
    renderWithProviders(<AccordionBlock props={{ title: en('  '), items }} />);

    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
  });

  test('`first` opens the first answer, and opening another closes it', async () => {
    renderWithProviders(<AccordionBlock props={{ initially: 'first', allowMultiple: false, items }} />);

    expect(screen.getByText('At the top of the hour.')).toBeVisible();
    expect(screen.queryByText('On the ground frequency.')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Where?' }));

    expect(screen.getByText('On the ground frequency.')).toBeVisible();
    expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();
  });

  test('`all` opens every answer, and closing one leaves the others open whatever `allowMultiple` says', async () => {
    renderWithProviders(<AccordionBlock props={{ initially: 'all', allowMultiple: false, items }} />);

    expect(screen.getByText('At the top of the hour.')).toBeVisible();
    expect(screen.getByText('On the ground frequency.')).toBeVisible();

    await userEvent.click(screen.getByRole('button', { name: 'When?' }));

    expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();
    expect(screen.getByText('On the ground frequency.')).toBeVisible();
  });

  test('a way of opening changed in the editor shows at once, without opening the page again', () => {
    const { rerender } = renderWithProviders(<AccordionBlock props={{ initially: 'closed', items }} />);
    expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();

    rerender(<AccordionBlock props={{ initially: 'first', items }} />);
    expect(screen.getByText('At the top of the hour.')).toBeVisible();

    rerender(<AccordionBlock props={{ initially: 'all', items }} />);
    expect(screen.getByText('On the ground frequency.')).toBeVisible();
  });

  test('a way of opening the block does not know is the one it had before: closed', () => {
    renderWithProviders(<AccordionBlock props={{ initially: 'half', items }} />);

    expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();
  });

  test('on paper the title heads the questions, a level above them, with their numbers', () => {
    renderWithProviders(
      <PrintContext.Provider value={true}>
        <AccordionBlock props={{ title: en('Before you connect'), numbered: true, items }} />
      </PrintContext.Provider>,
    );

    expect(screen.getByRole('heading', { level: 2, name: 'Before you connect' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: '1. When?' })).toBeInTheDocument();
    expect(screen.getByText('On the ground frequency.')).toBeVisible();
  });
});

describe('the word under a figure', () => {
  test('a label written by the editor takes the place of the hub’s word, and the others keep theirs', () => {
    renderWithProviders(
      <StatsBlock
        props={{
          columns: 3,
          metrics: [
            { metric: 'knownMembers', label: en('pilots and controllers') },
            { metric: 'staffMembers' },
          ],
        }}
        data={{
          metrics: [
            { metric: 'knownMembers', value: 1284 },
            { metric: 'staffMembers', value: 37 },
          ],
        }}
      />,
    );

    expect(screen.getByText('pilots and controllers')).toBeInTheDocument();
    expect(screen.queryByText('members')).toBeNull();
    expect(screen.getByText('staff')).toBeInTheDocument();
  });

  test('a label left empty is no label: the hub’s word stands', () => {
    renderWithProviders(
      <StatsBlock
        props={{ columns: 3, metrics: [{ metric: 'knownMembers', label: en(' ') }] }}
        data={{ metrics: [{ metric: 'knownMembers', value: 1284 }] }}
      />,
    );

    expect(screen.getByText('members')).toBeInTheDocument();
  });

  test('the figures of the network take a label the same way', () => {
    renderWithProviders(
      <NetworkStatsBlock
        props={{
          figures: [{ figure: 'divisionAtc', label: en('on frequency') }, { figure: 'divisionPilots' }],
          showPositions: false,
        }}
        data={{
          updatedAt: '2026-11-14T12:00:00Z',
          figures: [
            { figure: 'divisionAtc', value: 4 },
            { figure: 'divisionPilots', value: 52 },
          ],
          positions: [],
        }}
      />,
    );

    expect(screen.getByText('on frequency')).toBeInTheDocument();
    expect(screen.queryByText('controllers')).toBeNull();
    expect(screen.getByText('pilots')).toBeInTheDocument();
  });
});
