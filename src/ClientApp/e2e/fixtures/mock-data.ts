export interface DocumentDto {
  id: string;
  tenantId: string;
  originalFileName: string;
  storageKey: string;
  status: string;
  submittedAt: string;
  processedAt: string | null;
}

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

export interface FormTemplateDto {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  type: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  fields: { label: string; fieldType: string; isRequired: boolean; options: string | null }[];
}

let counter = 0;
function nextId(): string {
  counter++;
  return `00000000-0000-0000-0000-${String(counter).padStart(12, '0')}`;
}

export function createDocumentDto(overrides: Partial<DocumentDto> = {}): DocumentDto {
  return {
    id: nextId(),
    tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
    originalFileName: 'test-document.pdf',
    storageKey: 'uploads/test-document.pdf',
    status: 'Submitted',
    submittedAt: '2026-03-01T10:00:00Z',
    processedAt: null,
    ...overrides,
  };
}

export function createExtractedFieldDto(overrides: Partial<ExtractedFieldDto> = {}): ExtractedFieldDto {
  return {
    name: 'PatientName',
    value: 'John Doe',
    confidence: 0.95,
    correctedValue: null,
    ...overrides,
  };
}

export function createReviewDocumentDto(overrides: Partial<ReviewDocumentDto> = {}): ReviewDocumentDto {
  return {
    id: nextId(),
    tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
    originalFileName: 'intake-form.pdf',
    status: 'PendingReview',
    submittedAt: '2026-03-01T10:00:00Z',
    processedAt: '2026-03-01T10:01:00Z',
    reviewedBy: null,
    reviewedAt: null,
    extractedFields: [
      createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
      createExtractedFieldDto({ name: 'DateOfBirth', value: '1990-01-15', confidence: 0.82 }),
      createExtractedFieldDto({ name: 'SSN', value: '***-**-1234', confidence: 0.65 }),
    ],
    ...overrides,
  };
}

export function createAuditLogEntryDto(overrides: Partial<AuditLogEntryDto> = {}): AuditLogEntryDto {
  return {
    id: nextId(),
    action: 'ReviewStarted',
    performedBy: '00000000-0000-0000-0000-000000000020',
    timestamp: '2026-03-01T10:05:00Z',
    fieldName: null,
    previousValue: null,
    newValue: null,
    ...overrides,
  };
}

export function createFormTemplateDto(overrides: Partial<FormTemplateDto> = {}): FormTemplateDto {
  return {
    id: nextId(),
    tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
    name: 'Child Welfare Intake',
    description: 'Standard intake form for child welfare cases',
    type: 'ChildWelfare',
    isActive: true,
    createdAt: '2026-01-15T08:00:00Z',
    updatedAt: null,
    fields: [
      { label: 'Child Name', fieldType: 'Text', isRequired: true, options: null },
      { label: 'Date of Birth', fieldType: 'Date', isRequired: true, options: null },
      { label: 'Case Type', fieldType: 'Select', isRequired: true, options: '["Neglect","Abuse","Abandonment"]' },
    ],
    ...overrides,
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

export interface SearchDocumentsResultDto {
  items: DocumentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SearchCasesResultDto {
  items: CaseDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function createCaseDto(overrides: Partial<CaseDto> = {}): CaseDto {
  return {
    id: nextId(),
    tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
    subjectName: 'John Doe',
    createdAt: '2026-03-01T10:00:00Z',
    updatedAt: '2026-03-01T10:05:00Z',
    documentCount: 2,
    ...overrides,
  };
}

export function createCaseDetailDto(overrides: Partial<CaseDetailDto> = {}): CaseDetailDto {
  return {
    id: nextId(),
    tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
    subjectName: 'John Doe',
    createdAt: '2026-03-01T10:00:00Z',
    updatedAt: '2026-03-01T10:05:00Z',
    documents: [
      createDocumentDto({ status: 'PendingReview', processedAt: '2026-03-01T10:01:00Z' }),
      createDocumentDto({ originalFileName: 'follow-up.pdf', status: 'Finalized', processedAt: '2026-03-01T10:02:00Z' }),
    ],
    ...overrides,
  };
}

export function createSearchDocumentsResultDto(
  items: DocumentDto[] = [],
  totalCount?: number,
  page = 1,
  pageSize = 20
): SearchDocumentsResultDto {
  return {
    items,
    totalCount: totalCount ?? items.length,
    page,
    pageSize,
  };
}

export function createSearchCasesResultDto(
  items: CaseDto[] = [],
  totalCount?: number,
  page = 1,
  pageSize = 20
): SearchCasesResultDto {
  return {
    items,
    totalCount: totalCount ?? items.length,
    page,
    pageSize,
  };
}

export interface SimilarCaseDto {
  documentId: string;
  score: number;
  summary: string;
  metadata: Record<string, string>;
  sharedFields: Record<string, string>;
}

export interface SimilarCasesResultDto {
  items: SimilarCaseDto[];
}

export function createSimilarCaseDto(overrides: Partial<SimilarCaseDto> = {}): SimilarCaseDto {
  return {
    documentId: nextId(),
    score: 0.92,
    summary: 'Subject: Alice Johnson. Category: ChildWelfare. Presenting issue: Physical abuse suspected.',
    metadata: {
      ChildName: 'Alice Johnson',
      Type: 'ChildWelfare',
      ReasonForReferral: 'Physical abuse suspected',
      Age: '8',
    },
    sharedFields: {
      ChildName: 'Alice Johnson',
    },
    ...overrides,
  };
}

export function apiOk<T>(data: T) {
  return { data, error: null };
}

export function apiError(code: string, message: string) {
  return { data: null, error: { code, message } };
}
