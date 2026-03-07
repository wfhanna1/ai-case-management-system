import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import React, { createElement } from 'react';
import ProtectedRoute from './ProtectedRoute';

const mockAuthStore = vi.fn();

vi.mock('@/stores/authStore', () => ({
  default: (...args: unknown[]) => mockAuthStore(...args),
}));

function renderWithRoute(
  requiredRole?: string,
  initialPath = '/protected'
) {
  return render(
    createElement(
      MemoryRouter,
      { initialEntries: [initialPath] },
      createElement(
        Routes,
        null,
        createElement(Route, {
          path: '/protected',
          element: createElement(
            ProtectedRoute,
            { requiredRole } as React.ComponentProps<typeof ProtectedRoute>,
            createElement('div', { 'data-testid': 'protected-content' }, 'Protected Content')
          ),
        }),
        createElement(Route, {
          path: '/login',
          element: createElement('div', { 'data-testid': 'login-page' }, 'Login Page'),
        }),
        createElement(Route, {
          path: '/dashboard',
          element: createElement('div', { 'data-testid': 'dashboard-page' }, 'Dashboard Page'),
        })
      )
    )
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('redirects to /login when not authenticated', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: false,
      hasRole: () => false,
    });

    renderWithRoute();

    expect(screen.getByTestId('login-page')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });

  it('renders children when authenticated and no role required', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: () => false,
    });

    renderWithRoute();

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });

  it('renders children when user has the required role', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: (role: string) => role === 'Reviewer',
    });

    renderWithRoute('Reviewer');

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });

  it('redirects to /dashboard when user lacks the required role', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: (role: string) => role === 'IntakeWorker',
    });

    renderWithRoute('Reviewer');

    expect(screen.getByTestId('dashboard-page')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });

  it('renders children when user is Admin regardless of required role', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: (role: string) => role === 'Admin',
    });

    renderWithRoute('Reviewer');

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });

  it('redirects to /dashboard when user has no roles and role is required', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: () => false,
    });

    renderWithRoute('IntakeWorker');

    expect(screen.getByTestId('dashboard-page')).toBeInTheDocument();
  });

  it('renders children when requiredRole is Admin and user is Admin', () => {
    mockAuthStore.mockReturnValue({
      isAuthenticated: true,
      hasRole: (role: string) => role === 'Admin',
    });

    renderWithRoute('Admin');

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });
});
