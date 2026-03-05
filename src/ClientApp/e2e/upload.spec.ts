import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

test.describe('Document upload and list', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('email address').fill('worker@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|$)/);
  });

  test('upload page shows template dropdown and file input', async ({ page }) => {
    await page.goto('/upload');

    await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();
    await expect(page.getByLabel('Template (optional)')).toBeVisible();
    await expect(page.getByText('Drag and drop a file here')).toBeVisible();
  });

  test('upload a test file and verify success', async ({ page }) => {
    await page.goto('/upload');

    // Create a minimal valid PDF file (API only accepts PDF, PNG, JPEG, TIFF)
    const testFilePath = path.join(__dirname, 'test-upload.pdf');
    fs.writeFileSync(testFilePath, Buffer.from(
      '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
      '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
      '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
      'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
      '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
    ));

    const fileInput = page.locator('#file-input');
    await fileInput.setInputFiles(testFilePath);

    await expect(page.getByText('test-upload.pdf')).toBeVisible();

    await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();

    await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

    // Cleanup
    fs.unlinkSync(testFilePath);
  });

  test('documents page shows table with columns', async ({ page }) => {
    await page.goto('/documents');

    await expect(page.getByRole('heading', { name: 'Documents' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'File Name' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Submitted At' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Processed At' })).toBeVisible();
  });

  test('navigation has upload and documents links', async ({ page }) => {
    await page.goto('/dashboard');

    await page.getByRole('button', { name: 'Upload' }).click();
    await page.waitForURL(/\/upload/);
    await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();

    await page.getByRole('button', { name: 'Documents' }).click();
    await page.waitForURL(/\/documents/);
    await expect(page.getByRole('heading', { name: 'Documents' })).toBeVisible();
  });
});
