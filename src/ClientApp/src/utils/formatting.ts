export function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString();
}

export function confidenceColor(confidence: number): 'success' | 'warning' | 'error' {
  if (confidence > 0.9) return 'success';
  if (confidence >= 0.7) return 'warning';
  return 'error';
}

export function confidenceLabel(confidence: number): string {
  return `${(confidence * 100).toFixed(0)}%`;
}

export const STATUS_COLORS: Record<string, 'info' | 'warning' | 'success' | 'error'> = {
  Submitted: 'info',
  Processing: 'warning',
  Completed: 'success',
  Failed: 'error',
  PendingReview: 'warning',
  InReview: 'info',
  Finalized: 'success',
};

export function formatStatusLabel(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}
