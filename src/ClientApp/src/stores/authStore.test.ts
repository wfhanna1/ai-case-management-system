import { describe, it, expect, beforeEach, vi, beforeAll } from 'vitest';
import type { User } from './authStore';

// Must mock localStorage BEFORE the store module loads (persist middleware
// captures the reference during module evaluation).
const store: Record<string, string> = {};
const localStorageMock = {
  getItem: vi.fn((key: string) => store[key] ?? null),
  setItem: vi.fn((key: string, value: string) => { store[key] = value; }),
  removeItem: vi.fn((key: string) => { delete store[key]; }),
  clear: vi.fn(() => { Object.keys(store).forEach(k => delete store[k]); }),
  get length() { return Object.keys(store).length; },
  key: vi.fn((index: number) => Object.keys(store)[index] ?? null),
};

Object.defineProperty(globalThis, 'localStorage', { value: localStorageMock, writable: true, configurable: true });

// Dynamic import after localStorage is available
let useAuthStore: typeof import('./authStore').default;

beforeAll(async () => {
  const mod = await import('./authStore');
  useAuthStore = mod.default;
});

const testUser: User = {
  id: '123',
  email: 'test@example.com',
  roles: ['Admin', 'Reviewer'],
  tenantId: 'tenant-1',
};

describe('useAuthStore', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
    useAuthStore.setState({
      user: null,
      token: null,
      refreshToken: null,
      isAuthenticated: false,
      isLoading: false,
    });
  });

  it('starts with no authenticated user', () => {
    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.token).toBeNull();
    expect(state.refreshToken).toBeNull();
    expect(state.isAuthenticated).toBe(false);
    expect(state.isLoading).toBe(false);
  });

  it('setAuth stores user and tokens in state', () => {
    useAuthStore.getState().setAuth(testUser, 'access-token', 'refresh-token');

    const state = useAuthStore.getState();
    expect(state.user).toEqual(testUser);
    expect(state.token).toBe('access-token');
    expect(state.refreshToken).toBe('refresh-token');
    expect(state.isAuthenticated).toBe(true);
  });

  it('setAuth stores token in localStorage', () => {
    useAuthStore.getState().setAuth(testUser, 'access-token', 'refresh-token');

    expect(localStorageMock.setItem).toHaveBeenCalledWith('auth_token', 'access-token');
  });

  it('clearAuth resets state', () => {
    useAuthStore.getState().setAuth(testUser, 'access-token', 'refresh-token');
    useAuthStore.getState().clearAuth();

    const state = useAuthStore.getState();
    expect(state.user).toBeNull();
    expect(state.token).toBeNull();
    expect(state.refreshToken).toBeNull();
    expect(state.isAuthenticated).toBe(false);
  });

  it('clearAuth removes token from localStorage', () => {
    useAuthStore.getState().setAuth(testUser, 'access-token', 'refresh-token');
    useAuthStore.getState().clearAuth();

    expect(localStorageMock.removeItem).toHaveBeenCalledWith('auth_token');
  });

  it('setLoading updates isLoading', () => {
    useAuthStore.getState().setLoading(true);
    expect(useAuthStore.getState().isLoading).toBe(true);

    useAuthStore.getState().setLoading(false);
    expect(useAuthStore.getState().isLoading).toBe(false);
  });

  it('hasRole returns true for matching role', () => {
    useAuthStore.getState().setAuth(testUser, 'token', 'refresh');

    expect(useAuthStore.getState().hasRole('Admin')).toBe(true);
    expect(useAuthStore.getState().hasRole('Reviewer')).toBe(true);
  });

  it('hasRole returns false for non-matching role', () => {
    useAuthStore.getState().setAuth(testUser, 'token', 'refresh');

    expect(useAuthStore.getState().hasRole('IntakeWorker')).toBe(false);
  });

  it('hasRole returns false when no user is set', () => {
    expect(useAuthStore.getState().hasRole('Admin')).toBe(false);
  });
});
