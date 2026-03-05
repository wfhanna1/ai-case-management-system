import api from './api';

export interface DocumentDto {
  id: string;
  tenantId: string;
  originalFileName: string;
  storageKey: string;
  status: 'Submitted' | 'Processing' | 'Completed' | 'Failed' | 'PendingReview' | 'InReview' | 'Finalized';
  submittedAt: string;
  processedAt: string | null;
}

interface ApiResponse<T> {
  data?: T;
  error?: {
    code: string;
    message: string;
    details?: Record<string, string[]>;
  };
}

export async function uploadDocument(
  file: File,
  templateId?: string
): Promise<ApiResponse<DocumentDto>> {
  const formData = new FormData();
  formData.append('file', file);
  if (templateId) {
    formData.append('templateId', templateId);
  }
  const res = await api.post<ApiResponse<DocumentDto>>('/documents', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data;
}

export async function getDocuments(): Promise<DocumentDto[]> {
  const res = await api.get<ApiResponse<DocumentDto[]>>('/documents');
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? [];
}

export async function getDocument(id: string): Promise<DocumentDto> {
  const res = await api.get<ApiResponse<DocumentDto>>(`/documents/${id}`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('Document not found');
  }
  return res.data.data;
}

export interface SearchDocumentsParams {
  fileName?: string;
  status?: string;
  from?: string;
  to?: string;
  fieldValue?: string;
  page?: number;
  pageSize?: number;
}

export interface SearchDocumentsResult {
  items: DocumentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export async function searchDocuments(params: SearchDocumentsParams): Promise<SearchDocumentsResult> {
  const res = await api.get<ApiResponse<SearchDocumentsResult>>('/documents/search', { params });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('No search results');
  }
  return res.data.data;
}
