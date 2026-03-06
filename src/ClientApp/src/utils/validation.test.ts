import { describe, it, expect } from 'vitest';
import { validateEmail, validatePassword, validateRequired } from './validation';

describe('validateEmail', () => {
  it('returns error for empty string', () => {
    expect(validateEmail('')).toBe('Email is required.');
  });

  it('returns error for whitespace-only', () => {
    expect(validateEmail('   ')).toBe('Email is required.');
  });

  it('returns error for missing @', () => {
    expect(validateEmail('userexample.com')).toBe('Enter a valid email address.');
  });

  it('returns error for missing domain', () => {
    expect(validateEmail('user@')).toBe('Enter a valid email address.');
  });

  it('returns error for missing TLD', () => {
    expect(validateEmail('user@example')).toBe('Enter a valid email address.');
  });

  it('returns null for valid email', () => {
    expect(validateEmail('user@example.com')).toBeNull();
  });

  it('returns null for email with subdomain', () => {
    expect(validateEmail('user@sub.example.com')).toBeNull();
  });
});

describe('validatePassword', () => {
  it('returns error for empty password', () => {
    expect(validatePassword('')).toBe('Password is required.');
  });

  it('returns error for short password', () => {
    expect(validatePassword('Ab1')).toBe('Password must be at least 8 characters.');
  });

  it('returns error for password at 7 chars', () => {
    expect(validatePassword('Abcdef1')).toBe('Password must be at least 8 characters.');
  });

  it('returns error for no uppercase', () => {
    expect(validatePassword('abcdefg1')).toBe('Password must include an uppercase letter.');
  });

  it('returns error for no lowercase', () => {
    expect(validatePassword('ABCDEFG1')).toBe('Password must include a lowercase letter.');
  });

  it('returns error for no number', () => {
    expect(validatePassword('Abcdefgh')).toBe('Password must include a number.');
  });

  it('returns null for valid password', () => {
    expect(validatePassword('Abcdefg1')).toBeNull();
  });

  it('returns null for strong password', () => {
    expect(validatePassword('MyStr0ngPass!')).toBeNull();
  });
});

describe('validateRequired', () => {
  it('returns error for empty string', () => {
    expect(validateRequired('', 'Name')).toBe('Name is required.');
  });

  it('returns error for whitespace-only', () => {
    expect(validateRequired('   ', 'Name')).toBe('Name is required.');
  });

  it('returns null for non-empty value', () => {
    expect(validateRequired('John', 'Name')).toBeNull();
  });

  it('uses the label in the error message', () => {
    expect(validateRequired('', 'Email')).toBe('Email is required.');
  });
});
