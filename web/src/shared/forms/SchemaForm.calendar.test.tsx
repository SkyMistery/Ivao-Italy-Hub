import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';
import { z } from 'zod';

import { renderWithProviders } from '../../test/harness';
import type { CalendarKind } from '../api/bootstrap';

import type { BusyStretch } from './busy';

/**
 * A `calendar: true` date field (note 2026-11-18-il-calendario-libero-e-occupato): written by hand in the division's order and
 * read back, or chosen on the free and busy calendar, where an hour a blocking kind takes cannot be chosen. The calendar is
 * read from `/api/calendar/busy`, the row being written left out.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../api/client')),
  api: { GET: api.get },
}));

const { SchemaForm } = await import('./SchemaForm');

const kinds: CalendarKind[] = [
  { key: 'event', label: { en: 'Event' }, colour: 'blue', blocks: true },
  { key: 'training', label: { en: 'Training' }, colour: 'green', blocks: false },
];

const busy: BusyStretch[] = [
  {
    startsAtUtc: '2026-11-20T14:00:00Z',
    endsAtUtc: '2026-11-20T16:00:00Z',
    allDay: false,
    kind: 'event',
    blocks: true,
    title: { en: 'Fly-in' },
    kindLabel: { en: 'Event' },
    kindColour: 'blue',
  },
  {
    startsAtUtc: '2026-11-20T09:00:00Z',
    endsAtUtc: '2026-11-20T10:00:00Z',
    allDay: false,
    kind: 'training',
    blocks: false,
    title: { en: 'S2 · LIRF_TWR' },
    kindLabel: { en: 'Training' },
    kindColour: 'green',
  },
];

const schema = z.object({ meetsAt: z.string().optional().meta({ datetime: true, calendar: true }) });

function draw(onSubmit = vi.fn(() => Promise.resolve())) {
  renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{ meetsAt: '2026-11-20T08:00:00Z' }}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
      division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
      calendar={{ kinds, except: { module: 'events', id: 'event:7' } }}
    />,
  );

  return onSubmit;
}

beforeEach(() => {
  api.get.mockResolvedValue({ data: busy, response: new Response(null, { status: 200 }) });
});

afterEach(() => {
  api.get.mockReset();
});

describe('a date on the free and busy calendar', () => {
  test('shows the value in the division order and reads back what is written by hand', async () => {
    const user = userEvent.setup();
    const onSubmit = draw();

    const box = screen.getByRole('textbox');
    expect(box).toHaveValue('20/11/2026 08:00');

    await user.clear(box);
    await user.type(box, '21/11/2026 10:30{Enter}');

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ meetsAt: '2026-11-21T10:30:00Z' }));
  });

  test('a text that is not a date says so, and Enter saves nothing', async () => {
    const user = userEvent.setup();
    const onSubmit = draw();

    const box = screen.getByRole('textbox');
    await user.clear(box);
    await user.type(box, '31/11/2026 10:00{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent('31/10/2026 14:05');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  test('the button saves nothing either while the box holds a text that is not a date, and saves once it is corrected', async () => {
    // The review of #538, finding 2: the click blurs the box first, and the form used to send the value it held before.
    const user = userEvent.setup();
    const onSubmit = draw();

    const box = screen.getByRole('textbox');
    await user.clear(box);
    await user.type(box, 'next friday');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('31/10/2026 14:05');
    expect(box).toHaveAttribute('aria-invalid', 'true');
    expect(onSubmit).not.toHaveBeenCalled();

    // A second click changes nothing: the refusal is put back on the field each time.
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSubmit).not.toHaveBeenCalled();

    await user.clear(box);
    await user.type(box, '22/11/2026 18:00');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ meetsAt: '2026-11-22T18:00:00Z' }));
    expect(screen.queryByRole('alert')).toBeNull();
  });

  test('asks the calendar for the weeks of the month, leaving out the row being written', async () => {
    const user = userEvent.setup();
    draw();

    await user.click(screen.getByRole('button', { name: /Choose on the calendar/ }));

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith('/api/calendar/busy', {
        params: {
          query: {
            fromUtc: '2026-10-26T00:00:00.000Z',
            toUtc: '2026-12-07T00:00:00.000Z',
            exceptModule: 'events',
            exceptId: 'event:7',
          },
        },
      }),
    );
  });

  test('marks the busy day and its hours, and the hour a blocking kind takes cannot be chosen', async () => {
    const user = userEvent.setup();
    const onSubmit = draw();

    await user.click(screen.getByRole('button', { name: /Choose on the calendar/ }));
    const picker = await screen.findByTestId('busy-calendar');

    // The legend names the kinds of the month, and says which one blocks.
    const legend = within(picker).getByRole('list', { name: 'Legend' });
    await waitFor(() => expect(legend).toHaveTextContent('Event'));
    expect(legend).toHaveTextContent('blocks');
    expect(legend).toHaveTextContent('Training');

    await user.click(
      within(picker).getByRole('button', { name: /November 20, 2026, busy: Event, Training/ }),
    );

    expect(within(picker).getByRole('button', { name: '14:00 UTC, blocked' })).toBeDisabled();
    expect(within(picker).getByRole('button', { name: '15:00 UTC, blocked' })).toBeDisabled();
    expect(within(picker).getByRole('button', { name: '09:00 UTC, busy: Training' })).toBeEnabled();
    expect(within(picker).getByText('Fly-in')).toBeInTheDocument();

    await user.click(within(picker).getByRole('button', { name: '11:00 UTC, Free' }));

    expect(screen.getByRole('textbox')).toHaveValue('20/11/2026 11:00');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ meetsAt: '2026-11-20T11:00:00Z' }));
  });

  test('a date without a time cannot be put on a day a blocking kind touches, since it takes the whole day', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn(() => Promise.resolve());
    renderWithProviders(
      <SchemaForm
        schema={z.object({ on: z.string().optional().meta({ date: true, calendar: true }) })}
        defaults={{ on: '2026-11-19T00:00:00Z' }}
        locales={['en']}
        labels="test"
        submitLabel="Save"
        onSubmit={onSubmit}
        division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
        calendar={{ kinds }}
      />,
    );

    await user.click(screen.getByRole('button', { name: /Choose on the calendar/ }));
    const picker = await screen.findByTestId('busy-calendar');

    // The 20th has a blocking event from 14:00 to 16:00: an instant could still take its morning, a day cannot.
    await waitFor(() =>
      expect(
        within(picker).getByRole('button', { name: /November 20, 2026, blocked all day/ }),
      ).toBeDisabled(),
    );

    await user.click(within(picker).getByRole('button', { name: /November 21, 2026, Free/ }));
    expect(screen.getByRole('textbox')).toHaveValue('21/11/2026');
  });

  test('a retired kind keeps blocking, and the legend names it with the words of the kinds table', async () => {
    // Carmine's answer on #538, "finché si toglie": retired, so not in the bootstrap, and blocking until its box is unticked.
    api.get.mockResolvedValue({
      data: [
        {
          startsAtUtc: '2026-11-20T14:00:00Z',
          endsAtUtc: '2026-11-20T16:00:00Z',
          allDay: false,
          kind: 'old-fly-in',
          blocks: true,
          title: { en: 'Fly-in of the past' },
          kindLabel: { en: 'Old fly-in' },
          kindColour: 'pink',
        },
      ],
      response: new Response(null, { status: 200 }),
    });
    const user = userEvent.setup();
    draw();

    await user.click(screen.getByRole('button', { name: /Choose on the calendar/ }));
    const picker = await screen.findByTestId('busy-calendar');

    const legend = within(picker).getByRole('list', { name: 'Legend' });
    await waitFor(() => expect(legend).toHaveTextContent('Old fly-in'));
    expect(legend).not.toHaveTextContent('old-fly-in');

    await user.click(within(picker).getByRole('button', { name: /November 20, 2026, busy: Old fly-in/ }));
    expect(within(picker).getByRole('button', { name: '14:00 UTC, blocked' })).toBeDisabled();
  });

  test('a form with such a field and no calendar says why it cannot draw it', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() =>
      renderWithProviders(
        <SchemaForm
          schema={schema}
          defaults={{}}
          locales={['en']}
          labels="test"
          division={{ defaultLocale: 'en', timezone: 'Europe/Rome' }}
        />,
      ),
    ).toThrow(/calendar/);
  });
});
