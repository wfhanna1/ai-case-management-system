import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import {
  createReviewDocumentDto,
  createExtractedFieldDto,
  createAuditLogEntryDto,
  apiOk,
} from '../fixtures/mock-data';
import {
  mockGetReviewDocument,
  mockStartReview,
  mockCorrectField,
  mockFinalizeReview,
  mockGetAuditLog,
  mockApiError,
} from '../helpers/api-mocks';

reviewerTest.describe('Review detail page (isolation)', () => {
  reviewerTest('shows document metadata', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      originalFileName: 'patient-form.pdf',
      status: 'InReview',
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await expect(reviewerPage.getByRole('heading', { name: 'patient-form.pdf' })).toBeVisible();
    await expect(reviewerPage.getByTestId('review-status')).toHaveText('InReview');
  });

  reviewerTest('shows extracted fields with confidence indicators', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
        createExtractedFieldDto({ name: 'DateOfBirth', value: '1990-01-15', confidence: 0.82 }),
        createExtractedFieldDto({ name: 'SSN', value: '***-**-1234', confidence: 0.65 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // High confidence (>0.9) - green
    const highConf = reviewerPage.getByTestId('confidence-PatientName');
    await expect(highConf).toBeVisible();
    await expect(highConf).toHaveText('95%');

    // Medium confidence (0.7-0.9) - yellow
    const medConf = reviewerPage.getByTestId('confidence-DateOfBirth');
    await expect(medConf).toBeVisible();
    await expect(medConf).toHaveText('82%');

    // Low confidence (<0.7) - red
    const lowConf = reviewerPage.getByTestId('confidence-SSN');
    await expect(lowConf).toBeVisible();
    await expect(lowConf).toHaveText('65%');
  });

  reviewerTest('shows Start Review button for PendingReview documents', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'PendingReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await expect(reviewerPage.getByTestId('start-review-btn')).toBeVisible();
    await expect(reviewerPage.getByTestId('finalize-btn')).not.toBeVisible();
  });

  reviewerTest('Start Review calls API and updates UI', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'PendingReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockStartReview(reviewerPage, doc.id);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('start-review-btn').click();

    // After start, the document refetches. Mock the updated state.
    const updatedDoc = { ...doc, status: 'InReview' as const };
    await mockGetReviewDocument(reviewerPage, updatedDoc);

    // Should eventually show InReview status chip (via refetch)
    await expect(reviewerPage.getByTestId('review-status')).toHaveText('InReview', { timeout: 5000 });
  });

  reviewerTest('edit button opens inline edit for a field', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('edit-btn-PatientName').click();
    await expect(reviewerPage.getByTestId('edit-input-PatientName')).toBeVisible();
    await expect(reviewerPage.getByTestId('save-edit-PatientName')).toBeVisible();
    await expect(reviewerPage.getByTestId('cancel-edit-PatientName')).toBeVisible();
  });

  reviewerTest('save edit calls correct API', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockCorrectField(reviewerPage, doc.id);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('edit-btn-PatientName').click();
    const input = reviewerPage.getByTestId('edit-input-PatientName').locator('input');
    await input.clear();
    await input.fill('Jane Doe');
    await reviewerPage.getByTestId('save-edit-PatientName').click();

    // After correction, refetch returns updated field
    const updatedDoc = {
      ...doc,
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95, correctedValue: 'Jane Doe' }),
      ],
    };
    await mockGetReviewDocument(reviewerPage, updatedDoc);

    await expect(reviewerPage.getByText('Jane Doe')).toBeVisible({ timeout: 5000 });
  });

  reviewerTest('cancel edit restores original state', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'InReview',
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('edit-btn-PatientName').click();
    await expect(reviewerPage.getByTestId('edit-input-PatientName')).toBeVisible();

    await reviewerPage.getByTestId('cancel-edit-PatientName').click();
    await expect(reviewerPage.getByTestId('edit-input-PatientName')).not.toBeVisible();
    await expect(reviewerPage.getByTestId('edit-btn-PatientName')).toBeVisible();
  });

  reviewerTest('Finalize button visible for InReview documents', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await expect(reviewerPage.getByTestId('finalize-btn')).toBeVisible();
  });

  reviewerTest('Finalize shows confirmation dialog', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('finalize-btn').click();
    await expect(reviewerPage.getByText('Are you sure you want to finalize this review?')).toBeVisible();
    await expect(reviewerPage.getByTestId('confirm-finalize-btn')).toBeVisible();
  });

  reviewerTest('Confirm finalize calls API and shows success', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockFinalizeReview(reviewerPage, doc.id);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    // Mock pending reviews endpoint for post-finalize navigation
    await reviewerPage.route('**/api/reviews/pending*', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(apiOk({ items: [], totalCount: 0 })) })
    );

    await reviewerPage.getByTestId('finalize-btn').click();
    await reviewerPage.getByTestId('confirm-finalize-btn').click();

    // After finalize, app navigates to review queue
    await reviewerPage.waitForURL('**/reviews', { timeout: 5000 });
    await expect(reviewerPage.getByText('Review Queue')).toBeVisible();
  });

  reviewerTest('Audit log button opens drawer with entries', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    const auditEntries = [
      createAuditLogEntryDto({ action: 'ReviewStarted', timestamp: '2026-03-01T10:05:00Z' }),
      createAuditLogEntryDto({ action: 'FieldCorrected', fieldName: 'PatientName', previousValue: 'John', newValue: 'Jane', timestamp: '2026-03-01T10:10:00Z' }),
    ];
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetAuditLog(reviewerPage, doc.id, auditEntries);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('audit-btn').click();
    await expect(reviewerPage.getByTestId('audit-list')).toBeVisible();
    await expect(reviewerPage.getByText('ReviewStarted')).toBeVisible();
    await expect(reviewerPage.getByText('FieldCorrected')).toBeVisible();
  });

  reviewerTest('back button navigates to review queue', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview' });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await reviewerPage.getByTestId('back-btn').click();
    await expect(reviewerPage).toHaveURL(/\/reviews$/);
  });

  reviewerTest('shows read-only view for Finalized documents', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({
      status: 'Finalized',
      reviewedAt: '2026-03-01T12:00:00Z',
      extractedFields: [
        createExtractedFieldDto({ name: 'PatientName', value: 'John Doe', confidence: 0.95 }),
      ],
    });
    await mockGetReviewDocument(reviewerPage, doc);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    await expect(reviewerPage.getByText('This document has been finalized.')).toBeVisible();
    await expect(reviewerPage.getByTestId('finalize-btn')).not.toBeVisible();
    await expect(reviewerPage.getByTestId('start-review-btn')).not.toBeVisible();
    // No edit buttons on finalized documents
    await expect(reviewerPage.getByTestId('edit-btn-PatientName')).not.toBeVisible();
  });

  reviewerTest('handles API error loading document', async ({ reviewerPage }) => {
    await mockApiError(reviewerPage, '**/api/reviews/**', 'NOT_FOUND', 'Document not found');
    await reviewerPage.goto('/reviews/00000000-0000-0000-0000-000000000099');

    await expect(reviewerPage.getByRole('alert')).toBeVisible();
  });
});
