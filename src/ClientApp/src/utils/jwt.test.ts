import { describe, it, expect } from 'vitest';
import { parseJwt } from './jwt';

function encodePayload(payload: Record<string, unknown>): string {
  const json = JSON.stringify(payload);
  const base64 = btoa(json);
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function makeToken(payload: Record<string, unknown>): string {
  const header = encodePayload({ alg: 'HS256', typ: 'JWT' });
  const body = encodePayload(payload);
  return `${header}.${body}.fake-signature`;
}

describe('parseJwt', () => {
  it('parses a valid JWT payload', () => {
    const token = makeToken({ sub: '123', role: 'Admin' });
    const result = parseJwt(token);
    expect(result.sub).toBe('123');
    expect(result.role).toBe('Admin');
  });

  it('returns empty object for invalid token', () => {
    expect(parseJwt('not-a-jwt')).toEqual({});
  });

  it('returns empty object for empty string', () => {
    expect(parseJwt('')).toEqual({});
  });

  it('handles token with array claim', () => {
    const token = makeToken({ roles: ['Admin', 'Reviewer'] });
    const result = parseJwt(token);
    expect(result.roles).toEqual(['Admin', 'Reviewer']);
  });

  it('handles token with special characters in payload', () => {
    const token = makeToken({ name: 'John Doe & Friends' });
    const result = parseJwt(token);
    expect(result.name).toBe('John Doe & Friends');
  });
});
