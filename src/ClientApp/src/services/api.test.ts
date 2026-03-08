import { describe, it, expect, vi, beforeEach } from 'vitest';
import axios from 'axios';

// Create a minimal localStorage mock
const store: Record<string, string> = {};
const localStorageMock = {
  getItem: vi.fn((key: string) => store[key] ?? null),
  setItem: vi.fn((key: string, value: string) => { store[key] = value; }),
  removeItem: vi.fn((key: string) => { delete store[key]; }),
  clear: vi.fn(() => { Object.keys(store).forEach(k => delete store[k]); }),
  get length() { return Object.keys(store).length; },
  key: vi.fn(() => null),
};

vi.stubGlobal('localStorage', localStorageMock);

// Import api after stubbing localStorage
import api from './api';

// Extract response interceptor error handler for direct testing
type InterceptorHandler = {
  fulfilled: ((value: unknown) => unknown) | null;
  rejected: ((error: unknown) => unknown) | null;
};

function getResponseErrorHandler() {
  const handlers = (api.interceptors.response as unknown as {
    handlers: InterceptorHandler[];
  }).handlers;
  return handlers[0].rejected!;
}

function getRequestFulfilledHandler() {
  const handlers = (api.interceptors.request as unknown as {
    handlers: InterceptorHandler[];
  }).handlers;
  return handlers[0].fulfilled!;
}

describe('api interceptors', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
    // Reset window.location.href
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { href: 'http://localhost/' },
    });
  });

  describe('request interceptor', () => {
    it('sets Authorization header from localStorage auth_token', () => {
      localStorageMock.setItem('auth_token', 'my-jwt-token');

      const handler = getRequestFulfilledHandler();
      const config = { headers: {} as Record<string, string> };
      const result = handler(config) as { headers: Record<string, string> };
      expect(result.headers.Authorization).toBe('Bearer my-jwt-token');
    });

    it('does not set Authorization header when no token exists', () => {
      const handler = getRequestFulfilledHandler();
      const config = { headers: {} as Record<string, string> };
      const result = handler(config) as { headers: Record<string, string> };
      expect(result.headers.Authorization).toBeUndefined();
    });
  });

  describe('response interceptor (401 handling)', () => {
    it('clears auth and redirects to /login when 401 and no auth-storage', async () => {
      const errorHandler = getResponseErrorHandler();

      const error = {
        config: { url: '/documents', _retry: false, headers: {} },
        response: { status: 401 },
      };

      await expect(errorHandler(error)).rejects.toBeDefined();
      expect(localStorageMock.removeItem).toHaveBeenCalledWith('auth_token');
      expect(localStorageMock.removeItem).toHaveBeenCalledWith('auth-storage');
      expect(window.location.href).toBe('/login');
    });

    it('skips refresh for auth endpoints and rejects', async () => {
      const errorHandler = getResponseErrorHandler();

      const error = {
        config: { url: '/auth/login', _retry: false, headers: {} },
        response: { status: 401 },
      };

      await expect(errorHandler(error)).rejects.toBeDefined();
      // Should NOT redirect -- auth endpoints handle their own errors
      expect(localStorageMock.removeItem).not.toHaveBeenCalled();
    });

    it('does not retry if _retry is already true', async () => {
      const errorHandler = getResponseErrorHandler();

      const error = {
        config: { url: '/documents', _retry: true, headers: {} },
        response: { status: 401 },
      };

      await expect(errorHandler(error)).rejects.toBeDefined();
      // Should not redirect since the first clause (_retry check) fails
      expect(localStorageMock.removeItem).not.toHaveBeenCalled();
    });

    it('attempts refresh when auth-storage exists with valid data', async () => {
      const errorHandler = getResponseErrorHandler();

      const authState = {
        state: {
          user: { id: 'user-1', tenantId: 'tenant-1' },
          refreshToken: 'old-refresh-token',
        },
      };
      localStorageMock.setItem('auth-storage', JSON.stringify(authState));

      // Mock the refresh call to fail
      const postSpy = vi.spyOn(axios, 'post').mockRejectedValueOnce(new Error('refresh failed'));

      const error = {
        config: { url: '/documents', _retry: false, headers: {} },
        response: { status: 401 },
      };

      await expect(errorHandler(error)).rejects.toBeDefined();

      // Should have attempted refresh
      expect(postSpy).toHaveBeenCalledWith('/api/auth/refresh', {
        userId: 'user-1',
        tenantId: 'tenant-1',
        refreshToken: 'old-refresh-token',
      });

      // Refresh failed, so should redirect
      expect(window.location.href).toBe('/login');

      postSpy.mockRestore();
    });

    it('retries original request with new token after successful refresh', async () => {
      const errorHandler = getResponseErrorHandler();

      const authState = {
        state: {
          user: { id: 'user-1', tenantId: 'tenant-1' },
          refreshToken: 'old-refresh-token',
          token: 'old-token',
        },
      };
      localStorageMock.setItem('auth-storage', JSON.stringify(authState));

      // Mock successful refresh
      const postSpy = vi.spyOn(axios, 'post').mockResolvedValueOnce({
        data: {
          data: {
            accessToken: 'new-access-token',
            refreshToken: 'new-refresh-token',
          },
        },
      });

      const originalConfig = {
        url: '/documents',
        _retry: false,
        headers: {} as Record<string, string>,
      };

      const error = {
        config: originalConfig,
        response: { status: 401 },
      };

      // The interceptor calls api(originalRequest) to retry -- mock that
      // Since we can't easily mock the api instance call, we verify the
      // token was updated in localStorage and the config was modified
      try {
        await errorHandler(error);
      } catch {
        // The retry will fail since there's no real server, but we can
        // verify the token was stored
      }

      expect(postSpy).toHaveBeenCalled();
      // Verify new token was saved
      expect(localStorageMock.setItem).toHaveBeenCalledWith('auth_token', 'new-access-token');
      // Verify auth-storage was updated
      const updatedStorage = JSON.parse(store['auth-storage'] || '{}');
      expect(updatedStorage.state.token).toBe('new-access-token');
      expect(updatedStorage.state.refreshToken).toBe('new-refresh-token');

      postSpy.mockRestore();
    });

    it('passes through non-401 errors without interception', async () => {
      const errorHandler = getResponseErrorHandler();

      const error = {
        config: { url: '/documents', headers: {} },
        response: { status: 500 },
      };

      await expect(errorHandler(error)).rejects.toBeDefined();
      expect(localStorageMock.removeItem).not.toHaveBeenCalled();
    });
  });

  describe('api instance config', () => {
    it('has baseURL /api', () => {
      expect(api.defaults.baseURL).toBe('/api');
    });

    it('has 15s timeout', () => {
      expect(api.defaults.timeout).toBe(15000);
    });

    it('has JSON content type', () => {
      expect(api.defaults.headers['Content-Type']).toBe('application/json');
    });
  });
});
