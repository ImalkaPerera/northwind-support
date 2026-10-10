import { useCallback, useEffect, useMemo, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketFilters } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

/**
 * Owns the ticket list's filters and the data they produce.
 *
 * Every filter except the search text is applied the moment it changes. The search text is the
 * only thing debounced, so typing does not send a request per keystroke.
 */
export function useTicketList() {
  // What the last request was (or will be) made with.
  const [appliedFilters, setAppliedFilters] = useState<TicketFilters>(emptyFilters);
  // What is in the search box right now, which can be ahead of appliedFilters.search.
  const [searchText, setSearchText] = useState(emptyFilters.search);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setAppliedFilters((current) =>
        current.search === searchText ? current : { ...current, search: searchText, page: 1 },
      );
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [searchText]);

  const load = useCallback(() => ticketsApi.getTickets(appliedFilters), [appliedFilters]);
  const { data, error, isLoading, reload } = useAsyncData(load, 'Unable to load tickets.');

  // The box shows what was typed; the request uses appliedFilters.
  const filters = useMemo(
    () => ({ ...appliedFilters, search: searchText }),
    [appliedFilters, searchText],
  );

  const updateFilters = useCallback((patch: Partial<TicketFilters>) => {
    const { search, ...rest } = patch;

    if (search !== undefined) {
      setSearchText(search);
    }

    if (Object.keys(rest).length > 0) {
      setAppliedFilters((current) => ({ ...current, ...rest, page: rest.page ?? 1 }));
    }
  }, []);

  const resetFilters = useCallback(() => {
    setSearchText(emptyFilters.search);
    setAppliedFilters(emptyFilters);
  }, []);

  return { filters, updateFilters, resetFilters, reload, isLoading, data, error };
}
