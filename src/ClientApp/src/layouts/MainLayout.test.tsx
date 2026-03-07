import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import MainLayout from './MainLayout';

const mockAuthStore = vi.fn();

vi.mock('@/stores/authStore', () => ({
  default: (...args: unknown[]) => mockAuthStore(...args),
}));

vi.mock('@/queryClient', () => ({
  default: { clear: vi.fn() },
}));

function renderLayout() {
  const queryClient = new QueryClient();
  return render(
    createElement(
      QueryClientProvider,
      { client: queryClient },
      createElement(MemoryRouter, null, createElement(MainLayout))
    )
  );
}

describe('MainLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('when user is Admin', () => {
    beforeEach(() => {
      mockAuthStore.mockReturnValue({
        isAuthenticated: true,
        user: { email: 'admin@alpha.demo', roles: ['Admin'] },
        clearAuth: vi.fn(),
      });
    });

    it('desktop nav uses lg breakpoint', () => {
      renderLayout();
      const dashboardBtn = screen.getByRole('button', { name: /Dashboard/i });
      expect(dashboardBtn).toBeInTheDocument();
      expect(screen.getByTestId('mobile-menu-btn')).toBeInTheDocument();
    });

    it('renders all 7 nav items', () => {
      renderLayout();
      expect(screen.getByRole('button', { name: /Dashboard/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Upload/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Documents/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Templates/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Search/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Cases/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Reviews/i })).toBeInTheDocument();
    });

    it('displays user email and role chip', () => {
      renderLayout();
      expect(screen.getByText('admin@alpha.demo')).toBeInTheDocument();
      expect(screen.getByText('Admin')).toBeInTheDocument();
    });
  });

  describe('when user is IntakeWorker', () => {
    beforeEach(() => {
      mockAuthStore.mockReturnValue({
        isAuthenticated: true,
        user: { email: 'worker@alpha.demo', roles: ['IntakeWorker'] },
        clearAuth: vi.fn(),
      });
    });

    it('shows Upload but hides Reviews', () => {
      renderLayout();
      expect(screen.getByRole('button', { name: /Upload/i })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Reviews/i })).not.toBeInTheDocument();
    });

    it('shows common nav items', () => {
      renderLayout();
      expect(screen.getByRole('button', { name: /Dashboard/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Documents/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Search/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Cases/i })).toBeInTheDocument();
    });
  });

  describe('when user is Reviewer', () => {
    beforeEach(() => {
      mockAuthStore.mockReturnValue({
        isAuthenticated: true,
        user: { email: 'reviewer@alpha.demo', roles: ['Reviewer'] },
        clearAuth: vi.fn(),
      });
    });

    it('shows Reviews but hides Upload', () => {
      renderLayout();
      expect(screen.getByRole('button', { name: /Reviews/i })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Upload/i })).not.toBeInTheDocument();
    });
  });

  describe('when user is not authenticated', () => {
    beforeEach(() => {
      mockAuthStore.mockReturnValue({
        isAuthenticated: false,
        user: null,
        clearAuth: vi.fn(),
      });
    });

    it('shows Sign In button instead of nav items', () => {
      renderLayout();
      expect(screen.getByRole('button', { name: /Sign In/i })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Dashboard/i })).not.toBeInTheDocument();
    });
  });
});
