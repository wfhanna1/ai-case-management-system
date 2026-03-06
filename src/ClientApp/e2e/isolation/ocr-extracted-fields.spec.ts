import { expect } from '@playwright/test';
import { reviewerTest, workerTest } from '../fixtures/auth.fixture';
import {
  createDocumentDto,
  createReviewDocumentDto,
  createExtractedFieldDto,
} from '../fixtures/mock-data';
import {
  mockGetDocuments,
  mockGetDocument,
  mockGetReviewDocument,
  mockStartReview,
} from '../helpers/api-mocks';

reviewerTest.describe('OCR extracted fields display (isolation)', () => {
  reviewerTest('shows OCR-extracted fields from real document processing', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'PendingReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'ClientName', value: 'John Smith', confidence: 0.92 }),
        createExtractedFieldDto({ name: 'Date', value: '2024-01-15', confidence: 0.88 }),
        createExtractedFieldDto({ name: 'CaseNumber', value: 'CW-2024-001', confidence: 0.75 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await expect(reviewerPage.getByText('John Smith')).toBeVisible();
    await expect(reviewerPage.getByText('2024-01-15')).toBeVisible();
    await expect(reviewerPage.getByText('CW-2024-001')).toBeVisible();
  });

  reviewerTest('displays confidence indicators for OCR-extracted fields', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'ClientName', value: 'John Smith', confidence: 0.95 }),
        createExtractedFieldDto({ name: 'Address', value: '123 Main St', confidence: 0.72 }),
        createExtractedFieldDto({ name: 'PhoneNumber', value: '555-0123', confidence: 0.55 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // High confidence (>0.9) shows green indicator
    const highConf = reviewerPage.getByTestId('confidence-ClientName');
    await expect(highConf).toBeVisible();
    await expect(highConf).toHaveText('95%');

    // Medium confidence (0.7-0.9) shows yellow indicator
    const medConf = reviewerPage.getByTestId('confidence-Address');
    await expect(medConf).toBeVisible();
    await expect(medConf).toHaveText('72%');

    // Low confidence (<0.7) shows red indicator
    const lowConf = reviewerPage.getByTestId('confidence-PhoneNumber');
    await expect(lowConf).toBeVisible();
    await expect(lowConf).toHaveText('55%');
  });

  reviewerTest('shows empty state when OCR extracts no fields', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'PendingReview',
      extractedFields: [],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Should show the document but with no extracted fields
    await expect(reviewerPage.getByRole('heading', { name: doc.originalFileName })).toBeVisible();
    // The fields section should exist but be empty or show a message
    const fieldRows = reviewerPage.locator('[data-testid^="field-row-"]');
    await expect(fieldRows).toHaveCount(0);
  });

  reviewerTest('allows correction of low-confidence OCR fields', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'ClientName', value: 'J0hn Sm1th', confidence: 0.45 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Low confidence field should be editable
    await reviewerPage.getByTestId('edit-btn-ClientName').click();
    const input = reviewerPage.getByTestId('edit-input-ClientName').locator('input');
    await expect(input).toBeVisible();
    await expect(input).toHaveValue('J0hn Sm1th');
  });

  reviewerTest('document transitions from Processing to PendingReview with fields', async ({ reviewerPage }) => {
    // First load: document is still processing (no fields yet)
    const processingDoc = createReviewDocumentDto({
      status: 'Processing',
      processedAt: null,
      extractedFields: [],
    });
    await mockGetReviewDocument(reviewerPage, processingDoc);
    await reviewerPage.goto(`/reviews/${processingDoc.id}`);

    await expect(reviewerPage.getByTestId('review-status')).toHaveText('Processing');

    // After OCR completes, mock the updated state with extracted fields
    const processedDoc = {
      ...processingDoc,
      status: 'PendingReview' as const,
      processedAt: '2026-03-01T10:01:00Z',
      extractedFields: [
        createExtractedFieldDto({ name: 'ClientName', value: 'John Smith', confidence: 0.92 }),
        createExtractedFieldDto({ name: 'DateOfBirth', value: '1990-05-20', confidence: 0.85 }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, processedDoc);

    // Reload to simulate polling
    await reviewerPage.reload();

    await expect(reviewerPage.getByTestId('review-status')).toHaveText('PendingReview');
    await expect(reviewerPage.getByText('John Smith')).toBeVisible();
    await expect(reviewerPage.getByText('1990-05-20')).toBeVisible();
  });
});

workerTest.describe('OCR document status in documents list (isolation)', () => {
  workerTest('shows Processing status for documents being OCR-processed', async ({ workerPage }) => {
    const docs = [
      createDocumentDto({ originalFileName: 'scan-001.pdf', status: 'Processing' }),
      createDocumentDto({ originalFileName: 'scan-002.pdf', status: 'PendingReview', processedAt: '2026-03-01T10:01:00Z' }),
      createDocumentDto({ originalFileName: 'scan-003.pdf', status: 'Submitted' }),
    ];
    await mockGetDocuments(workerPage, docs);
    await workerPage.goto('/documents');

    // Verify each document shows correct status
    const processingRow = workerPage.locator('tr', { hasText: 'scan-001.pdf' });
    await expect(processingRow).toContainText('Processing');

    const reviewRow = workerPage.locator('tr', { hasText: 'scan-002.pdf' });
    await expect(reviewRow).toContainText('PendingReview');

    const submittedRow = workerPage.locator('tr', { hasText: 'scan-003.pdf' });
    await expect(submittedRow).toContainText('Submitted');
  });

  workerTest('documents list shows processed status after OCR', async ({ workerPage }) => {
    const doc = createDocumentDto({
      originalFileName: 'intake-form.pdf',
      status: 'PendingReview',
      processedAt: '2026-03-01T10:01:00Z',
    });
    await mockGetDocuments(workerPage, [doc]);
    await workerPage.goto('/documents');

    const row = workerPage.locator('tr', { hasText: 'intake-form.pdf' });
    await expect(row).toBeVisible();
    await expect(row).toContainText('PendingReview');
  });
});
