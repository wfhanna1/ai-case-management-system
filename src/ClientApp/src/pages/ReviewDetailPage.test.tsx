import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { createElement } from 'react';
import ReviewDetailPage from './ReviewDetailPage';

const mockState = {
  isAuthenticated: true,
  hasRole: vi.fn((_role: string) => false) as ReturnType<typeof vi.fn>,
};

vi.mock('@/stores/authStore', () => ({
  default: (selector?: (state: typeof mockState) => unknown) => {
    if (typeof selector === 'function') return selector(mockState);
    return mockState;
  },
}));

vi.mock('@/services/reviewService', () => ({
  getReview: vi.fn().mockResolvedValue({
    id: 'doc-1',
    tenantId: 'tenant-1',
    originalFileName: 'intake-form.pdf',
    status: 'PendingReview',
    submittedAt: '2026-01-15T10:00:00Z',
    processedAt: '2026-01-15T10:05:00Z',
    reviewedBy: null,
    reviewedAt: null,
    extractedFields: [
      { name: 'FullName', value: 'Jane Doe', confidence: 0.95, correctedValue: null },
    ],
  }),
  startReview: vi.fn(),
  correctField: vi.fn(),
  finalizeReview: vi.fn(),
  getAuditTrail: vi.fn().mockResolvedValue([]),
  getSimilarCases: vi.fn().mockResolvedValue({ items: [] }),
  getDocumentFileBlob: vi.fn().mockRejectedValue(new Error('no file')),
}));

function renderPage(docId = 'doc-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });
  return render(
    createElement(
      QueryClientProvider,
      { client: queryClient },
      createElement(
        MemoryRouter,
        { initialEntries: [`/documents/${docId}`] },
        createElement(
          Routes,
          null,
          createElement(Route, {
            path: '/documents/:id',
            element: createElement(ReviewDetailPage),
          })
        )
      )
    )
  );
}

describe('ReviewDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows Start Review button for Reviewer role', async () => {
    mockState.hasRole = vi.fn((role: string) => role === 'Reviewer');

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'intake-form.pdf' })).toBeInTheDocument();
    });

    expect(screen.getByTestId('start-review-btn')).toBeInTheDocument();
  });

  it('hides Start Review button for IntakeWorker role', async () => {
    mockState.hasRole = vi.fn((role: string) => role === 'IntakeWorker');

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'intake-form.pdf' })).toBeInTheDocument();
    });

    expect(screen.queryByTestId('start-review-btn')).not.toBeInTheDocument();
  });

  it('hides field edit buttons for IntakeWorker role when InReview', async () => {
    const { getReview } = await import('@/services/reviewService');
    vi.mocked(getReview).mockResolvedValueOnce({
      id: 'doc-1',
      tenantId: 'tenant-1',
      originalFileName: 'intake-form.pdf',
      status: 'InReview',
      submittedAt: '2026-01-15T10:00:00Z',
      processedAt: '2026-01-15T10:05:00Z',
      reviewedBy: null,
      reviewedAt: null,
      extractedFields: [
        { name: 'FullName', value: 'Jane Doe', confidence: 0.95, correctedValue: null },
      ],
    });

    mockState.hasRole = vi.fn((role: string) => role === 'IntakeWorker');

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'intake-form.pdf' })).toBeInTheDocument();
    });

    expect(screen.queryByTestId('edit-btn-FullName')).not.toBeInTheDocument();
    expect(screen.queryByTestId('finalize-btn')).not.toBeInTheDocument();
  });

  it('shows field edit buttons for Reviewer role when InReview', async () => {
    const { getReview } = await import('@/services/reviewService');
    vi.mocked(getReview).mockResolvedValueOnce({
      id: 'doc-1',
      tenantId: 'tenant-1',
      originalFileName: 'intake-form.pdf',
      status: 'InReview',
      submittedAt: '2026-01-15T10:00:00Z',
      processedAt: '2026-01-15T10:05:00Z',
      reviewedBy: null,
      reviewedAt: null,
      extractedFields: [
        { name: 'FullName', value: 'Jane Doe', confidence: 0.95, correctedValue: null },
      ],
    });

    mockState.hasRole = vi.fn((role: string) => role === 'Reviewer');

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'intake-form.pdf' })).toBeInTheDocument();
    });

    expect(screen.getByTestId('edit-btn-FullName')).toBeInTheDocument();
    expect(screen.getByTestId('finalize-btn')).toBeInTheDocument();
  });

  it('shows document info for all roles', async () => {
    mockState.hasRole = vi.fn((role: string) => role === 'IntakeWorker');

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'intake-form.pdf' })).toBeInTheDocument();
    });

    // Document info is visible
    expect(screen.getByText('Document Info')).toBeInTheDocument();
    expect(screen.getByText('Extracted Fields')).toBeInTheDocument();
    expect(screen.getByText('Jane Doe')).toBeInTheDocument();
  });
});
