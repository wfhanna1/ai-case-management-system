import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { createElement } from 'react';
import SearchPage from './SearchPage';

vi.mock('@/services/documentService', () => ({
  searchDocuments: vi.fn().mockResolvedValue({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  }),
}));

function renderSearchPage() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
  return render(
    createElement(
      QueryClientProvider,
      { client: queryClient },
      createElement(MemoryRouter, null, createElement(SearchPage))
    )
  );
}

describe('SearchPage', () => {
  it('submits search when Enter is pressed in a filter field', async () => {
    const { searchDocuments } = await import('@/services/documentService');
    const mockSearch = vi.mocked(searchDocuments);
    mockSearch.mockClear();

    renderSearchPage();

    const fileNameInput = screen.getByTestId('search-filename').querySelector('input')!;
    await userEvent.type(fileNameInput, 'test.pdf{Enter}');

    expect(mockSearch).toHaveBeenCalled();
  });

  it('renders search filters', () => {
    renderSearchPage();

    expect(screen.getByRole('heading', { name: 'Search Documents' })).toBeInTheDocument();
    expect(screen.getByTestId('search-filename')).toBeInTheDocument();
    expect(screen.getByTestId('search-status')).toBeInTheDocument();
    expect(screen.getByTestId('search-fieldvalue')).toBeInTheDocument();
    expect(screen.getByTestId('search-from')).toBeInTheDocument();
    expect(screen.getByTestId('search-to')).toBeInTheDocument();
    expect(screen.getByTestId('search-btn')).toBeInTheDocument();
    expect(screen.getByTestId('clear-btn')).toBeInTheDocument();
  });

  it('shows prompt message before search', () => {
    renderSearchPage();

    expect(screen.getByTestId('search-prompt')).toBeInTheDocument();
    expect(screen.getByTestId('search-prompt')).toHaveTextContent('Use the filters above and click Search');
  });

  it('hides the results table before first search', () => {
    renderSearchPage();

    // Before search is triggered, the table should not be visible
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows date validation error when from is after to', async () => {
    renderSearchPage();

    const fromInput = screen.getByTestId('search-from').querySelector('input')!;
    const toInput = screen.getByTestId('search-to').querySelector('input')!;

    await userEvent.type(fromInput, '2026-03-10');
    await userEvent.type(toInput, '2026-03-05');

    expect(screen.getByText('From date must be before To date')).toBeInTheDocument();
  });

  it('disables search button when date validation fails', async () => {
    renderSearchPage();

    const fromInput = screen.getByTestId('search-from').querySelector('input')!;
    const toInput = screen.getByTestId('search-to').querySelector('input')!;

    await userEvent.type(fromInput, '2026-03-10');
    await userEvent.type(toInput, '2026-03-05');

    expect(screen.getByTestId('search-btn')).toBeDisabled();
  });
});
