import { TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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
  // Saved: nothing draws the form again, and a redraw would read the value the server now keeps.
  await new Promise((resolve) => setTimeout(resolve, 50));
  expect(
    screen.getByRole('switch', { name: englishEvents.mine.reports.auto.fields.autoReport }),
  ).toHaveAttribute('aria-checked', 'true');
});
