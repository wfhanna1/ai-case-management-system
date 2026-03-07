import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { useDocumentSearch } from './useDocumentSearch';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
  return {
    queryClient,
    wrapper: ({ children }: { children: React.ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children),
  };
}

vi.mock('@/services/documentService', () => ({
  searchDocuments: vi.fn().mockResolvedValue({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  }),
}));

describe('useDocumentSearch', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('starts with searchTriggered false and query disabled', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    expect(result.current.searchTriggered).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });

  it('handleSearch sets searchTriggered to true', async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.handleSearch();
    });

    expect(result.current.searchTriggered).toBe(true);
  });

  it('handleSearch resets page to 0', async () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    // Change page first
    act(() => {
      result.current.setPage(2);
    });
    expect(result.current.page).toBe(2);

    // handleSearch should reset to 0
    act(() => {
      result.current.handleSearch();
    });
    expect(result.current.page).toBe(0);
  });

  it('handleClear resets all filters and searchTriggered', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    // Set some filters
    act(() => {
      result.current.setFileName('test.pdf');
      result.current.setStatus('Submitted');
      result.current.setFieldValue('some value');
      result.current.setFromDate('2026-01-01');
      result.current.setToDate('2026-03-01');
      result.current.handleSearch();
    });

    expect(result.current.searchTriggered).toBe(true);
    expect(result.current.fileName).toBe('test.pdf');

    act(() => {
      result.current.handleClear();
    });

    expect(result.current.searchTriggered).toBe(false);
    expect(result.current.fileName).toBe('');
    expect(result.current.status).toBe('');
    expect(result.current.fieldValue).toBe('');
    expect(result.current.fromDate).toBe('');
    expect(result.current.toDate).toBe('');
    expect(result.current.page).toBe(0);
  });

  it('handleClear removes cached query data', () => {
    const { queryClient, wrapper } = createWrapper();
    const removeSpy = vi.spyOn(queryClient, 'removeQueries');

    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.handleSearch();
    });

    act(() => {
      result.current.handleClear();
    });

    expect(removeSpy).toHaveBeenCalledWith({ queryKey: ['searchDocuments'] });
  });

  it('changing filters does not trigger a new query when search was already triggered', async () => {
    const { searchDocuments } = await import('@/services/documentService');
    const mockSearch = vi.mocked(searchDocuments);
    mockSearch.mockClear();

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    // Trigger first search
    act(() => {
      result.current.handleSearch();
    });

    const callCountAfterSearch = mockSearch.mock.calls.length;

    // Change a filter -- should NOT cause a new fetch because params are frozen until next handleSearch
    act(() => {
      result.current.setFileName('newfile.pdf');
    });

    // The query key should use the committed params, not the live filter state
    // so the call count should not increase
    expect(mockSearch.mock.calls.length).toBe(callCountAfterSearch);
  });

  it('exposes filter setters', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setFileName('doc.pdf');
    });
    expect(result.current.fileName).toBe('doc.pdf');

    act(() => {
      result.current.setStatus('Completed');
    });
    expect(result.current.status).toBe('Completed');

    act(() => {
      result.current.setFieldValue('value');
    });
    expect(result.current.fieldValue).toBe('value');

    act(() => {
      result.current.setFromDate('2026-01-01');
    });
    expect(result.current.fromDate).toBe('2026-01-01');

    act(() => {
      result.current.setToDate('2026-03-01');
    });
    expect(result.current.toDate).toBe('2026-03-01');
  });

  it('setPage and setPageSize update pagination', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setPage(3);
    });
    expect(result.current.page).toBe(3);

    act(() => {
      result.current.setPageSize(50);
    });
    expect(result.current.pageSize).toBe(50);
    // Changing page size should reset page to 0
    expect(result.current.page).toBe(0);
  });

  it('builds params with end-of-day for toDate', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setToDate('2026-03-06');
    });

    act(() => {
      result.current.handleSearch();
    });

    // The committed params should include end-of-day time
    const params = result.current.committedParams;
    expect(params.to).toContain('2026-03-06T23:59:59');
  });

  it('date validation: returns error when fromDate is after toDate', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setFromDate('2026-03-10');
      result.current.setToDate('2026-03-05');
    });

    expect(result.current.dateError).toBe('From date must be before To date');
  });

  it('date validation: returns null when dates are valid', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setFromDate('2026-03-01');
      result.current.setToDate('2026-03-05');
    });

    expect(result.current.dateError).toBeNull();
  });

  it('date validation: returns null when only one date is set', () => {
    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useDocumentSearch(), { wrapper });

    act(() => {
      result.current.setFromDate('2026-03-10');
    });

    expect(result.current.dateError).toBeNull();
  });
});
