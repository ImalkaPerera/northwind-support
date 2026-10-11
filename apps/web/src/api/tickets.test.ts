import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../features/tickets/hooks/useTicketList';
import { page } from '../test/fixtures';
import { ticketsApi } from './tickets';

const fetchMock = vi.fn();

function lastRequest() {
  const [url, init] = fetchMock.mock.calls[fetchMock.mock.calls.length - 1] as [string, RequestInit | undefined];

  return { url, init };
}

describe('ticketsApi', () => {
  beforeEach(() => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify(page([])), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  it('sends the SLA status as a query parameter', async () => {
    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: 'AtRisk', priority: 'High' });

    const { url } = lastRequest();

    expect(url).toContain('slaStatus=AtRisk');
    expect(url).toContain('priority=High');
  });

  it('leaves the SLA status out when no filter is chosen', async () => {
    await ticketsApi.getTickets(emptyFilters);

    expect(lastRequest().url).not.toContain('slaStatus');
  });

  it('posts an escalation to the ticket', async () => {
    fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));

    await ticketsApi.escalateTicket(12, { reason: 'Customer is losing money' });

    const { url, init } = lastRequest();

    expect(url).toMatch(/\/tickets\/12\/escalate$/);
    expect(init?.method).toBe('POST');
    // Who is escalating is never in the body: the server takes it from the token.
    expect(JSON.parse(String(init?.body))).toEqual({ reason: 'Customer is losing money' });
  });

  it('reads the escalation history of a ticket', async () => {
    fetchMock.mockResolvedValue(new Response('[]', { status: 200 }));

    await ticketsApi.getEscalations(12);

    expect(lastRequest().url).toMatch(/\/tickets\/12\/escalations$/);
  });
});
