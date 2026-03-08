import { describe, it, expect } from 'vitest';
import { formatDate, confidenceColor, confidenceLabel, STATUS_COLORS } from './formatting';

describe('formatDate', () => {
  it('returns dash for null', () => {
    expect(formatDate(null)).toBe('-');
  });

  it('returns dash for undefined', () => {
    expect(formatDate(undefined as unknown as null)).toBe('-');
  });

  it('formats a valid ISO date string', () => {
    const result = formatDate('2026-03-01T10:30:00Z');
    expect(result).not.toBe('-');
    expect(result.length).toBeGreaterThan(0);
  });

  it('formats a date with time components', () => {
    const result = formatDate('2026-03-01T14:30:00Z');
    expect(result).toContain('2026');
  });
});

describe('confidenceColor', () => {
  it('returns success for confidence > 0.9', () => {
    expect(confidenceColor(0.95)).toBe('success');
    expect(confidenceColor(1.0)).toBe('success');
  });

  it('returns warning for confidence between 0.7 and 0.9', () => {
    expect(confidenceColor(0.7)).toBe('warning');
    expect(confidenceColor(0.85)).toBe('warning');
    expect(confidenceColor(0.9)).toBe('warning');
  });

  it('returns error for confidence below 0.7', () => {
    expect(confidenceColor(0.69)).toBe('error');
    expect(confidenceColor(0.5)).toBe('error');
    expect(confidenceColor(0)).toBe('error');
  });
});

describe('confidenceLabel', () => {
  it('formats confidence as percentage', () => {
    expect(confidenceLabel(0.95)).toBe('95%');
    expect(confidenceLabel(1.0)).toBe('100%');
    expect(confidenceLabel(0.7)).toBe('70%');
    expect(confidenceLabel(0.123)).toBe('12%');
  });
});

describe('STATUS_COLORS', () => {
  it('maps Submitted to info', () => {
    expect(STATUS_COLORS.Submitted).toBe('info');
  });

  it('maps Processing to warning', () => {
    expect(STATUS_COLORS.Processing).toBe('warning');
  });

  it('maps Completed to success', () => {
    expect(STATUS_COLORS.Completed).toBe('success');
  });

  it('maps Failed to error', () => {
    expect(STATUS_COLORS.Failed).toBe('error');
  });

  it('maps PendingReview to warning', () => {
    expect(STATUS_COLORS.PendingReview).toBe('warning');
  });

  it('maps InReview to info', () => {
    expect(STATUS_COLORS.InReview).toBe('info');
  });

  it('maps Finalized to success', () => {
    expect(STATUS_COLORS.Finalized).toBe('success');
  });
});
