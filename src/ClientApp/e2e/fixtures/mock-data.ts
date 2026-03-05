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

export function apiOk<T>(data: T) {
  return { data, error: null };
}

export function apiError(code: string, message: string) {
  return { data: null, error: { code, message } };
}
