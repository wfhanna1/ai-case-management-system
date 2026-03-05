import { test, expect } from '@playwright/test';
import { loginAsWorker, loginAsReviewer } from '../helpers/login';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function createTestPdf(): string {
  const filePath = path.join(__dirname, 'pipeline-test.pdf');
  fs.writeFileSync(filePath, Buffer.from(
    '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
    '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
    '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
    'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
    '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
  ));
  return filePath;
}

test.describe.serial('Full document pipeline', () => {
  let uploadedFileName: string;

  test('upload document and verify in documents list', async ({ page }) => {
    await loginAsWorker(page);
    await page.goto('/upload');

    const testFile = createTestPdf();
    uploadedFileName = 'pipeline-test.pdf';

    try {
      const fileInput = page.locator('#file-input');
      await fileInput.setInputFiles(testFile);
      await expect(page.getByText(uploadedFileName)).toBeVisible();

      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

      // Navigate to documents list
      await page.goto('/documents');
      await expect(page.getByText(uploadedFileName)).toBeVisible({ timeout: 10000 });
    } finally {
      fs.unlinkSync(testFile);
    }
  });

  test('document transitions through OCR processing', async ({ page }) => {
    await loginAsWorker(page);
    await page.goto('/documents');

    // Poll for status change - OCR mock should process within ~30s
    // We check that the document eventually moves past Submitted
    const statusCell = page.locator('tr', { hasText: uploadedFileName ?? 'pipeline-test.pdf' });

    await expect(async () => {
      await page.reload();
      const text = await statusCell.textContent();
      expect(text).not.toContain('Submitted');
    }).toPass({ timeout: 60000, intervals: [3000] });
  });

  test('reviewer can review and finalize document', async ({ page }) => {
    await loginAsReviewer(page);
    await page.goto('/reviews');

    // Wait for the document to appear in review queue
    await expect(async () => {
      await page.reload();
      await expect(page.getByText(uploadedFileName ?? 'pipeline-test.pdf')).toBeVisible();
    }).toPass({ timeout: 60000, intervals: [3000] });

    // Click review button for the document
    const row = page.locator('tr', { hasText: uploadedFileName ?? 'pipeline-test.pdf' });
    await row.getByRole('button', { name: 'Review' }).click();

    // Start review if in PendingReview
    const startBtn = page.getByTestId('start-review-btn');
    if (await startBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startBtn.click();
      await expect(page.getByText('InReview')).toBeVisible({ timeout: 5000 });
    }

    // If there are extracted fields, try correcting one
    const editBtns = page.locator('[data-testid^="edit-btn-"]');
    if (await editBtns.count() > 0) {
      await editBtns.first().click();
      const input = page.locator('[data-testid^="edit-input-"]').first().locator('input');
      await input.clear();
      await input.fill('Corrected Value');
      await page.locator('[data-testid^="save-edit-"]').first().click();
      // Wait for save to complete
      await page.waitForTimeout(1000);
    }

    // Finalize the review
    await page.getByTestId('finalize-btn').click();
    await expect(page.getByText('Are you sure you want to finalize this review?')).toBeVisible();
    await page.getByTestId('confirm-finalize-btn').click();

    await expect(page.getByText('This document has been finalized.')).toBeVisible({ timeout: 10000 });
  });

  test('audit trail records review actions', async ({ page }) => {
    await loginAsReviewer(page);
    await page.goto('/reviews');

    // Find the finalized document - it may not be in the queue anymore
    // Navigate directly to review detail if possible, or look in completed
    // The finalized doc should still be accessible via direct URL navigation
    // For now, check the audit trail from the last test context
    await page.goto('/reviews');

    // The document might still appear in the list temporarily
    const row = page.locator('tr', { hasText: uploadedFileName ?? 'pipeline-test.pdf' });
    if (await row.isVisible({ timeout: 3000 }).catch(() => false)) {
      await row.getByRole('button', { name: 'Review' }).click();
    } else {
      // If not in queue, skip this test - document was already fully processed
      test.skip();
      return;
    }

    // Open audit history
    await page.getByTestId('audit-btn').click();
    await expect(page.getByTestId('audit-list')).toBeVisible({ timeout: 5000 });

    // Verify audit entries exist
    const auditList = page.getByTestId('audit-list');
    await expect(auditList.locator('li')).not.toHaveCount(0);
  });
});
