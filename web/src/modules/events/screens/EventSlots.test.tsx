import { TooltipProvider } from '@ivao/atmosphere-react';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { createTestI18n, renderWithProviders } from '../../../test/harness';
import type { PublicEventDto, PublicEventSlotDto } from '../api';
import englishEvents from '../locales/en/events.json';

import { EventSlots } from './EventSlots';

/**
 * The public slots of an event as a visitor reads them (note 2026-10-07-gli-slot-sulla-pagina-dell-evento): by the airport of the
 * event, the departures and the arrivals apart; the main aircraft type, the others on focus and on a tap; a leg of a rotation
 * marked; a row that opens the slot with every type it admits and the legs of its rotation. The words are the module's own file.
 */

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.public;

const airportA = { icao: 'XXAA', name: 'Smoke Airport A' };
const airportB = { icao: 'XXBB', name: 'Smoke Airport B' };
const airportC = { icao: 'XXCC', name: 'Smoke Airport C' };

/** A public slot as the page's read lists it: free, with no flight number, stand or rotation unless said. */
function slot(
  id: number,
  callsign: string,
  hours: readonly [number, number],
  from: { icao: string; name: string },
  to: { icao: string; name: string },
  overrides: Partial<PublicEventSlotDto> = {},
): PublicEventSlotDto {
  const at = (hour: number) => `2099-11-21T${String(hour).padStart(2, '0')}:00:00Z`;

  return {
    id,
    callsign,
    flightNumber: null,
    aircraftTypes: ['XA20'],
    departure: from,
    arrival: to,
    offBlockUtc: at(hours[0]),
    onBlockUtc: at(hours[1]),
    stand: null,
    rotation: null,
    leg: null,
    isArrival: false,
    taken: false,
    ...overrides,
  };
}

/** The slots part of the page's read: the airports and the slots are all the list reads of it. */
function event(airports: readonly { icao: string; name: string }[], slots: readonly PublicEventSlotDto[]) {
  return { airports, slots } as unknown as PublicEventDto;
}

/** A rotation out of A and back, a slot alone landing at B, and a type the out leg admits besides its main one. */
const rotationAndAlone = [
  slot(1, 'XSM101', [18, 19], airportA, airportC, {
    flightNumber: 'XS101',
    stand: 'B12',
    rotation: 'R1',
    leg: 1,
    aircraftTypes: ['XA20', 'XA21'],
  }),
  slot(2, 'XSM300', [17, 20], airportC, airportB, { isArrival: true, taken: true }),
  slot(3, 'XSM102', [20, 21], airportC, airportA, {
    flightNumber: 'XS102',
    rotation: 'R1',
    leg: 2,
    isArrival: true,
  }),
];

function draw(slots: readonly PublicEventSlotDto[], airports = [airportA, airportB]) {
  return renderWithProviders(
    <TooltipProvider>
      <EventSlots event={event(airports, slots)} />
    </TooltipProvider>,
    { i18n },
  );
}

describe('the slots on the page of an event', () => {
  test('each airport of the event has its section, with its departures and its arrivals apart', () => {
    draw(rotationAndAlone);

    const atA = screen.getByRole('region', { name: 'XXAA · Smoke Airport A' });
    const departuresA = within(atA).getByRole('table', { name: words.departures });
    const arrivalsA = within(atA).getByRole('table', { name: words.arrivals });
    // The leg out among the departures, the leg back among the arrivals: each in its own table.
    expect(within(departuresA).getAllByRole('row')).toHaveLength(2);
    expect(within(departuresA).getByRole('button', { name: 'XSM101' })).toBeInTheDocument();
    expect(within(arrivalsA).getByRole('button', { name: 'XSM102' })).toBeInTheDocument();

    // B has only an arrival: no table of departures, and the flight from C landing there, taken.
    const atB = screen.getByRole('region', { name: 'XXBB · Smoke Airport B' });
    expect(within(atB).queryByRole('table', { name: words.departures })).not.toBeInTheDocument();
    const arrivalsB = within(atB).getByRole('table', { name: words.arrivals });
    expect(within(arrivalsB).getByRole('button', { name: 'XSM300' })).toBeInTheDocument();
    expect(within(arrivalsB).getByText(words.taken)).toBeInTheDocument();
    // The other airport, and the time at the event's: the on block of an arrival.
    expect(within(arrivalsB).getByText('Smoke Airport C', { exact: false })).toBeInTheDocument();
    expect(within(arrivalsB).getByText('20:00')).toBeInTheDocument();
  });

  test('with one airport the two tables stand alone, without a section', () => {
    draw([rotationAndAlone[0]!, rotationAndAlone[2]!], [airportA]);

    expect(screen.queryByRole('region', { name: 'XXAA · Smoke Airport A' })).not.toBeInTheDocument();
    expect(screen.getByRole('table', { name: words.departures })).toBeInTheDocument();
    expect(screen.getByRole('table', { name: words.arrivals })).toBeInTheDocument();
  });

  test('the type column shows the main type, and the others on focus and on a tap', async () => {
    draw(rotationAndAlone);

    const departures = screen.getAllByRole('table', { name: words.departures })[0]!;
    const types = within(departures).getByRole('button', { name: /XA20/ });
    expect(types).toHaveTextContent('XA20+1');
    expect(within(departures).queryByText('XA21')).not.toBeInTheDocument();

    // The keyboard: on focus, read with the button.
    act(() => types.focus());
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Also admitted: XA21');
    act(() => types.blur());
    await waitFor(() => expect(screen.queryByRole('tooltip')).not.toBeInTheDocument());

    // A tap, which a phone has instead of a hover, turns it over: open, then closed. What Radix adds around a real tap — closing
    // at the press, and at a tap anywhere — is the full round's to show in a browser.
    fireEvent.click(types);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Also admitted: XA21');
    fireEvent.click(types);
    await waitFor(() => expect(screen.queryByRole('tooltip')).not.toBeInTheDocument());

    // Neither opens the slot.
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  test('a press that never became a click is forgotten, and the keyboard turns over what is shown', async () => {
    draw(rotationAndAlone);
    const types = within(screen.getAllByRole('table', { name: words.departures })[0]!).getByRole('button', {
      name: /XA20/,
    });

    // A press begun on the button while nothing was shown, and let go elsewhere — no click —; then the focus, which shows it.
    fireEvent.pointerDown(types);
    fireEvent.pointerUp(document);
    act(() => types.focus());
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Also admitted: XA21');

    // Enter on the button: a click without a press (detail 0) closes what is shown, whatever the old press saw.
    fireEvent.click(types, { detail: 0 });
    await waitFor(() => expect(screen.queryByRole('tooltip')).not.toBeInTheDocument());
  });

  test('a leg of a rotation is marked with an icon that says so, once', async () => {
    draw(rotationAndAlone);

    const marks = screen.getAllByRole('button', { name: words.rotationHint });
    // The two legs of R1, not the slot alone.
    expect(marks).toHaveLength(2);

    fireEvent.click(marks[0]!);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(words.rotationHint);
    // The sentence is the button's name: the tooltip that shows it is not its description too, or a reader would hear it twice.
    expect(marks[0]).not.toHaveAttribute('aria-describedby');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  test('closing the slot gives the focus back to the callsign that opened it', async () => {
    const user = userEvent.setup();
    draw(rotationAndAlone);

    // Opened by its callsign, from the keyboard's side.
    const callsign = screen.getByRole('button', { name: 'XSM300' });
    await user.click(callsign);
    expect(await screen.findByRole('dialog', { name: 'XSM300' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await waitFor(() => expect(callsign).toHaveFocus());

    // Opened by a click on its row: back to the callsign of that row.
    await user.click(screen.getByText('B12'));
    expect(await screen.findByRole('dialog', { name: 'XSM101 · XS101' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.getByRole('button', { name: 'XSM101' })).toHaveFocus());
  });

  test('a click that ends selecting some text in a row opens nothing', () => {
    draw(rotationAndAlone);
    const stand = screen.getByText('B12');

    // A selection takes a range only when it holds none: a click of an earlier test may have left a caret.
    const range = document.createRange();
    range.selectNodeContents(stand);
    window.getSelection()?.removeAllRanges();
    window.getSelection()?.addRange(range);
    fireEvent.click(stand);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    window.getSelection()?.removeAllRanges();
    fireEvent.click(stand);
    expect(screen.getByRole('dialog', { name: 'XSM101 · XS101' })).toBeInTheDocument();
  });

  test('a row opens the slot read only, with every type it admits and the legs of its rotation', async () => {
    const user = userEvent.setup();
    draw(rotationAndAlone);

    // The row, not only its button: the stand of the leg out.
    await user.click(screen.getByText('B12'));
    const detail = await screen.findByRole('dialog', { name: 'XSM101 · XS101' });
    expect(within(detail).getByText('XA20')).toBeInTheDocument();
    expect(within(detail).getByText('XA21')).toBeInTheDocument();
    expect(within(detail).getByText(`(${words.detail.main})`)).toBeInTheDocument();

    const legs = within(detail).getByRole('region', { name: 'The legs of rotation R1' });
    const items = within(legs).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('XSM101');
    expect(items[0]).toHaveAttribute('aria-current', 'true');
    expect(items[1]).toHaveTextContent('XSM102');
    expect(items[1]).not.toHaveAttribute('aria-current');

    // Closed, and the slot alone opens with no legs.
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'XSM300' }));
    const alone = await screen.findByRole('dialog', { name: 'XSM300' });
    expect(within(alone).queryByRole('region')).not.toBeInTheDocument();
    expect(within(alone).getByText(words.taken)).toBeInTheDocument();
  });
});
