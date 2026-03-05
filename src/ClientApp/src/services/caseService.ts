import api from './api';
import type { DocumentDto } from './documentService';

interface ApiResponse<T> {
  data?: T;
  error?: {
    code: string;
    message: string;
  };
}

export interface CaseDto {
  id: string;
  tenantId: string;
  subjectName: string;
  createdAt: string;
  updatedAt: string;
  documentCount: number;
}

export interface CaseDetailDto {
  id: string;
  tenantId: string;
  subjectName: string;
  createdAt: string;
  updatedAt: string;
  documents: DocumentDto[];
}

export interface CasesResult {
  items: CaseDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SearchCasesParams {
  q?: string;
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getCases(page = 1, pageSize = 20): Promise<CasesResult> {
  const res = await api.get<ApiResponse<CasesResult>>('/cases', { params: { page, pageSize } });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('No cases data');
  }
  return res.data.data;
}

export async function getCase(id: string): Promise<CaseDetailDto> {
  const res = await api.get<ApiResponse<CaseDetailDto>>(`/cases/${id}`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('Case not found');
  }
  return res.data.data;
}

export async function searchCases(params: SearchCasesParams): Promise<CasesResult> {
  const res = await api.get<ApiResponse<CasesResult>>('/cases/search', { params });
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('No search results');
  }
  return res.data.data;
}
