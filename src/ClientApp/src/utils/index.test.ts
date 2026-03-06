import { describe, it, expect } from 'vitest';
import { formatDate, formatDateTime, truncate } from './index';

describe('truncate', () => {
  it('returns original string if within max length', () => {
    expect(truncate('hello', 10)).toBe('hello');
  });

  it('returns original string if exactly at max length', () => {
    expect(truncate('hello', 5)).toBe('hello');
  });

  it('truncates and adds ellipsis when exceeding max length', () => {
    expect(truncate('hello world', 5)).toBe('hello...');
  });

  it('handles empty string', () => {
    expect(truncate('', 5)).toBe('');
  });

  it('truncates to 0 characters', () => {
    expect(truncate('hello', 0)).toBe('...');
  });
});

describe('formatDate', () => {
  it('formats an ISO date string', () => {
    const result = formatDate('2024-01-15T10:30:00Z');
    expect(result).toContain('2024');
    expect(result).toContain('15');
  });
});

describe('formatDateTime', () => {
  it('formats an ISO datetime string with time', () => {
    const result = formatDateTime('2024-01-15T10:30:00Z');
    expect(result).toContain('2024');
    expect(result).toContain('15');
  });
});
