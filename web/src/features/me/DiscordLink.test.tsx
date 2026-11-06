import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';

import type { DiscordRefusal } from './DiscordLink';

const api = vi.hoisted(() => ({ get: vi.fn(), delete: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get, DELETE: api.delete },
}));

const { DiscordLink } = await import('./DiscordLink');

const linked = { available: true, username: 'member.on.discord', linkedAt: '2026-11-05T20:00:00Z' };

function answer(data: { available: boolean; username: string | null; linkedAt: string | null }) {
  api.get.mockResolvedValue({ data, response: new Response() });
}

function draw(refusal?: DiscordRefusal) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <DiscordLink refusal={refusal} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  api.get.mockReset();
  api.delete.mockReset();
});

test('an installation without Discord draws nothing for a member with no link', async () => {
  answer({ available: false, username: null, linkedAt: null });
  const { container } = draw();

  await waitFor(() => {
    expect(api.get).toHaveBeenCalled();
  });
  expect(container).toBeEmptyDOMElement();
});

test('a member with no link is offered the round trip, as a full navigation', async () => {
  answer({ available: true, username: null, linkedAt: null });
  draw();

  expect(await screen.findByRole('link', { name: 'discord.link' })).toHaveAttribute('href', '/auth/discord');
  expect(screen.queryByRole('button', { name: 'discord.unlink' })).not.toBeInTheDocument();
});

test('a linked member sees the name, and can update it or leave', async () => {
  answer(linked);
  draw();

  expect(await screen.findByText('member.on.discord')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'discord.refresh' })).toHaveAttribute('href', '/auth/discord');
  expect(screen.getByRole('button', { name: 'discord.unlink' })).toBeInTheDocument();
});

test('somebody linked before the application went away can still leave, and is offered nothing else', async () => {
  answer({ ...linked, available: false });
  draw();

  expect(await screen.findByRole('button', { name: 'discord.unlink' })).toBeInTheDocument();
  expect(screen.queryByRole('link')).not.toBeInTheDocument();
});

test('leaving asks the server, and draws what the server says afterwards', async () => {
  answer(linked);
  api.delete.mockResolvedValue({ response: new Response(null, { status: 204 }) });
  draw();

  const unlink = await screen.findByRole('button', { name: 'discord.unlink' });
  answer({ available: true, username: null, linkedAt: null });
  await userEvent.click(unlink);

  expect(api.delete).toHaveBeenCalledWith('/api/me/discord');
  expect(await screen.findByRole('link', { name: 'discord.link' })).toBeInTheDocument();
  expect(screen.queryByText('member.on.discord')).not.toBeInTheDocument();
});

test.each([429, 409])('a %i on leaving says so, and the name and the button stay', async (status) => {
  answer(linked);
  api.delete.mockResolvedValue({
    error: { title: 'Refused', status },
    response: new Response(null, { status }),
  });
  draw();

  await userEvent.click(await screen.findByRole('button', { name: 'discord.unlink' }));

  expect(await screen.findByRole('alert')).toBeInTheDocument();
  expect(screen.getByText('member.on.discord')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'discord.unlink' })).toBeEnabled();
});

test('a refusal still shows when the link could not be read', async () => {
  api.get.mockResolvedValue({
    error: { title: 'Down', status: 500 },
    response: new Response(null, { status: 500 }),
  });
  draw('failed');

  expect(await screen.findByText('discord.refused.failed')).toBeInTheDocument();
  await waitFor(() => {
    expect(api.get).toHaveBeenCalled();
  });
  expect(screen.getByText('discord.refused.failed')).toBeInTheDocument();
  expect(screen.queryByRole('link')).not.toBeInTheDocument();
  expect(screen.queryByRole('button')).not.toBeInTheDocument();
});

test('with the link not read and nothing to say, it draws nothing', async () => {
  api.get.mockResolvedValue({
    error: { title: 'Down', status: 500 },
    response: new Response(null, { status: 500 }),
  });
  const { container } = draw();

  await waitFor(() => {
    expect(api.get).toHaveBeenCalled();
  });
  expect(container).toBeEmptyDOMElement();
});

test('on an installation without Discord an address that names a refusal says nothing', async () => {
  answer({ available: false, username: null, linkedAt: null });
  const { container } = draw('failed');

  await waitFor(() => {
    expect(container).toBeEmptyDOMElement();
  });
});

test('a refused round trip says why, until the member has left the account they had', async () => {
  answer(linked);
  api.delete.mockResolvedValue({ response: new Response(null, { status: 204 }) });
  draw('taken');

  expect(await screen.findByText('discord.refused.taken')).toBeInTheDocument();

  answer({ available: true, username: null, linkedAt: null });
  await userEvent.click(await screen.findByRole('button', { name: 'discord.unlink' }));

  await waitFor(() => {
    expect(screen.queryByText('discord.refused.taken')).not.toBeInTheDocument();
  });
});
