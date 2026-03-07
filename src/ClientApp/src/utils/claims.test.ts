import { describe, it, expect } from 'vitest';
import { extractRoles } from './claims';

describe('extractRoles', () => {
  it('extracts roles from short "role" claim as array', () => {
    const claims = { role: ['Admin', 'IntakeWorker'] };
    expect(extractRoles(claims)).toEqual(['Admin', 'IntakeWorker']);
  });

  it('extracts a single role from short "role" claim', () => {
    const claims = { role: 'Reviewer' };
    expect(extractRoles(claims)).toEqual(['Reviewer']);
  });

  it('extracts roles from Microsoft URI claim as array', () => {
    const claims = {
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin', 'Reviewer'],
    };
    expect(extractRoles(claims)).toEqual(['Admin', 'Reviewer']);
  });

  it('extracts a single role from Microsoft URI claim', () => {
    const claims = {
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'IntakeWorker',
    };
    expect(extractRoles(claims)).toEqual(['IntakeWorker']);
  });

  it('prefers short "role" claim over Microsoft URI', () => {
    const claims = {
      role: 'Admin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'IntakeWorker',
    };
    expect(extractRoles(claims)).toEqual(['Admin']);
  });

  it('returns empty array when no role claim exists', () => {
    const claims = { sub: '123', email: 'user@test.com' };
    expect(extractRoles(claims)).toEqual([]);
  });

  it('returns empty array for empty claims', () => {
    expect(extractRoles({})).toEqual([]);
  });

  it('returns empty array for null-like role value', () => {
    const claims = { role: null };
    expect(extractRoles(claims)).toEqual([]);
  });
});
