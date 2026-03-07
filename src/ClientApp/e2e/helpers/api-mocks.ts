import type { Page } from '@playwright/test';
import type {
  DocumentDto,
  ReviewDocumentDto,
  AuditLogEntryDto,
  FormTemplateDto,
  CaseDetailDto,
  SearchDocumentsResultDto,
  SearchCasesResultDto,
  SimilarCasesResultDto,
} from '../fixtures/mock-data';
import { apiOk } from '../fixtures/mock-data';

// Documents
export async function mockGetDocuments(page: Page, documents: DocumentDto[]) {
  await page.route('**/api/documents', route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(documents)) });
    }
    return route.continue();
  });
}

export async function mockGetDocument(page: Page, doc: DocumentDto) {
  await page.route(`**/api/documents/${doc.id}`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(doc)) })
  );
}

export async function mockUploadDocument(page: Page, response: DocumentDto) {
  await page.route('**/api/documents', route => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(response)) });
    }
    return route.continue();
  });
}

export async function mockUploadDocumentError(page: Page, code: string, message: string) {
  await page.route('**/api/documents', route => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code, message } }),
      });
    }
    return route.continue();
  });
}

// Reviews
export async function mockGetReviewQueue(page: Page, docs: ReviewDocumentDto[]) {
  await page.route(/\/api\/reviews\/pending(\?|$)/, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: docs, totalCount: docs.length, page: 1, pageSize: 20 })) })
  );
}

export async function mockGetReviewDocument(page: Page, doc: ReviewDocumentDto) {
  // Default mock for similar-cases (empty) so the useQuery doesn't hit a real server.
  // Tests that need specific similar-cases data should call mockGetSimilarCases after this.
  await page.route(`**/api/reviews/${doc.id}/similar-cases`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: [] })) })
  );
  // Default mock for document file preview (1x1 transparent PNG).
  // Tests that need specific file behavior should call mockGetDocumentFile/mockGetDocumentFileError after this.
  await page.route(`**/api/documents/${doc.id}/file`, route =>
    route.fulfill({
      status: 200,
      contentType: 'image/png',
      body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQI12NgAAIABQAB', 'base64'),
    })
  );
  await page.route(`**/api/reviews/${doc.id}`, route => {
    if (route.request().url().includes('/audit') || route.request().url().includes('/start') ||
        route.request().url().includes('/correct-field') || route.request().url().includes('/finalize') ||
        route.request().url().includes('/similar-cases')) {
      return route.continue();
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(doc)) });
  });
}

export async function mockStartReview(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/start`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({})) })
  );
}

export async function mockCorrectField(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/correct-field`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({})) })
  );
}

export async function mockFinalizeReview(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/finalize`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({})) })
  );
}

export async function mockGetAuditLog(page: Page, docId: string, entries: AuditLogEntryDto[]) {
  await page.route(`**/api/reviews/${docId}/audit`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(entries)) })
  );
}

export async function mockGetSimilarCases(page: Page, docId: string, result: SimilarCasesResultDto) {
  await page.route(`**/api/reviews/${docId}/similar-cases`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(result)) })
  );
}

// Templates
export async function mockGetTemplates(page: Page, templates: FormTemplateDto[]) {
  await page.route('**/api/form-templates', route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(templates)) });
    }
    return route.continue();
  });
}

// Auth
export async function mockLoginSuccess(page: Page, token = 'mock-jwt-token') {
  await page.route('**/api/auth/login', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        data: {
          userId: '00000000-0000-0000-0000-000000000010',
          accessToken: token,
          refreshToken: 'mock-refresh',
          expiresAt: '2026-12-31T23:59:59Z',
        },
        error: null,
      }),
    })
  );
}

export async function mockLoginFailure(page: Page, code: string, message: string) {
  await page.route('**/api/auth/login', route =>
    route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ data: null, error: { code, message } }),
    })
  );
}

// Search
export async function mockSearchDocuments(page: Page, result: SearchDocumentsResultDto) {
  await page.route('**/api/documents/search*', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(result)) })
  );
}

// Cases
export async function mockGetCases(page: Page, result: SearchCasesResultDto) {
  await page.route(/\/api\/cases(\?.*)?$/, route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(result)) });
    }
    return route.continue();
  });
}

export async function mockGetCase(page: Page, detail: CaseDetailDto) {
  await page.route(`**/api/cases/${detail.id}`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(detail)) })
  );
}

export async function mockSearchCases(page: Page, result: SearchCasesResultDto) {
  await page.route('**/api/cases/search*', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(result)) })
  );
}

// Document file blob
export async function mockGetDocumentFile(page: Page, docId: string, contentType = 'image/png') {
  // Minimal 1x1 PNG (67 bytes) for image tests; a tiny PDF header for PDF tests.
  const pngBytes = Buffer.from(
    '89504e470d0a1a0a0000000d49484452000000010000000108020000009001' +
    '2e0000000c4944415408d76360f8cfc00000000200017221bc330000000049454e44ae426082',
    'hex'
  );
  const pdfBytes = Buffer.from('%PDF-1.4\n%%EOF\n');
  const body = contentType === 'application/pdf' ? pdfBytes : pngBytes;
  await page.route(`**/api/documents/${docId}/file`, route =>
    route.fulfill({ status: 200, contentType, body })
  );
}

export async function mockGetDocumentFileError(page: Page, docId: string, status = 404) {
  await page.route(`**/api/documents/${docId}/file`, route =>
    route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ data: null, error: { code: 'NOT_FOUND', message: 'File not found' } }) })
  );
}

// Generic error mock for any API endpoint
export async function mockApiError(page: Page, urlPattern: string, code: string, message: string) {
  await page.route(urlPattern, route =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ data: null, error: { code, message } }),
    })
  );
}
