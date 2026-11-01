import { TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { beforeEach, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import englishEvents from '../locales/en/events.json';

/**
 * «Report my participation in events automatically» on `/events/mine` (E14b) when its save is refused (#333, item 4): the switch goes back
 * to what the server kept, and the notice says why — never a switch turned on a value nobody saved.
 */

const api = vi.hoisted(() => ({ get: vi.fn(), put: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get, PUT: api.put },
}));

const { AutoReportSwitch } = await import('./myReports');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);

beforeEach(() => {
  api.get.mockReset();
  api.put.mockReset();
  api.get.mockResolvedValue({ data: { value: false }, response: new Response(null, { status: 200 }) });
});

function draw() {
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <TooltipProvider>
          <AutoReportSwitch />
        </TooltipProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

test('a refused save puts the switch back where the server kept it', async () => {
  api.put.mockResolvedValue({
    error: { title: 'errors.forbidden', status: 403 },
    response: new Response(null, { status: 403 }),
  });
  draw();

  const toggle = await screen.findByRole('switch', {
    name: englishEvents.mine.reports.auto.fields.autoReport,
  });
  expect(toggle).toHaveAttribute('aria-checked', 'false');

  fireEvent.click(toggle);
  expect(toggle).toHaveAttribute('aria-checked', 'true');

  await waitFor(() => expect(api.put).toHaveBeenCalledOnce());
  await waitFor(() =>
    expect(
      screen.getByRole('switch', { name: englishEvents.mine.reports.auto.fields.autoReport }),
    ).toHaveAttribute('aria-checked', 'false'),
  );
});

test('a save that passes keeps the switch on', async () => {
  api.put.mockResolvedValue({ data: { value: true }, response: new Response(null, { status: 200 }) });
  draw();

  const toggle = await screen.findByRole('switch', {
    name: englishEvents.mine.reports.auto.fields.autoReport,
  });
  fireEvent.click(toggle);

  await waitFor(() => expect(api.put).toHaveBeenCalledOnce());
  // Saved, and the answer handed over: the switch is the same one, never drawn again (that would take the focus off it).
  await act(async () => {
    await api.put.mock.results[0]?.value;
  });
  const after = screen.getByRole('switch', { name: englishEvents.mine.reports.auto.fields.autoReport });
  expect(after).toBe(toggle);
  expect(after).toHaveAttribute('aria-checked', 'true');
});

test('a save answered after the member switched back draws what the server kept', async () => {
  let answer: (value: unknown) => void = () => undefined;
  api.put.mockReturnValue(
    new Promise((resolve) => {
      answer = resolve;
    }),
  );
  draw();

  const toggle = await screen.findByRole('switch', {
    name: englishEvents.mine.reports.auto.fields.autoReport,
  });
  fireEvent.click(toggle);
  await waitFor(() => expect(api.put).toHaveBeenCalledOnce());

  // Off again while «on» is on its way: the same value the page read, so nothing is sent.
  fireEvent.click(toggle);
  expect(toggle).toHaveAttribute('aria-checked', 'false');
  // Past the live form's 150 ms (`LIVE_DELAY`), so the form has told the screen before the answer comes.
  await act(() => new Promise((resolve) => setTimeout(resolve, 300)));
  await act(async () => {
    answer({ data: { value: true }, response: new Response(null, { status: 200 }) });
    await Promise.resolve();
  });

  await waitFor(() =>
    expect(
      screen.getByRole('switch', { name: englishEvents.mine.reports.auto.fields.autoReport }),
    ).toHaveAttribute('aria-checked', 'true'),
  );
  expect(api.put).toHaveBeenCalledOnce();
});
