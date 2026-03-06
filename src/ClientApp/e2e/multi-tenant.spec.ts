import { test, expect } from '@playwright/test';
import { loginAsWorker } from './helpers/login';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function loginAsBetaWorker(page: import('@playwright/test').Page) {
  await page.goto('/login');
  await page.getByLabel('email address').fill('worker@beta.demo');
  await page.getByLabel('password').fill('Demo123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/(dashboard|$)/);
}

test.describe('Multi-tenant isolation E2E', () => {
  test('documents uploaded by Alpha tenant are not visible to Beta tenant', async ({ page }) => {
    const pdfName = `tenant-test-${Date.now()}.pdf`;
    const pdfPath = path.join(__dirname, pdfName);
    fs.writeFileSync(pdfPath, Buffer.from(
      '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
      '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
      '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
      'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
      '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
    ));

    try {
      // Step 1: Login as Alpha worker and upload a document
      await loginAsWorker(page);
      await page.goto('/upload');

      await page.locator('#file-input').setInputFiles(pdfPath);
      await expect(page.getByText(pdfName)).toBeVisible();
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

      // Step 2: Verify document appears in Alpha's documents list
      await page.goto('/documents');
      await expect(page.getByText(pdfName)).toBeVisible({ timeout: 10000 });

      // Step 3: Sign out
      await page.getByRole('button', { name: 'Sign Out' }).click();
      await expect(page).toHaveURL('/login');

      // Step 4: Login as Beta worker
      await loginAsBetaWorker(page);

      // Step 5: Navigate to documents page and verify Alpha's document is NOT visible
      await page.goto('/documents');
      await page.waitForTimeout(2000);
      await expect(page.getByText(pdfName)).not.toBeVisible();
    } finally {
      if (fs.existsSync(pdfPath)) fs.unlinkSync(pdfPath);
    }
  });
});
