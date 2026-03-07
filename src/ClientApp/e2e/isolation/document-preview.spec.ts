import { expect } from '@playwright/test';
import { reviewerTest } from '../fixtures/auth.fixture';
import { createReviewDocumentDto } from '../fixtures/mock-data';
import { mockGetReviewDocument, mockGetDocumentFile, mockGetDocumentFileError } from '../helpers/api-mocks';

reviewerTest.describe('Document preview panel (isolation)', () => {
  reviewerTest('shows document preview panel with image when file loads', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview', originalFileName: 'scan.png' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetDocumentFile(reviewerPage, doc.id, 'image/png');
    await reviewerPage.goto(`/reviews/${doc.id}`);

    const preview = reviewerPage.getByTestId('document-preview');
    await expect(preview).toBeVisible();
    await expect(preview.locator('img')).toBeVisible({ timeout: 5000 });
  });

  reviewerTest('shows warning alert when file download fails', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview', originalFileName: 'missing.pdf' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetDocumentFileError(reviewerPage, doc.id, 404);
    await reviewerPage.goto(`/reviews/${doc.id}`);

    const preview = reviewerPage.getByTestId('document-preview');
    await expect(preview).toBeVisible();
    await expect(preview.getByRole('alert')).toBeVisible({ timeout: 5000 });
    await expect(preview.getByRole('alert')).toContainText('Unable to load document preview');
  });

  reviewerTest('shows PDF iframe when file is application/pdf', async ({ reviewerPage }) => {
    const doc = createReviewDocumentDto({ status: 'InReview', originalFileName: 'form.pdf' });
    await mockGetReviewDocument(reviewerPage, doc);
    await mockGetDocumentFile(reviewerPage, doc.id, 'application/pdf');
    await reviewerPage.goto(`/reviews/${doc.id}`);

    const preview = reviewerPage.getByTestId('document-preview');
    await expect(preview).toBeVisible();
    await expect(preview.locator('iframe')).toBeVisible({ timeout: 5000 });
  });
});
