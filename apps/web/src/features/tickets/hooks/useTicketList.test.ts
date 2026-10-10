import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  // DEFECT-117: the loading effect ignored every filter except search and page.
  describe('DEFECT-117', () => {
    const settle = () =>
      act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });

    it('requests immediately when the status changes, without waiting for a search', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();
      getTickets.mockClear();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await settle();

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'InProgress' }));
    });

    it.each([
      ['priority', { priority: 'High' }],
      ['categoryId', { categoryId: 3 }],
      ['customerId', { customerId: 2 }],
      ['assignedAgentId', { assignedAgentId: 4 }],
      ['unassignedOnly', { unassignedOnly: true }],
      ['slaStatus', { slaStatus: 'AtRisk' }],
      ['sort', { sortBy: 'dueAtUtc', sortDirection: 'asc' }],
    ] as const)('requests immediately when %s changes', async (_name, patch) => {
      const { result } = renderHook(() => useTicketList());
      await settle();
      getTickets.mockClear();

      act(() => {
        result.current.updateFilters(patch);
      });
      await settle();

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
    });

    it('sends the status chosen earlier, and only one request, once the search settles', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      act(() => {
        result.current.updateFilters({ status: 'InProgress' });
      });
      await settle();
      getTickets.mockClear();

      for (const text of ['i', 'in', 'inv']) {
        act(() => {
          result.current.updateFilters({ search: text });
        });
        await act(async () => {
          await vi.advanceTimersByTimeAsync(100);
        });
      }

      // Still typing: no request per keystroke.
      expect(getTickets).not.toHaveBeenCalled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'inv', status: 'InProgress' }),
      );
    });

    it('keeps the typed text in the search box while the request is still pending', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });

      expect(result.current.filters.search).toBe('invoice');
    });

    it('clears everything at once when the filters are reset', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      act(() => {
        result.current.updateFilters({ status: 'Open', search: 'invoice' });
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });
      getTickets.mockClear();

      act(() => {
        result.current.resetFilters();
      });
      await act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: '', status: undefined }),
      );
    });
  });
});
