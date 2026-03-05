import api from './api';

export interface TemplateField {
  label: string;
  fieldType: string;
  isRequired: boolean;
  options: string | null;
}

export interface FormTemplate {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  type: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  fields: TemplateField[];
}

interface ApiResponse<T> {
  data?: T;
  error?: {
    code: string;
    message: string;
    details?: Record<string, string[]>;
  };
}

export async function getTemplates(): Promise<FormTemplate[]> {
  const res = await api.get<ApiResponse<FormTemplate[]>>('/form-templates');
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  return res.data.data ?? [];
}

export async function getTemplateById(id: string): Promise<FormTemplate> {
  const res = await api.get<ApiResponse<FormTemplate>>(`/form-templates/${id}`);
  if (res.data.error) {
    throw new Error(res.data.error.message);
  }
  if (!res.data.data) {
    throw new Error('Template not found');
  }
  return res.data.data;
}
