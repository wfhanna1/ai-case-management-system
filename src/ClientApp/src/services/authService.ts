import axios from 'axios';
import api from './api';

export interface AuthResponse {
  data?: {
    userId: string;
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
  };
  error?: { code: string; message: string; details?: Record<string, string[]> };
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface RegisterPayload {
  tenantId: string;
  email: string;
  password: string;
  roles: string[];
}

export interface RefreshPayload {
  userId: string;
  tenantId: string;
  refreshToken: string;
}

export const DEMO_TENANTS = [
  { id: 'a1b2c3d4-0000-0000-0000-000000000001', name: 'Alpha Clinic' },
  { id: 'b2c3d4e5-0000-0000-0000-000000000002', name: 'Beta Hospital' },
];

export async function login(payload: LoginPayload): Promise<AuthResponse> {
  try {
    const res = await api.post<AuthResponse>('/auth/login', payload);
    return res.data;
  } catch (err: unknown) {
    if (axios.isAxiosError(err) && err.response?.data) {
      return err.response.data as AuthResponse;
    }
    throw err;
  }
}

export async function register(payload: RegisterPayload): Promise<AuthResponse> {
  try {
    const res = await api.post<AuthResponse>('/auth/register', payload);
    return res.data;
  } catch (err: unknown) {
    if (axios.isAxiosError(err) && err.response?.data) {
      return err.response.data as AuthResponse;
    }
    throw err;
  }
}

export async function refreshToken(payload: RefreshPayload): Promise<AuthResponse> {
  const res = await api.post<AuthResponse>('/auth/refresh', payload);
  return res.data;
}
