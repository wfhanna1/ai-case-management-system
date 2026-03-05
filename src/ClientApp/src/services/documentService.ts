import api from './api';

export interface DocumentDto {
  id: string;
  tenantId: string;
  originalFileName: string;
  storageKey: string;
  status: 'Submitted' | 'Processing' | 'Completed' | 'Failed';
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

interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
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
  return res.data;
}

export async function getDocuments(
  page: number,
  pageSize: number
): Promise<PaginatedResult<DocumentDto>> {
  const res = await api.get<ApiResponse<PaginatedResult<DocumentDto>>>(
    `/documents?page=${page}&pageSize=${pageSize}`
  );
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? { items: [], totalCount: 0, pageNumber: page, pageSize, totalPages: 0 };
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
