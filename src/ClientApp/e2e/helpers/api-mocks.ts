import type { Page } from '@playwright/test';
import type {
  DocumentDto,
  ReviewDocumentDto,
  AuditLogEntryDto,
  FormTemplateDto,
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
  await page.route('**/api/reviews/pending', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(docs)) })
  );
}

export async function mockGetReviewDocument(page: Page, doc: ReviewDocumentDto) {
  await page.route(`**/api/reviews/${doc.id}`, route => {
    if (route.request().url().includes('/audit') || route.request().url().includes('/start') ||
        route.request().url().includes('/correct-field') || route.request().url().includes('/finalize')) {
      return route.continue();
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(doc)) });
  });
}

export async function mockStartReview(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/start`, route =>
    route.fulfill({ status: 204 })
  );
}

export async function mockCorrectField(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/correct-field`, route =>
    route.fulfill({ status: 204 })
  );
}

export async function mockFinalizeReview(page: Page, docId: string) {
  await page.route(`**/api/reviews/${docId}/finalize`, route =>
    route.fulfill({ status: 204 })
  );
}

export async function mockGetAuditLog(page: Page, docId: string, entries: AuditLogEntryDto[]) {
  await page.route(`**/api/reviews/${docId}/audit`, route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk(entries)) })
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
