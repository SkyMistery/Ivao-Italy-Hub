import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18next from 'i18next';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { beforeEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../../locales/en/common.json';
import englishErrors from '../../../../../locales/en/errors.json';
import englishFlightOps from '../locales/en/flightops.json';
import type { LegDto, TourDetailDto, TourLegsDto } from '../api';

/**
 * The leg editor (M2, T7a) against an API that answers what the test says: a leg added after another, filled in as
 * the one that follows it, and saved where it was put; a leg duplicated; a leg with reports retired with a reason the
 * dialog asks for, after the server said it would be retired; the reason a leg with reports asks for when it changes;
 * a refusal under its cell. The numbers are always the server's: the grid never computes one.
 */

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get, POST: api.post, PUT: api.put },
}));

const { LegGrid } = await import('./LegGrid');

const legs = englishFlightOps.legs;
const tour = { id: 5, kind: 'Sequential', ownerDepartment: 'FOD' } as unknown as TourDetailDto;

function leg(id: number, number: number, from: string, to: string, extra: Partial<LegDto> = {}): LegDto {
  return {
    id,
    tourId: 5,
    number,
    kind: 'Normal',
    rotationId: null,
    seqInRotation: null,
    departureIcao: from,
    departureIata: null,
    arrivalIcao: to,
    arrivalIata: null,
    distanceNm: 100,
    estimatedMinutes: null,
    realCallsign: null,
    flightNumber: null,
    aircraft: { types: [], groupIds: [] },
    releaseAt: null,
    retiredAt: null,
    retiredReason: null,
    hasReports: false,
    updatedAt: '2026-09-18T10:00:00Z',
    rowVersion: `2026-09-18T10:00:0${id % 10}Z`,
    ...extra,
  };
}

function grid(...rows: LegDto[]): TourLegsDto {
  return { tourId: 5, legs: rows, totalNm: rows.length * 100, totalEstimatedMinutes: null };
}

const ok = <T,>(data: T) => ({ data, response: new Response(null, { status: 200 }) });

const initial = grid(leg(11, 1, 'AAAA', 'BBBB'), leg(12, 2, 'BBBB', 'CCCC', { hasReports: true }));

beforeEach(() => {
  api.get.mockReset();
  api.post.mockReset();
  api.put.mockReset();

  api.get.mockImplementation((path: string) => {
    if (path === '/api/flightops/tours/{tourId}/legs') {
      return Promise.resolve(ok(initial));
    }

    if (path === '/api/flightops/tours/{tourId}/legs/{legId}/removal') {
      return Promise.resolve(ok({ outcome: 'Retire', numbers: [2] }));
    }

    // The airports a cell offers while somebody types: none here.
    return Promise.resolve(ok([]));
  });
});

function draw() {
  const i18n = i18next.createInstance();
  void i18n.use(initReactI18next).init({
    lng: 'en',
    ns: ['common', 'errors', 'flightops'],
    defaultNS: 'common',
    fallbackNS: 'errors',
    resources: { en: { common: englishCommon, errors: englishErrors, flightops: englishFlightOps } },
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
  });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={client}>
        <LegGrid tour={tour} editable />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

/** A row of the table by its name: "Leg 2", or "Not saved" for a row the server has not numbered yet. */
async function row(number: string) {
  const name = number === '+' ? legs.state.new : legs.row.replace('{{number}}', number);
  return within(await screen.findByRole('row', { name }));
}

test('a leg is added after another as the one that follows it, and saved where it was put', async () => {
  const user = userEvent.setup();
  draw();

  await user.click((await row('1')).getByRole('button', { name: legs.actions.addAfter }));
  await user.click(await screen.findByRole('menuitem', { name: legs.actions.follows }));

  // A new row, departing from the arrival of the one it follows.
  const fresh = await row('+');
  expect(fresh.getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue('BBBB');
  await user.type(fresh.getByRole('combobox', { name: legs.fields.arrivalIcao }), 'dddd');

  api.post.mockResolvedValue(
    ok(
      grid(
        leg(11, 1, 'AAAA', 'BBBB'),
        leg(13, 2, 'BBBB', 'DDDD'),
        leg(12, 3, 'BBBB', 'CCCC', { hasReports: true }),
      ),
    ),
  );
  await user.click(fresh.getByRole('button', { name: englishCommon.common.save }));

  await waitFor(() => expect(api.post).toHaveBeenCalled());
  const [path, request] = api.post.mock.lastCall as [
    string,
    { params: unknown; body: { departureIcao: string; arrivalIcao: string } },
  ];
  expect(path).toBe('/api/flightops/tours/{tourId}/legs');
  expect(request.params).toEqual({ path: { tourId: 5 }, query: { after: 11 } });
  expect(request.body).toMatchObject({ departureIcao: 'BBBB', arrivalIcao: 'DDDD' });

  // The server's numbers, and no row left unsaved.
  expect((await row('2')).getByRole('combobox', { name: legs.fields.arrivalIcao })).toHaveValue('DDDD');
  expect(screen.queryByText(legs.state.new)).not.toBeInTheDocument();
});

test('a duplicate carries every field of the leg it copies', async () => {
  const user = userEvent.setup();
  api.get.mockImplementation((path: string) =>
    Promise.resolve(
      ok(
        path === '/api/flightops/tours/{tourId}/legs'
          ? grid(
              leg(11, 1, 'AAAA', 'BBBB', {
                flightNumber: 'XX 100',
                aircraft: { types: ['A320'], groupIds: [] },
              }),
            )
          : [],
      ),
    ),
  );
  draw();

  await user.click((await row('1')).getByRole('button', { name: legs.actions.addAfter }));
  await user.click(await screen.findByRole('menuitem', { name: legs.actions.duplicate }));

  const fresh = await row('+');
  expect(fresh.getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue('AAAA');
  expect(fresh.getByRole('combobox', { name: legs.fields.arrivalIcao })).toHaveValue('BBBB');
  expect(fresh.getByRole('textbox', { name: legs.fields.flightNumber })).toHaveValue('XX 100');
  expect(fresh.getByRole('textbox', { name: legs.fields.aircraft })).toHaveValue('A320');
});

test('a leg with reports is retired with a reason, after the server said it would be', async () => {
  const user = userEvent.setup();
  draw();

  await user.click((await row('2')).getByRole('button', { name: legs.actions.remove }));
  const dialog = await screen.findByRole('alertdialog');

  // What the server said, and no retirement without a reason.
  expect(await within(dialog).findByText(legs.remove.Retire)).toBeVisible();
  const retire = within(dialog).getByRole('button', { name: legs.actions.retire });
  expect(retire).toBeDisabled();

  await user.type(within(dialog).getByRole('textbox', { name: legs.fields.reason }), 'Airport closed');
  api.post.mockResolvedValue(
    ok(
      grid(
        leg(11, 1, 'AAAA', 'BBBB'),
        leg(12, 2, 'BBBB', 'CCCC', {
          hasReports: true,
          retiredAt: '2026-09-18T11:00:00Z',
          retiredReason: 'Airport closed',
        }),
      ),
    ),
  );
  await user.click(retire);

  await waitFor(() =>
    expect(api.post).toHaveBeenCalledWith('/api/flightops/tours/{tourId}/legs/{legId}/remove', {
      params: { path: { tourId: 5, legId: 12 } },
      body: { reason: 'Airport closed', rowVersion: '2026-09-18T10:00:02Z' },
    }),
  );
  expect(await screen.findByText(legs.state.retired)).toBeVisible();
  expect(screen.getByText('Airport closed')).toBeVisible();
  expect(screen.getByRole('button', { name: legs.actions.restore })).toBeVisible();
});

test('a leg with reports asks why it changes, and a refusal lands under its cell', async () => {
  const user = userEvent.setup();
  draw();

  const reported = await row('2');
  expect(reported.queryByRole('textbox', { name: legs.fields.changeReason })).not.toBeInTheDocument();

  await user.clear(reported.getByRole('combobox', { name: legs.fields.arrivalIcao }));
  await user.type(reported.getByRole('combobox', { name: legs.fields.arrivalIcao }), 'ZZZZ');
  expect(reported.getByRole('textbox', { name: legs.fields.changeReason })).toBeVisible();

  api.put.mockResolvedValue({
    data: undefined,
    error: {
      status: 400,
      errors: {
        arrivalIcao: ['flightops:errors.airportUnknown'],
        changeReason: ['flightops:errors.changeReasonRequired'],
      },
    },
    response: new Response(null, { status: 400 }),
  });
  await user.click(reported.getByRole('button', { name: englishCommon.common.save }));

  expect(await reported.findByText(englishFlightOps.errors.airportUnknown)).toBeVisible();
  expect(reported.getByText(englishFlightOps.errors.changeReasonRequired)).toBeVisible();
});
