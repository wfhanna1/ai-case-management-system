import { describe, it, expect, vi, beforeEach } from 'vitest';

// Create a minimal localStorage mock
const store: Record<string, string> = {};
const localStorageMock = {
  getItem: vi.fn((key: string) => store[key] ?? null),
  setItem: vi.fn((key: string, value: string) => { store[key] = value; }),
  removeItem: vi.fn((key: string) => { delete store[key]; }),
  clear: vi.fn(() => { Object.keys(store).forEach(k => delete store[k]); }),
  get length() { return Object.keys(store).length; },
  key: vi.fn((_: number) => null),
};

vi.stubGlobal('localStorage', localStorageMock);

// Import api after stubbing localStorage
import api from './api';

describe('api interceptors', () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  it('sets Authorization header from localStorage auth_token', () => {
    localStorageMock.setItem('auth_token', 'my-jwt-token');

    const handlers = (api.interceptors.request as unknown as {
      handlers: Array<{ fulfilled: (config: Record<string, unknown>) => Record<string, unknown> }>;
    }).handlers;

    const config = { headers: {} as Record<string, string> };
    const result = handlers[0].fulfilled(config);
    const headers = result.headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer my-jwt-token');
  });

  it('does not set Authorization header when no token exists', () => {
    const handlers = (api.interceptors.request as unknown as {
      handlers: Array<{ fulfilled: (config: Record<string, unknown>) => Record<string, unknown> }>;
    }).handlers;

    const config = { headers: {} as Record<string, string> };
    const result = handlers[0].fulfilled(config);
    const headers = result.headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
  });

  it('api instance has baseURL /api', () => {
    expect(api.defaults.baseURL).toBe('/api');
  });

  it('api instance has 15s timeout', () => {
    expect(api.defaults.timeout).toBe(15000);
  });

  it('api instance has JSON content type', () => {
    expect(api.defaults.headers['Content-Type']).toBe('application/json');
  });
});
