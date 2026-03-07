import api from './api';

export interface ExtractedFieldDto {
  name: string;
  value: string;
  confidence: number;
  correctedValue: string | null;
}

export interface ReviewDocumentDto {
  id: string;
  tenantId: string;
  originalFileName: string;
  status: string;
  submittedAt: string;
  processedAt: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  extractedFields: ExtractedFieldDto[];
}

export interface AuditLogEntryDto {
  id: string;
  action: string;
  performedBy: string | null;
  timestamp: string;
  fieldName: string | null;
  previousValue: string | null;
  newValue: string | null;
}

interface ApiResponse<T> {
  data?: T;
  error?: {
    code: string;
    message: string;
  };
}

export interface PendingReviewResult {
  items: ReviewDocumentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function getPendingReviews(page = 1, pageSize = 20): Promise<PendingReviewResult> {
  const res = await api.get<ApiResponse<PendingReviewResult>>('/reviews/pending', {
    params: { page, pageSize },
  });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? { items: [], totalCount: 0, page, pageSize };
}

export async function getReview(documentId: string): Promise<ReviewDocumentDto> {
  const res = await api.get<ApiResponse<ReviewDocumentDto>>(`/reviews/${documentId}`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('Document not found');
  }
  return res.data.data;
}

export async function startReview(documentId: string): Promise<void> {
  const res = await api.post<ApiResponse<object>>(`/reviews/${documentId}/start`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
}

export async function correctField(
  documentId: string,
  fieldName: string,
  newValue: string
): Promise<void> {
  const res = await api.post<ApiResponse<object>>(`/reviews/${documentId}/correct-field`, {
    fieldName,
    newValue,
  });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
}

export async function finalizeReview(documentId: string): Promise<void> {
  const res = await api.post<ApiResponse<object>>(`/reviews/${documentId}/finalize`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
}

export async function getAuditTrail(documentId: string): Promise<AuditLogEntryDto[]> {
  const res = await api.get<ApiResponse<AuditLogEntryDto[]>>(`/reviews/${documentId}/audit`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? [];
}

export interface SimilarCaseDto {
  documentId: string;
  score: number;
  summary: string;
  metadata: Record<string, string>;
}

export interface SimilarCasesResultDto {
  items: SimilarCaseDto[];
}

export async function getDocumentFileBlob(documentId: string): Promise<Blob> {
  const res = await api.get(`/documents/${documentId}/file`, {
    responseType: 'blob',
  });
  return res.data;
}

export async function getSimilarCases(documentId: string): Promise<SimilarCasesResultDto> {
  const res = await api.get<ApiResponse<SimilarCasesResultDto>>(
    `/reviews/${documentId}/similar-cases`
  );
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? { items: [] };
}
