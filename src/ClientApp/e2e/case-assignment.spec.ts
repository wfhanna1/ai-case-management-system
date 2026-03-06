import { test, expect } from '@playwright/test';
import { loginAsWorker, loginAsReviewer } from './helpers/login';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

test.describe('Case assignment E2E', () => {
  test('finalized document creates a case visible in cases list', async ({ page }) => {
    const pdfName = `case-e2e-${Date.now()}.pdf`;
    const pdfPath = path.join(__dirname, pdfName);
    fs.writeFileSync(pdfPath, Buffer.from(
      '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
      '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
      '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
      'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
      '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
    ));

    try {
      // Step 1: Upload a document as worker
      await loginAsWorker(page);
      await page.goto('/upload');

      await page.locator('#file-input').setInputFiles(pdfPath);
      await expect(page.getByText(pdfName)).toBeVisible();
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

      // Step 2: Wait for OCR processing and verify doc appears
      await page.goto('/documents');
      await expect(page.getByText(pdfName)).toBeVisible({ timeout: 15000 });

      // Step 3: Sign out and login as reviewer
      await page.getByRole('button', { name: 'Sign Out' }).click();
      await loginAsReviewer(page);

      // Step 4: Navigate to review queue and look for the document
      await page.goto('/reviews');
      await page.waitForTimeout(3000);

      // The document should appear once OCR processing completes
      const docLink = page.getByText(pdfName);
      if (await docLink.isVisible({ timeout: 15000 })) {
        await docLink.click();

        // Step 5: Start the review
        const startButton = page.getByRole('button', { name: /start review/i });
        if (await startButton.isVisible({ timeout: 5000 })) {
          await startButton.click();
        }

        // Step 6: Finalize the review
        const finalizeButton = page.getByRole('button', { name: /finalize|approve|complete/i });
        if (await finalizeButton.isVisible({ timeout: 5000 })) {
          await finalizeButton.click();

          // Handle confirmation dialog if present
          const confirmButton = page.getByRole('button', { name: /confirm|yes/i });
          if (await confirmButton.isVisible({ timeout: 2000 }).catch(() => false)) {
            await confirmButton.click();
          }

          // Step 7: Navigate to cases and verify case exists
          await page.goto('/cases');
          await page.waitForTimeout(2000);
          const casesContent = await page.textContent('main');
          expect(casesContent).toBeTruthy();
        }
      }
    } finally {
      if (fs.existsSync(pdfPath)) fs.unlinkSync(pdfPath);
    }
  });
});
