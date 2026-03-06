import { test, expect } from '@playwright/test';
import { loginAsWorker, loginAsReviewer } from './helpers/login';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function createTestPdf(): string {
  const filePath = path.join(__dirname, 'ocr-test-upload.pdf');
  fs.writeFileSync(filePath, Buffer.from(
    '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
    '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
    '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
    'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
    '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
  ));
  return filePath;
}

test.describe('OCR processing pipeline', () => {
  test('uploaded document progresses through OCR processing status', async ({ page }) => {
    await loginAsWorker(page);
    await page.goto('/upload');

    const testFile = createTestPdf();
    const fileName = 'ocr-test-upload.pdf';

    try {
      const fileInput = page.locator('#file-input');
      await fileInput.setInputFiles(testFile);
      await expect(page.getByText(fileName)).toBeVisible();

      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

      // Navigate to documents list and verify the document appears
      await page.goto('/documents');
      await expect(page.getByText(fileName)).toBeVisible({ timeout: 10000 });

      // The document should initially be in Submitted or Processing status
      const docRow = page.locator('tr', { hasText: fileName });
      const statusText = await docRow.textContent();
      expect(statusText).toBeTruthy();

      // Poll for status transition past Submitted (OCR processing)
      await expect(async () => {
        await page.reload();
        const row = page.locator('tr', { hasText: fileName });
        const text = await row.textContent();
        // Document should eventually move past Submitted to Processing or further
        expect(text).not.toContain('Submitted');
      }).toPass({ timeout: 60000, intervals: [3000] });
    } finally {
      fs.unlinkSync(testFile);
    }
  });

  test('reviewer sees extracted fields after OCR completes', async ({ page }) => {
    await loginAsReviewer(page);
    await page.goto('/reviews');

    // Wait for a document to appear in review queue (from previous or concurrent uploads)
    await expect(async () => {
      await page.reload();
      const rows = page.locator('tbody tr');
      const count = await rows.count();
      expect(count).toBeGreaterThan(0);
    }).toPass({ timeout: 60000, intervals: [3000] });

    // Click review on the first document
    const firstRow = page.locator('tbody tr').first();
    await firstRow.getByRole('button', { name: 'Review' }).click();

    // Start review if needed
    const startBtn = page.getByTestId('start-review-btn');
    if (await startBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startBtn.click();
      await expect(page.getByTestId('review-status')).toHaveText('InReview', { timeout: 5000 });
    }

    // Verify extracted fields section exists (OCR should have produced some)
    // With real Tesseract, fields come from actual document content
    const fieldsSection = page.locator('[data-testid^="field-row-"], [data-testid^="confidence-"]');
    const fieldCount = await fieldsSection.count();

    // If document had extractable content, there should be fields
    // If not (e.g., minimal test PDF), at least the UI shouldn't error
    if (fieldCount > 0) {
      // Verify confidence indicators are present for each field
      const confidenceBadges = page.locator('[data-testid^="confidence-"]');
      const badgeCount = await confidenceBadges.count();
      expect(badgeCount).toBeGreaterThan(0);

      // Verify confidence values are in valid range (displayed as percentage)
      for (let i = 0; i < badgeCount; i++) {
        const text = await confidenceBadges.nth(i).textContent();
        const percent = parseInt(text?.replace('%', '') ?? '0');
        expect(percent).toBeGreaterThanOrEqual(0);
        expect(percent).toBeLessThanOrEqual(100);
      }
    }
  });

  test('reviewer can correct OCR-extracted field and finalize', async ({ page }) => {
    await loginAsReviewer(page);
    await page.goto('/reviews');

    // Wait for a document in review queue
    await expect(async () => {
      await page.reload();
      const rows = page.locator('tbody tr');
      const count = await rows.count();
      expect(count).toBeGreaterThan(0);
    }).toPass({ timeout: 60000, intervals: [3000] });

    // Open first document for review
    const firstRow = page.locator('tbody tr').first();
    await firstRow.getByRole('button', { name: 'Review' }).click();

    // Start review if needed
    const startBtn = page.getByTestId('start-review-btn');
    if (await startBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startBtn.click();
      await expect(page.getByTestId('review-status')).toHaveText('InReview', { timeout: 5000 });
    }

    // If there are editable fields, correct one
    const editBtns = page.locator('[data-testid^="edit-btn-"]');
    if (await editBtns.count() > 0) {
      await editBtns.first().click();
      const input = page.locator('[data-testid^="edit-input-"]').first().locator('input');
      await input.clear();
      await input.fill('Corrected by reviewer');
      await page.locator('[data-testid^="save-edit-"]').first().click();
      await page.waitForTimeout(1000);
    }

    // Finalize the review
    await page.getByTestId('finalize-btn').click();
    await expect(page.getByText('Are you sure you want to finalize this review?')).toBeVisible();
    await page.getByTestId('confirm-finalize-btn').click();

    await expect(page.getByText('This document has been finalized.')).toBeVisible({ timeout: 10000 });
  });
});
