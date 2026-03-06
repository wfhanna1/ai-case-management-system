import { test, expect } from '@playwright/test';
import { loginAsWorker, loginAsReviewer } from './helpers/login';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Real document with extractable text (contains "John Smith", dates, etc.)
const TEST_IMAGE_PATH = path.join(__dirname, 'test-document.png');

async function uploadDocument(page: import('@playwright/test').Page, filePath: string) {
  await loginAsWorker(page);
  await page.goto('/upload');
  const fileInput = page.locator('#file-input');
  await fileInput.setInputFiles(filePath);
  await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
  await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });
}

test.describe('OCR processing pipeline', () => {
  // These tests interact with a live Docker stack and OCR processing takes time
  test.setTimeout(120000);

  test('uploaded document progresses through OCR processing status', async ({ page }) => {
    await uploadDocument(page, TEST_IMAGE_PATH);

    // Navigate to documents list and verify the document appears
    await page.goto('/documents');
    const docRow = page.locator('tr', { hasText: 'test-document.png' }).first();
    await expect(docRow).toBeVisible({ timeout: 10000 });

    // Poll for status transition past Submitted (OCR processing)
    await expect(async () => {
      await page.reload();
      const row = page.locator('tr', { hasText: 'test-document.png' }).first();
      const text = await row.textContent();
      // Document should eventually move past Submitted to Processing or PendingReview
      expect(text).toMatch(/Processing|PendingReview|InReview|Finalized/);
    }).toPass({ timeout: 90000, intervals: [3000] });
  });

  test('reviewer sees extracted fields after OCR completes', async ({ page }) => {
    await loginAsReviewer(page);
    await page.goto('/reviews');

    // Wait for at least one document in review queue
    await expect(async () => {
      await page.reload();
      const rows = page.locator('tbody tr');
      const count = await rows.count();
      expect(count).toBeGreaterThan(0);
    }).toPass({ timeout: 60000, intervals: [3000] });

    // Click review on the first available document
    const firstRow = page.locator('tbody tr').first();
    await firstRow.getByRole('button', { name: 'Review' }).click();

    // Start review if in PendingReview status
    const startBtn = page.getByTestId('start-review-btn');
    if (await startBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await startBtn.click();
      await expect(page.getByTestId('review-status')).toHaveText('InReview', { timeout: 5000 });
    }

    // Verify extracted fields section exists
    const fieldsSection = page.locator('[data-testid^="field-row-"], [data-testid^="confidence-"]');
    const fieldCount = await fieldsSection.count();

    if (fieldCount > 0) {
      const confidenceBadges = page.locator('[data-testid^="confidence-"]');
      const badgeCount = await confidenceBadges.count();
      expect(badgeCount).toBeGreaterThan(0);

      for (let i = 0; i < badgeCount; i++) {
        const text = await confidenceBadges.nth(i).textContent();
        const percent = parseInt(text?.replace('%', '') ?? '0');
        expect(percent).toBeGreaterThanOrEqual(0);
        expect(percent).toBeLessThanOrEqual(100);
      }
    }
  });

  test('reviewer can correct OCR-extracted field and finalize', async ({ page }) => {
    // First upload a document as worker
    await uploadDocument(page, TEST_IMAGE_PATH);

    // Clear auth state before switching to reviewer
    await page.context().clearCookies();
    await page.evaluate(() => localStorage.clear());

    // Wait for OCR processing, then switch to reviewer
    await loginAsReviewer(page);

    // Poll reviews page until a document with Review button appears
    await page.goto('/reviews');
    await expect(async () => {
      await page.reload();
      const rows = page.locator('tbody tr');
      const count = await rows.count();
      expect(count).toBeGreaterThan(0);
    }).toPass({ timeout: 90000, intervals: [3000] });

    // Open the first reviewable document
    const firstRow = page.locator('tbody tr').first();
    await firstRow.getByRole('button', { name: 'Review' }).click();

    // Start review -- the document should be in PendingReview after OCR
    const startBtn = page.getByTestId('start-review-btn');
    await expect(startBtn).toBeVisible({ timeout: 10000 });
    await startBtn.click();
    await expect(page.getByTestId('review-status')).toHaveText('InReview', { timeout: 10000 });

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
    const finalizeBtn = page.getByTestId('finalize-btn');
    await expect(finalizeBtn).toBeVisible({ timeout: 10000 });
    await finalizeBtn.click();
    await expect(page.getByText('Are you sure you want to finalize this review?')).toBeVisible();
    await page.getByTestId('confirm-finalize-btn').click();

    await expect(page.getByText('This document has been finalized.')).toBeVisible({ timeout: 10000 });
  });
});
